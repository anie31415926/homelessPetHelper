# Station Care Temporary Art

Main care displays the cat rig's original pose and `ShibaCarePose` (frame 0 in
`Assets/Art/PetSit/T_Station_ShibaMotion.png`). During washing or grooming the animal
and dirt/fur layers breathe together and hold still during a stroke. The separate
128 × 128 saved coat fields are unchanged. Run `node tools/station-care-visuals.mjs --dog-only`
to regenerate the Shiba silhouette-to-save projection without rewriting the cat or effects.

Care preview assets generated locally with ComfyUI on 2026-11-18.
These are not final or release-approved resources. Do not overwrite the older StationArt images.

On 2026-11-20 these assets moved with their GUIDs to Assets/Art/Station and became
static Main dependencies. The paired sprites and masks support station care in the
runtime loop; moving out of EditorScenes does not approve final art or release delivery.

The source masters, complete prompts, node workflows, seeds and generation history are under
`art-source/station-care-temporary/` outside Unity Assets. The image inventory is maintained in
`art-source/resource-catalog.json` at repository root.

Two 512 x 512 transparent PNG base animals are shared by the three preview samples. Matching
128 x 128 bottom-left-origin byte masks exclude transparent pixels and the central face.
Dirt and messy fur are independent procedural fields; do not bake these states into more images.

Import: Sprite Single / FullRect, alpha, max size 512, Read/Write off, mipmaps off.
Desktop preview uses uncompressed textures; channel texture compression and download size are unverified.
