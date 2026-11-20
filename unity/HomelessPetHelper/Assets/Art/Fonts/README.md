# Bundled Game Font

`NotoSansSC-Game.ttf` is a static weight-400 subset of Noto Sans SC, under
SIL Open Font License 1.1. See `OFL.txt`, including the original copyright.
The modified font is not sold separately. No Microsoft system font is redistributed.

- Source: https://github.com/google/fonts/tree/main/ofl/notosanssc
- Download: https://raw.githubusercontent.com/google/fonts/main/ofl/notosanssc/NotoSansSC%5Bwght%5D.ttf
- Source SHA256: `a3041811a78c361b1de50f953c805e0244951c21c5bd412f7232ef0d899af0da`
- Derivation: `tools/build-game-font.py`, fonttools 4.68.0, weight 400;
  printable ASCII plus characters in saved Settings, Prefabs, Game scripts and Siege copy.
- Current output: 159484 bytes, 636 cmap entries (2026-11-20 station habitat and siege carry copy).
  Not a full Chinese input font. The source hash and weight remain unchanged.
- Regenerate after adding new copy. Keep the original font in ignored
  `test-results/fonts/`, not in Assets. The build command includes a copy of the OFL.

Example from the repository root, with fonttools installed in the selected Python:

```powershell
python tools/build-game-font.py test-results/fonts/NotoSansSC.ttf
```

Font generation is offline after obtaining the pinned source. Source changes require
reviewing the hash and provenance again; do not assume the remote `main` stays identical.
