import json
from pathlib import Path
import runpy


root = Path(__file__).resolve().parent
main = runpy.run_path(str(root.parent / "process.py"))["main"]
plan = json.loads((root / "plan.json").read_text("utf-8"))
plan["cues"] = [cue for cue in plan["cues"] if not cue.get("status", "").startswith("rejected-")]
main.__globals__.update(ROOT=root, PLAN=plan)
main()
manifest_path = root / "manifest.json"
manifest = json.loads(manifest_path.read_text("utf-8"))
manifest["models"] = "../provenance/models.json"
manifest["processing"]["tool"] = "../process.py via ./process.py"
manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
