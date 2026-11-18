# Station Care Temporary Art

Editor-only care preview assets generated locally with ComfyUI on 2026-11-18.
These are not final or release-approved resources. Do not overwrite the older StationArt images.

The source masters, complete prompts, node workflows, seeds and generation history are under
`art-source/station-care-temporary/` outside Unity Assets. The image inventory is maintained in
`art-source/resource-catalog.json` at repository root.

Two 512 x 512 transparent PNG base animals are shared by the three preview samples. Matching
128 x 128 bottom-left-origin byte masks exclude transparent pixels and the central face.
Dirt and messy fur are independent procedural fields; do not bake these states into more images.

Import: Sprite Single / FullRect, alpha, max size 512, Read/Write off, mipmaps off.
Desktop preview uses uncompressed textures; channel texture compression and download size are unverified.
