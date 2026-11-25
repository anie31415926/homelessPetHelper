import { createRequire } from 'node:module';
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
  { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) {
  throw new Error('Only a local ComfyUI endpoint is allowed.');
}
const source = 'art-source/city-map-neighborhood';
const refine = process.argv.includes('--refine');
const removeCharacter = process.argv.includes('--remove-character');
const prefix = removeCharacter ? 'clean-' : refine ? 'simplified-' : '';
const runtime = removeCharacter ? `${source}/clean.jpg` : refine ? `${source}/simplified.jpg` :
  'unity/HomelessPetHelper/Assets/Art/CityMap/T_CityMap_Neighborhood.jpg';
for (const file of [`${source}/${prefix}master.png`, runtime]) {
  if (await access(file).then(() => true, () => false)) throw new Error(`Refusing to overwrite ${file}`);
}
await mkdir(source, { recursive: true });
const reference = 'unity/HomelessPetHelper/Assets/Art/PetMotion/T_Pet_original.png';
const styleSheet = removeCharacter ? await readFile(`${source}/simplified-master.png`) : await sharp({ create: { width: 768, height: 1344, channels: 3, background: '#ffffff' } })
  .composite([{ input: await readFile(reference), gravity: 'centre' }]).png().toBuffer();
if (!removeCharacter && !refine) await writeFile(`${source}/style-reference.png`, styleSheet);
const form = new FormData();
form.append('image', new Blob([styleSheet]), 'city-map-style-reference.png');
form.append('overwrite', 'false');
const uploadResponse = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
if (!uploadResponse.ok) throw new Error(await uploadResponse.text());
const upload = await uploadResponse.json();
const prompt = removeCharacter ? 'Edit this illustrated neighborhood map. Remove the large orange cat in the center COMPLETELY, including its tail, whiskers, feet and shadow. Replace its entire area with the same empty light green grass as the surrounding lawn. Keep EVERY building, tree, bicycle shed, courtyard, path, layout, colors and thick outline style exactly unchanged. The center must be EMPTY grass. No animals or people anywhere, no letters, text, pins, labels or UI. Full-bleed 768 by 1344 portrait neighborhood background.' : refine ? `Create a NEW illustrated portrait small-neighborhood map, 768 by 1344, using ONLY the drawing style of the reference cat. Do not draw any animal. Match the thick dark charcoal wobbly outline and clean solid pastel color blocks of that reference EXACTLY. Extremely simple cute 2D animation background, bold clear contours, smooth flat colors, no pencil texture or crosshatching. Sparse layout: only EIGHT buildings total in the whole image, each large and clearly separated by generous mint green grass. No distant town or repeated small houses. One aqua-roof animal shelter at bottom left, one open teal bicycle parking shed at middle left, a green square residential courtyard at upper right, one coral-roof porch house at lower right, and four small supporting white homes. Connect them with a gently winding pale gray footpath. Four rounded simple trees only. Top 15 percent and bottom 20 percent are quiet open grass, important places in the central 65 percent. Close high bird's-eye map, buildings seen at a slight angle, no horizon, no island outline. Mint grass, aqua and coral roofs, white walls, charcoal lines. Full-bleed opaque artwork. No lettering, labels, signage, pins, UI, people, cats, dogs, animals, shadows, photorealism or 3D rendering.` :
  `Draw a new portrait neighborhood city map for a gentle animal rescue mobile game, 768 by 1344. The attached picture is ONLY a drawing-style reference, NOT the subject or composition. Match its thick slightly wobbly charcoal outlines, simple flat pastel fills and very minimal shading. Do NOT draw the cat or any animal. The new image must be a full-bleed opaque illustrated small Chinese residential neighborhood seen from a high oblique bird's-eye view, with connected gently winding pedestrian roads and modest low-rise homes. Clear, readable map, not a decorative island. A small aqua-roof rescue shelter near bottom left, an open bicycle parking shed at middle left, a leafy residential courtyard at upper middle right, and a coral-roof house with a porch at lower right. Landmarks are spaced apart with clear grassy space around each for separate game markers. Mint green lawns, pale sky blue and coral roofs, white walls, a few round green trees, muted pale paths. Keep the top 15 percent and bottom 20 percent visually calm for separate UI, all key landmarks within the central area. Flat hand-drawn animation background with confident simple silhouettes, cheerful daylight. No characters, no animals, no map pins, no text, no labels, no numbers, no logos, no border, no UI, no paper texture, no realistic shading, no shadows hiding landmarks.`;
const graph = {
  '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
  '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
  '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
  '4': { class_type: 'LoadImage', inputs: { image: upload.name } },
  '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt,
    negative_prompt: 'cat, dog, animal, person, letters, text, watermark, realistic, 3d render, photographic, dense buildings, island, UI, interface, blur',
    resolution: 0, 'images.image_1': ['4', 0] } },
  '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
  '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1],
    latent_image: ['5', 2], seed: removeCharacter ? 2026111813 : refine ? 2026111812 : 2026111811, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
  '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
  '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: 'city-map-neighborhood/map' } },
};
await writeFile(`${source}/${prefix}workflow.json`, JSON.stringify({ reference: removeCharacter ? `${source}/simplified-master.png` : reference, prompt: graph }, null, 2));
const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ prompt: graph }) });
const submitted = await response.json();
if (!response.ok) throw new Error(JSON.stringify(submitted));
console.log('Queued', submitted.prompt_id);
await writeFile(`${source}/${prefix}submission.json`, JSON.stringify(submitted, null, 2));
let result;
const deadline = Date.now() + 25 * 60 * 1000;
while (Date.now() < deadline) {
  await new Promise(resolve => setTimeout(resolve, 4000));
  const history = await (await fetch(`${server}/history/${submitted.prompt_id}`)).json();
  result = history[submitted.prompt_id];
  if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
  if (result?.outputs?.['9']?.images?.length) break;
}
if (!result?.outputs?.['9']?.images?.length) throw new Error('Generation timed out; inspect submission before retrying.');
const file = result.outputs['9'].images[0];
const view = await fetch(`${server}/view?${new URLSearchParams(file)}`);
if (!view.ok) throw new Error(await view.text());
const master = Buffer.from(await view.arrayBuffer());
await writeFile(`${source}/${prefix}master.png`, master);
await writeFile(`${source}/${prefix}history.json`, JSON.stringify(result, null, 2));
await sharp(master).flatten({ background: '#ffffff' }).resize(768, 1344, { fit: 'inside', withoutEnlargement: true })
  .jpeg({ quality: 82, mozjpeg: true }).toFile(runtime);
console.log('Saved master and JPEG for review', runtime);
