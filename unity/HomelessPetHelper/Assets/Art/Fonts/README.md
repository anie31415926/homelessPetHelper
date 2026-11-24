# Shared game font

Current default: `PetRound-Game.ttf`, a renamed static subset derived from
**寒蝉全圆体 / ChillRoundF v3.0**. SIL Open Font License 1.1 permits commercial
embedding and redistribution. Keep `ChillRound-OFL.txt`; the complete license is
also embedded in the derivative's name table. Original copyright is retained.
The upstream Reserved Font Names are not used as the derivative's family name.

- Upstream: https://github.com/Warren2060/ChillRound
- Pinned commit: `46dda1602729b58de96e3fdee9f918d7aaa64727`
- Source file: `ChillRoundF v3.0.ttf` (6,168,772 bytes)
- Source SHA256: `7dae804b344f7bc1a1c8426b9515e9b54d7baac3e123263d0bae94d0a305a732`
- License: https://github.com/Warren2060/ChillRound/blob/46dda1602729b58de96e3fdee9f918d7aaa64727/LICENSE
- Derivative family/PostScript: `PetRoundGame` / `PetRoundGame-Regular`.
- Output: **251,108 bytes / 777 cmap entries**, generated with fonttools 4.68.0.
- Shared by GameContent and saved UI Text references; dynamic font, one face,
  no fallback references, no runtime downloads or font framework change.

Rebuild after adding copy. The tool includes printable ASCII, saved Settings,
Prefabs (including escaped Unicode), Game scripts and Siege copy. It rejects
missing required Chinese/ASCII characters and a mismatched source hash. It is
not a full Chinese input font; user-generated names would need a separate policy.

Example from the repository root, with fonttools installed in the chosen Python:

```bash
python tools/build-game-font.py test-results/fonts/ChillRoundF.ttf \
  --sha256 7dae804b344f7bc1a1c8426b9515e9b54d7baac3e123263d0bae94d0a305a732 \
  --output unity/HomelessPetHelper/Assets/Art/Fonts/PetRound-Game.ttf \
  --family PetRoundGame \
  --license-file unity/HomelessPetHelper/Assets/Art/Fonts/ChillRound-OFL.txt
```

Keep full upstream binaries in ignored evidence directories, outside Assets.
Review source changes, preserve licensing and recheck glyph coverage, short-screen
layout, dynamic atlas memory and repeated opening. This round's Unity Editor
measurements are documented in `docs/救助站反馈与字体优化.md`; they are not a
WeChat/device performance certification.

## Preserved legacy subset

`NotoSansSC-Game.ttf` remains for historical Editor previews and comparison, under
SIL OFL 1.1 (`OFL.txt`). Main's dependency test excludes it from the runtime graph.
Current saved legacy file is 180,680 bytes. Its source and original build remain:

- Source: https://github.com/google/fonts/tree/main/ofl/notosanssc
- Download: https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf
- Source SHA256: `a3041811a78c361b1de50f953c805e0244951c21c5bd412f7232ef0d899af0da`
- Historical default command: `python tools/build-game-font.py test-results/fonts/NotoSansSC.ttf`
  (variable source instantiated at weight 400).

No Microsoft system font is redistributed. Neither font is sold separately.
