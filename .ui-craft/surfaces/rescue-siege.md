# Rescue Siege

Independent `siege.html` reverse-tower-defense trial, approved 2026-11-19. Preserve every old HTML and Unity flow. Product recovery: `docs/逆塔防HTML首版实现记录.md`.

- Continuous three-lane field: deployment bottom-left, kitten top-right, outer/inner tower pads, no high-ground tower separate from the animal. The three routes are pre-open samples, not implemented cleanup.
- Reuse mint/coral, Chinese rounded display face and Microsoft YaHei body; keep the map primary, compact hand and commands below. Hard preview cap 375x667, landscape 667x375, narrower screens reflow without global scaling.
- Signature: the same living battlefield freezes at each player decision. Material links and the chosen anchor show where a paid merge resolves. An additional material selector handles overlapping units; deployment and reroute destinations are independent.
- States: decision, running, explicit/blur pause, invalid plan, merge selection, reroute plan, terminal success or timeout. No state mutation from UI callbacks; commands are validated atomically in pure rules.
- Assets: existing complete raster cat `pet-mesh.cat-side`, temporary/preview-only; unit/tower icons from Lucide. Whole-image tilt, ground warnings and impact rings are temporary feedback, not a finished paw/tail rig.
- Defender-strength pass approved 2026-11-19: persistent timed guard, true spatial multi-unit sweep (including adjacent routes and transit), finite telegraphed repairs, tower range 52 -> 85. The warning and hit test share the exact frozen area object; never substitute lane-only damage behind a broad visual. Tapping a tower shows its range in place, without hiding the field in a modal. Preserve deliberate response windows, recovery and pause semantics.
- Checked 320x568 through desktop and landscape, including expanded three-material controls, image decoding, real control victory, freezes and seeded retry. Evidence in ignored `test-results/siege/`.
- Remaining: real-device touch sizing, animal animation, long-session tactical diversity, difficulty and cleanup-to-route mapping. Fixed single-lane blind play is now weaker, but mechanical lane cycling still clears all eight normal samples and alert is much harsher; do not claim solved balance or production approval.
