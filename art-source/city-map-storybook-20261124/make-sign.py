"""Typeset the factory fascia using the project's OFL source font, without a system font."""
import argparse
import hashlib
from pathlib import Path

from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.ttLib import TTFont

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("font", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
source_hash = hashlib.sha256(args.font.read_bytes()).hexdigest()
if source_hash != "7dae804b344f7bc1a1c8426b9515e9b54d7baac3e123263d0bae94d0a305a732":
    parser.error("Use the pinned ChillRound source described in the game font README")

font = TTFont(args.font)
cmap = font.getBestCmap()
glyphs = font.getGlyphSet()
offset = 0
paths = []
for character in "\u51bb\u5e72\u5de5\u5382":
    glyph = glyphs[cmap[ord(character)]]
    pen = SVGPathPen(glyphs, ntos=lambda value: f"{value:.2f}".rstrip("0").rstrip("."))
    glyph.draw(pen)
    paths.append(f'<path d="{pen.getCommands()}" transform="translate({offset} 0)"/>')
    offset += glyph.width

scale = 46 / offset
args.output.write_text(
    '<svg xmlns="http://www.w3.org/2000/svg" width="64" height="45" viewBox="0 0 64 45">\n'
    '<title>&#20923;&#24178;&#24037;&#21378;</title>\n'
    '<g transform="translate(12 12) rotate(27)">\n'
    '<rect x="-2" y="-7" width="50" height="13" rx="1.5" fill="#4e7d90"/>\n'
    '<g fill="#fff8e2" '
    f'transform="translate(0 3.7) scale({scale:.7f} {-scale:.7f})">\n'
    + "\n".join(paths) + "\n</g>\n</g>\n</svg>\n",
    encoding="ascii", newline="\n",
)
