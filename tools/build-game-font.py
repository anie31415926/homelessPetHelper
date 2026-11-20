"""Build a deterministic OFL Chinese font subset for the saved game content."""
import argparse
import hashlib
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

parser = argparse.ArgumentParser()
parser.add_argument("source", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
assets = root / "unity/HomelessPetHelper/Assets"
characters = set(chr(c) for c in range(32, 127))
for directory in (assets / "Settings", assets / "Prefabs", assets / "Scripts/Game", root / "src/siege"):
    for file in sorted(directory.rglob("*")):
        if file.suffix in (".json", ".cs", ".prefab"):
            characters.update(file.read_text(encoding="utf-8"))
font = TTFont(args.source, recalcTimestamp=False)
font = instantiateVariableFont(font, {"wght": 400}, inplace=True)
options = subset.Options()
options.recalc_timestamp = False
options.canonical_order = True
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=sorted(map(ord, characters)))
subsetter.subset(font)
output = assets / "Art/Fonts/NotoSansSC-Game.ttf"
output.parent.mkdir(parents=True, exist_ok=True)
font.save(output)
print(f"{output}: {output.stat().st_size} bytes, {len(font.getBestCmap())} glyphs")
print("source sha256:", hashlib.sha256(args.source.read_bytes()).hexdigest())
