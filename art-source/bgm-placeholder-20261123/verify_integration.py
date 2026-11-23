import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
PREFIX = "unity/HomelessPetHelper/"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


def report_path(file):
    return file.resolve().relative_to(ROOT).as_posix()


def tests(file, allowed_skip=None):
    root = ET.parse(file).getroot()
    cases = list(root.iter("test-case"))
    failures = [case.attrib.get("fullname", case.attrib.get("name")) for case in cases
                if case.attrib.get("result") != "Passed" and
                case.attrib.get("fullname") != allowed_skip]
    assert cases and not failures, f"Tests not all passed: {failures}"
    return {"path": report_path(file), "sha256": sha(file.read_bytes()),
            "passed": sum(case.attrib.get("result") == "Passed" for case in cases), "total": len(cases),
            "failed": int(root.attrib.get("failed", 0)), "skipped": int(root.attrib.get("skipped", 0))}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--edit-xml", type=Path, required=True)
    parser.add_argument("--play-xml", type=Path, required=True)
    parser.add_argument("--baseline-xml", type=Path)
    parser.add_argument("--integration-xml", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    staged = git("diff", "--cached", "--name-only", "-z").decode("utf-8").split("\0")
    owned = [path for path in staged if path.startswith(PREFIX)]
    records = []
    for path in owned:
        expected = git("show", ":" + path)
        actual = (args.project / path[len(PREFIX):]).read_bytes()
        assert actual == expected, f"Tested BGM input differs from staged blob: {path}"
        records.append({"path": path, "bytes": len(expected), "sha256": sha(expected)})
    assert len(records) == 22, "Unexpected staged Unity scope"
    manifest_path = ROOT / "art-source/bgm-placeholder-20261123/manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    integration = json.loads((manifest_path.parent / "integration.json").read_text(encoding="utf-8"))
    assert integration["sourceManifestSha256"] == sha(manifest_path.read_bytes())
    for entry in integration["assets"]:
        assert sha((ROOT / entry["runtimeFile"]).read_bytes()) == entry["sourceSha256"]
        assert entry["status"] == "temporary" and not entry["final"]
    baseline_name = None
    baseline = None
    if args.baseline_xml:
        baseline = tests(args.baseline_xml)
        baseline_cases = list(ET.parse(args.baseline_xml).getroot().iter("test-case"))
        assert len(baseline_cases) == 1
        baseline_name = baseline_cases[0].attrib["fullname"]
        skipped = [case for case in ET.parse(args.play_xml).getroot().iter("test-case")
                   if case.attrib.get("result") != "Passed"]
        assert len(skipped) == 1 and skipped[0].attrib["fullname"] == baseline_name
    report = {"status": "passed", "stagedTreeAtAudit": git("write-tree").decode().strip(),
              "testedProject": report_path(args.project), "ownedUnityInputs": records,
              "ownedFingerprint": sha(json.dumps(records, separators=(",", ":")).encode()),
              "editMode": tests(args.edit_xml), "playMode": tests(args.play_xml, baseline_name),
              "baselineRetest": baseline,
              "parentIntegration": tests(args.integration_xml) if args.integration_xml else None,
              "sourceManifestSha256": sha(manifest_path.read_bytes()),
              "sourceOggBytes": manifest["candidateTotalBytes"],
              "standaloneAuditBytes": integration["audit"]["totalBundleBytes"],
              "scope": "Exact staged BGM Unity blobs match tested project; other project inputs are not certified here.",
              "wechatDeviceValidated": False, "finalMusic": False}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: report[key] for key in ["status", "ownedFingerprint", "editMode", "playMode"]}))


if __name__ == "__main__":
    main()
