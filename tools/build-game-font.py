"""Build an offline, deterministic OFL font subset for the saved game content.

Pass --family for derivatives with a Reserved Font Name. Sources stay outside Assets.
"""
import argparse
import hashlib
import re
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools import subset

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("--sha256", help="Reject a different upstream binary before modifying any output")
parser.add_argument("--output", type=Path, help="Defaults to the historical Noto output")
parser.add_argument("--family", help="New family/PostScript name for a renamed derivative (ASCII, no spaces)")
parser.add_argument("--weight", type=int, default=400)
parser.add_argument("--pet-names", action="store_true", help="Include the GB2312 level-one name repertoire")
parser.add_argument("--license-file", type=Path, help="Embed the original OFL so distributed font binaries retain it")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
assets = root / "unity/HomelessPetHelper/Assets"
source_hash = hashlib.sha256(args.source.read_bytes()).hexdigest()
if args.sha256 and source_hash != args.sha256.lower():
    parser.error("Source SHA256 differs from the reviewed font")
if args.family and not re.fullmatch(r"[A-Za-z][A-Za-z0-9-]*", args.family):
    parser.error("--family must be an ASCII font identifier")
if args.family and not args.license_file:
    parser.error("Renamed derivatives must include --license-file for redistribution")
characters = set(chr(c) for c in range(32, 127))
if args.pet_names:
    for first in range(0xB0, 0xD8):
        for second in range(0xA1, 0xFF):
            characters.update(bytes((first, second)).decode("gb2312", errors="ignore"))
for directory in (assets / "Settings", assets / "Prefabs", assets / "Scripts/Game", root / "src/siege"):
    for file in sorted(directory.rglob("*")):
        if file.suffix in (".json", ".cs", ".prefab"):
            raw = file.read_text(encoding="utf-8")
            characters.update(raw)
            # Unity YAML and JSON may serialize copy as literal Unicode escape sequences.
            characters.update(re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m[1], 16)), raw))
characters = {c for c in characters if c.isprintable() and not (0xD800 <= ord(c) <= 0xDFFF)}
font = TTFont(args.source, recalcTimestamp=False)
if "fvar" in font:
    font = instantiateVariableFont(font, {"wght": args.weight}, inplace=True)
cmap = font.getBestCmap()
# Reject missing Chinese copy and printable ASCII; unrelated C# comment symbols are not UI requirements.
required = {c for c in characters if 32 <= ord(c) < 127 or 0x3400 <= ord(c) <= 0x9FFF}
missing = sorted(required - {chr(c) for c in cmap})
if missing:
    parser.error("Source lacks required characters: " + "".join(missing))
options = subset.Options()
options.recalc_timestamp = False
options.canonical_order = True
options.name_IDs = [0, 1, 2, 3, 4, 5, 6, 13, 14, 16, 17]
options.name_legacy = True
options.name_languages = [0x409, 0x804, 0x404, 0x411]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=sorted(map(ord, characters)))
subsetter.subset(font)
if args.family:
    # Rename every family, unique and full-name record, including Chinese localized names.
    for record in font["name"].names:
        value = {1: args.family, 2: "Regular", 3: args.family + "-Regular",
                 4: args.family + " Regular", 6: args.family + "-Regular",
                 16: args.family, 17: "Regular"}.get(record.nameID)
        if value is not None:
            record.string = value.encode(record.getEncoding())
if args.license_file:
    font["name"].setName(args.license_file.read_text(encoding="utf-8"), 13, 3, 1, 0x409)
    font["name"].setName("https://openfontlicense.org/", 14, 3, 1, 0x409)
output = args.output or assets / "Art/Fonts/NotoSansSC-Game.ttf"
output.parent.mkdir(parents=True, exist_ok=True)
font.save(output)
print(f"{output}: {output.stat().st_size} bytes, {len(font.getBestCmap())} cmap entries")
print("source sha256:", source_hash)
