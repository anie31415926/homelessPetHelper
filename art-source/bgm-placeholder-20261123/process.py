import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import sys

import av
import numpy as np
from scipy.io import wavfile


ROOT = Path(__file__).resolve().parent
PLAN = json.loads((ROOT / "plan.json").read_text(encoding="utf-8"))
SPEC = PLAN["delivery"]
RATE = 44100
BIN = Path(sys.executable).parent.parent / "Lib/site-packages/ffmpeg/binaries/bin"
FFMPEG = BIN / "ffmpeg.exe"
FFPROBE = BIN / "ffprobe.exe"
WORK = ROOT / "server/processing"


def run(executable, args):
    result = subprocess.run(
        [str(executable), *map(str, args)], capture_output=True, check=True,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
    )
    return result.stdout.decode("utf-8"), result.stderr.decode("utf-8")


def transcode(source, target, args):
    run(FFMPEG, ["-hide_banner", "-nostdin", "-y", "-i", source,
                 "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact",
                 *args, target])


def decode(file, rate=RATE):
    with av.open(str(file)) as container:
        resampler = av.AudioResampler(format="fltp", layout="stereo", rate=rate)
        blocks = []
        for frame in container.decode(audio=0):
            blocks.extend(out.to_ndarray() for out in resampler.resample(frame))
        blocks.extend(out.to_ndarray() for out in resampler.resample(None))
    return np.concatenate(blocks, axis=1).T.astype(np.float64)


