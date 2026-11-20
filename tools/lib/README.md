# Offline image atlas helpers

`image-atlas.mjs` owns shared rectangle packing and raster composition. Callers own image selection, alpha trimming, source-coordinate metadata, mesh UVs, file paths and resource catalog updates. It does not modify inputs, rotate/resize images, import Unity assets or manage runtime loading.

- `packAtlasRegions(regions, {maxSize:1024, alignment:4})`: deterministic single-page MaxRects search; returns `{width,height,regions}` with integer `x/y`. Region `id/width/height` are required, other metadata is preserved. Rejects invalid/duplicate IDs, oversized images and batches that cannot fit; split loading groups explicitly rather than silently growing a global atlas.
- `composeAtlas(sharp, layout, images, pngOptions)`: returns a PNG Buffer using either a packed or caller-specified fixed layout. `images` is a Map from region ID to image Buffer/path. Validates sizes, bounds and overlaps. Copies raw RGBA rows, including translucent/transparent RGB, without alpha-over rounding. PNG is lossless by default; palette/quantization is an explicit caller option.

Both current callers use this compositor: `tools/pet-rig-process.mjs` retains the legacy 768px 3x3 layout and explicit palette options; `tools/pet-motion-atlas.mjs` uses packed NPOT regions, original pixels and coordinate-preserving UV remapping. The legacy public outputs are not regenerated as part of motion optimization.

`prepareAtlasImage` in `atlas-padding.mjs` adds a complete transparent gutter even when content touches the source boundary. Offsets can be negative and remain reversible. It preserves retained RGBA bytes; `edge:'extrude'` is an explicit alternative, not the current pet policy. This gutter is for bilinear sampling without mipmaps or block compression, not arbitrary downsampling.

Install the pinned local Sharp 0.35.5 with `npm ci` (Node >=20.9). No external runtime module path is required. Run `npm run test:atlas` and `npm run atlas:check`. `npm run atlas:generate` regenerates the pet PNG, coordinates, bake/fixtures and verified input/mesh manifest; Unity still checks actual imported meshes before builds. Do not edit the generated manifest to bypass a failed check.

Custom skinned meshes require explicit UV remapping. Ordinary uGUI uses the separate native `UiAtlasTools` flow, with explicit lifetime groups and preserved Sprite identities rather than this compositor. See `docs/图集工具与验证.md` for both workflows, reports and target-platform limitations.
