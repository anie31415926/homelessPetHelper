# Station Care Art

As of the 2026-11-26 cleanup, station care uses the formal rescued-animal sprites
`Assets/Art/PetMotion/T_Pet_original.png` (cat) and
`Assets/Art/Outing/T_Outing_DogNatural.png` (dog). They are bound in
`Assets/Settings/Game/GameContent.asset` and in the station panels; the temporary
512 x 512 preview PNGs and every `*Temporary*` name in this folder were removed.

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