def sha(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


def db(value):
    return 20 * math.log10(max(float(value), 1e-12))


def loudness(file):
    _, log = run(FFMPEG, ["-hide_banner", "-nostdin", "-i", file,
                         "-af", "loudnorm=I=-20:TP=-3:LRA=11:print_format=json",
                         "-f", "null", "-"])
    result = json.JSONDecoder().raw_decode(log[log.rfind('{'):])[0]
    return {key: float(result[key]) for key in ["input_i", "input_tp", "input_lra"]}


def probe(file):
    output, _ = run(FFPROBE, ["-v", "error", "-show_streams", "-show_format", "-of", "json", file])
    data = json.loads(output)
    stream = data["streams"][0]
    return {"codec": stream["codec_name"], "sampleRate": int(stream["sample_rate"]),
            "channels": stream["channels"], "durationSeconds": float(data["format"]["duration"])}


def metrics(samples, rate):
    assert np.all(np.isfinite(samples)), "Non-finite audio"
    energy = np.sqrt(np.mean(samples ** 2))
    mono_ratio = np.sqrt(np.mean(np.mean(samples, axis=1) ** 2)) / max(energy, 1e-12)
    assert energy > 0.0001 and mono_ratio > 0.4, "Silent or phase-cancelled audio"
    differences = np.abs(np.diff(samples, axis=0))
    seam = float(np.max(np.abs(samples[0] - samples[-1])))
    return {"seconds": round(len(samples) / rate, 6), "frames": len(samples),
            "peakDbfs": round(db(np.max(np.abs(samples))), 3),
            "rmsDbfs": round(db(energy), 3),
            "clippedSamples": int(np.count_nonzero(np.abs(samples) >= 0.9999)),
            "dcOffset": round(float(np.max(np.abs(np.mean(samples, axis=0)))), 8),
            "monoRmsRatio": round(float(mono_ratio), 6),
            "seamStepDbfs": round(db(seam), 3),
            "ordinaryStepP999Dbfs": round(db(np.quantile(differences, 0.999)), 3)}


def make_loop(samples, rate, seconds, fade_seconds):
    count, fade = round(seconds * rate), round(fade_seconds * rate)
    span = count + fade
    assert fade > 1 and span + rate * 2 < len(samples), "Insufficient source for loop"
    candidates = range(2 * rate, len(samples) - span - rate + 1, rate // 4)
    best = None
    for start in candidates:
        segment = samples[start:start + span]
        first, last = segment[:fade], segment[-fade:]
        first_rms = np.sqrt(np.mean(first ** 2))
        last_rms = np.sqrt(np.mean(last ** 2))
        energies = np.array([np.mean(block ** 2) for block in np.array_split(segment, 32)])
        # Avoid near-silent windows and large energy changes at the crossfade.
        score = abs(db(first_rms) - db(last_rms)) + np.std(np.log(energies + 1e-12))
        if np.min(energies) < np.mean(energies) * 0.03:
            score += 100
        if best is None or score < best[0]:
            best = (score, start)
    assert best is not None
    segment = samples[best[1]:best[1] + span].copy()
    segment -= np.mean(segment, axis=0, keepdims=True)
    weight = ((1 - np.cos(np.linspace(0, math.pi, fade))) / 2)[:, None]
    overlap = segment[-fade:] * (1 - weight) + segment[:fade] * weight
    result = np.concatenate((segment[fade:-fade], overlap))
    assert len(result) == count
    return result, {"sourceStartFrame": best[1], "sourceSpanFrames": span,
                    "crossfadeFrames": fade, "windowScore": round(float(best[0]), 6),
                    "method": "DC removal, cosine equal-gain tail/head crossfade, rotated seam"}


def check_audio(file, expected_codec, rate):
    data = decode(file, rate)
    result = {**probe(file), **metrics(data, rate), "loudness": loudness(file)}
    assert result["codec"] == expected_codec
    assert result["sampleRate"] == rate and result["channels"] == 2
    assert abs(result["seconds"] - SPEC["durationSeconds"]) < 0.02
    assert result["clippedSamples"] == 0
    assert result["dcOffset"] < 0.002
    assert abs(result["loudness"]["input_i"] - SPEC["targetLufs"]) <= SPEC["lufsTolerance"]
    assert result["loudness"]["input_tp"] <= SPEC["maxTruePeakDbtp"]
    assert result["seamStepDbfs"] <= result["ordinaryStepP999Dbfs"] + 6
    return result


def encode_cue(cue, loop):
    name = cue["assetName"]
    temporary = WORK / f"{cue['id']}-float.wav"
    wavfile.write(temporary, RATE, loop.astype(np.float32))
    before = loudness(temporary)
    gain = min(SPEC["targetLufs"] - before["input_i"],
               SPEC["maxTruePeakDbtp"] - 1 - before["input_tp"])
    master = ROOT / "masters" / f"{name}.wav"
    ogg = ROOT / "candidates" / f"{name}.ogg"
    preview = ROOT / "audition" / f"{name}.mp3"
    transcode(temporary, master, ["-af", f"volume={gain:.8f}dB", "-c:a", "pcm_s16le", "-ar", RATE])
    transcode(master, ogg, ["-c:a", SPEC["codec"], "-b:a", SPEC["targetBitrate"],
                           "-ar", SPEC["sampleRate"], "-ac", SPEC["channels"]])
    transcode(master, preview, ["-c:a", "libmp3lame", "-b:a", "96k", "-ar", 44100,
                               "-metadata", f"title={name} - temporary not final"])
    return master, ogg, preview, round(gain, 8)


def file_record(file, analysis=None):
    return {"path": file.relative_to(ROOT).as_posix(), "bytes": file.stat().st_size,
            "sha256": sha(file), **({"analysis": analysis} if analysis else {})}


def process_cue(cue):
    raw = ROOT / "raw" / f"{cue['id']}.flac"
    source = decode(raw)
    original = metrics(source, RATE)
    assert original["clippedSamples"] == 0 and original["seconds"] >= 45
    loop, edit = make_loop(source, RATE, SPEC["durationSeconds"], SPEC["crossfadeSeconds"])
    master, ogg, preview, gain = encode_cue(cue, loop)
    master_metrics = check_audio(master, "pcm_s16le", RATE)
    compressed_metrics = check_audio(ogg, "vorbis", SPEC["sampleRate"])
    assert ogg.stat().st_size <= SPEC["maxFileBytes"]
    # The preview repeats the decoded OGG boundary, not a different lossless edit.
    decoded = decode(ogg, SPEC["sampleRate"])
    seam = np.concatenate((decoded[-6 * SPEC["sampleRate"]:], decoded[:6 * SPEC["sampleRate"]]))
    seam_wav = WORK / f"{cue['id']}-seam.wav"
    wavfile.write(seam_wav, SPEC["sampleRate"], seam.astype(np.float32))
    seam_preview = ROOT / "audition" / f"{cue['id']}-loop-boundary.mp3"
    transcode(seam_wav, seam_preview, ["-c:a", "libmp3lame", "-b:a", "96k"])
    return {"id": cue["id"], "assetName": cue["assetName"], "suggestedCueId": cue["suggestedCueId"],
            "status": "temporary", "final": False, "loadPhase": "unassigned",
            "review": {"listening": "pending", "loopMusicalPhrase": "pending",
                       "commercialRights": "pending", "targetDevice": "pending"},
            "source": file_record(raw, original), "edit": {**edit, "linearGainDb": gain},
            "master": file_record(master, master_metrics), "candidate": file_record(ogg, compressed_metrics),
            "preview": file_record(preview), "loopBoundaryPreview": file_record(seam_preview)}


def verify():
    manifest = json.loads((ROOT / "manifest.json").read_text(encoding="utf-8"))
    assert manifest["status"] == "temporary" and manifest["final"] is False
    assert manifest["inputs"] == {name: sha(ROOT / name) for name in ["plan.json", "process.py", "generate.mjs"]}
    assert len(manifest["cues"]) == len(PLAN["cues"])
    for cue, entry in zip(PLAN["cues"], manifest["cues"]):
        assert cue["id"] == entry["id"] and not entry["final"]
        submission = json.loads((ROOT / f"history/{cue['id']}-submission.json").read_text())
        workflow = ROOT / f"workflows/{cue['id']}-api.json"
        graph = json.loads(workflow.read_text())
        history = json.loads((ROOT / f"history/{cue['id']}-history.json").read_text())
        assert history["status"]["status_str"] == "success" and history["status"]["completed"]
        assert submission["prompt_id"] == history["prompt"][1]
        assert graph == history["prompt"][2] and sha(workflow) == submission["workflowSha256"]
        assert graph["3"]["inputs"]["text"] == cue["prompt"] and graph["7"]["inputs"]["seed"] == cue["seed"]
        for key in ["source", "master", "candidate", "preview", "loopBoundaryPreview"]:
            file = ROOT / entry[key]["path"]
            assert sha(file) == entry[key]["sha256"] and file.stat().st_size == entry[key]["bytes"]
        assert check_audio(ROOT / entry["candidate"]["path"], "vorbis", SPEC["sampleRate"]) == entry["candidate"]["analysis"]
        assert entry["candidate"]["bytes"] <= SPEC["maxFileBytes"]
    total = sum(entry["candidate"]["bytes"] for entry in manifest["cues"])
    assert total == manifest["candidateTotalBytes"] and total <= SPEC["maxTotalBytes"]
    return {"status": "passed", "cueCount": len(manifest["cues"]), "candidateTotalBytes": total,
            "manifestSha256": sha(ROOT / "manifest.json"), "listening": "pending",
            "unityImported": False, "wechatDeviceValidated": False}


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--verify":
        result = verify()
        (ROOT / "verification.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
        print(json.dumps(result))
        return
    for directory in [WORK, ROOT / "masters", ROOT / "candidates", ROOT / "audition"]:
        directory.mkdir(parents=True, exist_ok=True)
    entries = [process_cue(cue) for cue in PLAN["cues"]]
    total = sum(entry["candidate"]["bytes"] for entry in entries)
    assert total <= SPEC["maxTotalBytes"]
    manifest = {"status": "temporary", "final": False, "unityImported": False,
                "specification": SPEC, "candidateTotalBytes": total,
                "inputs": {name: sha(ROOT / name) for name in ["plan.json", "process.py", "generate.mjs"]},
                "tools": {"python": sys.version, "pyav": av.__version__, "ffmpegSha256": sha(FFMPEG)},
                "cues": entries}
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"candidateTotalBytes": total, "cues": [entry["id"] for entry in entries]}))


if __name__ == "__main__":
    main()
