# Station Care Art

Main care displays the cat rig's original pose and `ShibaCarePose` (frame 0 in
`Assets/Art/PetSit/T_Station_ShibaMotion.png`). During washing or grooming the animal
and dirt/fur layers breathe together and hold still during a stroke. The separate
128 × 128 saved coat fields are unchanged. Run `node tools/station-care-visuals.mjs --dog-only`
to regenerate the Shiba silhouette-to-save projection without rewriting the cat or effects.

The previous temporary Unity preview PNGs were removed.

The 128 x 128 bottom-left-origin care grids are kept as
`T_Station_CatCareMask.bytes` / `T_Station_DogCareMask.bytes`. Only the file names
changed: their GUIDs and bytes are unchanged, so existing save data and the care
grid still work. `T_Station_*CareProjection.bytes` maps the formal sprite
silhouettes back onto that grid, and `T_Station_CareEffects.png` is the shared
bubble/effect atlas.

Dirt and messy fur are independent procedural fields; do not bake these states
into more images. The ComfyUI masters, prompts, workflows and generation history
under `art-source/station-care-temporary/` are a historical snapshot, not current
Unity assets. The image inventory is maintained in
`art-source/resource-catalog.json` at repository root.

Import settings of the formal sprites are owned by their own folders. Channel
texture compression and download size remain unverified.
