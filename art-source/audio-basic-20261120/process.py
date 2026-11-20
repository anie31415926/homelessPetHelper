import hashlib
import json
import math
from pathlib import Path
import sys
import wave

import av
import numpy as np


ROOT = Path(__file__).resolve().parent
if len(sys.argv) > 1:
    if sys.argv[1] not in ["v2"]:
        raise ValueError("Unknown candidate variant")
    ROOT /= sys.argv[1]
RATE = 44100
PLAN = json.loads((ROOT / "plan.json").read_text(encoding="utf-8"))


def decode_audio(file):
    with av.open(str(file)) as container:
        resampler = av.AudioResampler(format="fltp", layout="stereo", rate=RATE)
        blocks = []
        for frame in container.decode(audio=0):
            blocks.extend(output.to_ndarray() for output in resampler.resample(frame))
        blocks.extend(output.to_ndarray() for output in resampler.resample(None))
    return np.concatenate(blocks, axis=1).T.astype(np.float64)


def db(value):
    return float(20 * math.log10(max(float(value), 1e-12)))


def stats(samples):
    return {
        "seconds": round(len(samples) / RATE, 6),
        "peakDbfs": round(db(np.max(np.abs(samples))), 3),
        "rmsDbfs": round(db(np.sqrt(np.mean(samples ** 2))), 3),
        "clippedSamples": int(np.count_nonzero(np.abs(samples) >= 0.9999)),
        "dcOffset": round(float(np.mean(samples)), 8),
    }


def save_wave(file, mono):
    pcm = np.round(np.clip(mono, -1, 1) * 32767).astype("<i2")
    with wave.open(str(file), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())
    with wave.open(str(file), "rb") as check:
        assert check.getnframes() == len(mono)
        assert check.getnchannels() == 1 and check.getframerate() == RATE
        decoded = np.frombuffer(check.readframes(check.getnframes()), dtype="<i2") / 32768.0
    return stats(decoded)


def sha256(file):
    return hashlib.sha256(file.read_bytes()).hexdigest()


def prepare_candidate(cue):
    raw_path = ROOT / "raw" / (cue["id"] + ".flac")
    stereo = decode_audio(raw_path)
    assert len(stereo) > RATE and np.all(np.isfinite(stereo))
    mono = stereo.mean(axis=1)
    mono_rms = np.sqrt(np.mean(mono ** 2))
    stereo_rms = np.sqrt(np.mean(stereo ** 2))
    assert mono_rms > 0.0001, "Silent or phase-cancelled source"
    assert mono_rms / max(stereo_rms, 1e-12) > 0.4, "Review stereo cancellation before mono conversion"
    raw_stats = stats(stereo)
    assert raw_stats["clippedSamples"] == 0, "Clipped source: regenerate with pre-export headroom"

    # Select the highest-energy short window, then trim its low-level edge silence.
    maximum = min(len(mono), round(cue["maxSeconds"] * RATE))
    hop = round(0.005 * RATE)
    starts = np.arange(0, max(1, len(mono) - maximum + 1), hop)
    energy = np.concatenate(([0.0], np.cumsum(mono ** 2)))
    scores = energy[starts + maximum] - energy[starts]
    start = max(0, int(starts[int(np.argmax(scores))]) - round(0.012 * RATE))
    end = min(len(mono), start + maximum)
    selected = mono[start:end].copy()
    threshold = max(10 ** (-55 / 20), np.max(np.abs(selected)) * 10 ** (-38 / 20))
    active = np.flatnonzero(np.abs(selected) >= threshold)
    assert len(active) > 0
    trim_start = max(0, int(active[0]) - round(0.008 * RATE))
    trim_end = min(len(selected), int(active[-1]) + round(0.05 * RATE))
    start += trim_start
    end = start + trim_end - trim_start
    selected = mono[start:end].copy()
    dc = float(np.mean(selected))
    selected -= dc
    fade_in = min(len(selected), round(0.003 * RATE))
    fade_out = min(len(selected), round(0.018 * RATE))
    selected[:fade_in] *= np.linspace(0, 1, fade_in)
    selected[-fade_out:] *= np.linspace(1, 0, fade_out)
    gain = 10 ** (cue["peakDbfs"] / 20) / max(np.max(np.abs(selected)), 1e-12)
    selected *= gain
    target = ROOT / "candidates" / (cue["id"] + ".wav")
    candidate_stats = save_wave(target, selected)
    assert candidate_stats["clippedSamples"] == 0
    assert candidate_stats["rmsDbfs"] > -50
    assert abs(candidate_stats["peakDbfs"] - cue["peakDbfs"]) < 0.05
    return {
        "id": cue["id"], "status": "candidate", "loadTiming": "unassigned",
        "source": {"file": raw_path.relative_to(ROOT).as_posix(), "sha256": sha256(raw_path),
                   "sampleRate": RATE, "channels": 2, **raw_stats},
        "candidate": {"file": target.relative_to(ROOT).as_posix(), "sha256": sha256(target),
                      "bytes": target.stat().st_size, "sampleRate": RATE, "channels": 1,
                      "encoding": "PCM16 WAV", **candidate_stats},
        "processing": {"cropStartSample": start, "cropEndSample": end, "dcRemoved": dc,
                       "fadeInSamples": fade_in, "fadeOutSamples": fade_out, "gainDb": round(db(gain), 4),
                       "monoStereoRmsRatio": round(float(mono_rms / stereo_rms), 6),
                       "selection": "Highest energy window, then low-level edge trim; requires listening review."},
        "workflow": f"workflows/{cue['id']}-api.json", "history": f"history/{cue['id']}-history.json",
        "review": {"listening": "pending", "rights": "pending", "targetDevice": "pending"},
    }, selected


def main():
    (ROOT / "candidates").mkdir(exist_ok=True)
    records = []
    audition = [np.zeros(round(RATE * 0.3))]
    timeline = []
    for cue in PLAN["cues"]:
        record, samples = prepare_candidate(cue)
        offset = sum(len(block) for block in audition) / RATE
        timeline.append({"id": cue["id"], "startSeconds": round(offset, 3),
                         "endSeconds": round(offset + len(samples) / RATE, 3)})
        audition.extend([samples, np.zeros(round(RATE * 0.7))])
        records.append(record)
        print(cue["id"], record["candidate"])
    preview = ROOT / "audition.wav"
    save_wave(preview, np.concatenate(audition))
    manifest = {
        "schemaVersion": 1, "status": "candidate", "tool": "Local ComfyUI 0.37.1",
        "models": "../provenance/models.json" if ROOT.name == "v2" else "provenance/models.json", "plan": "plan.json",
        "processing": {"tool": "process.py", "avVersion": av.__version__, "numpyVersion": np.__version__},
        "audition": {"file": "audition.wav", "sha256": sha256(preview), "timeline": timeline},
        "assets": records,
        "verification": "Decoded raw FLAC and exported/reopened WAV; finite, non-silent, mono fold-down and peak checks passed. No manual listening or target-device validation claimed.",
    }
    (ROOT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
