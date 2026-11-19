import { access, copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const base = 'art-source/pet-rig';
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) throw new Error('Local ComfyUI only.');
await mkdir(base, { recursive: true });
const selection = process.argv[2] || 'turnaround';
if (!['turnaround', 'front', 'side', 'front-body', 'side-body'].includes(selection)) throw new Error('Unknown asset selection');
if (await access(`${base}/${selection}-master.png`).then(() => true, () => false)) throw new Error('Refusing overwrite; choose a new batch.');
if (selection === 'turnaround') {
  await copyFile('C:/Users/Admin/AppData/Local/Temp/codex-clipboard-53627219-d455-4179-9e61-1da52cadce09.png', `${base}/user-reference.png`);
  await sharp(`${base}/user-reference.png`).extract({ left: 16, top: 31, width: 110, height: 72 }).png().toFile(`${base}/style-reference.png`);
}
const reference = selection === 'turnaround' ? `${base}/style-reference.png` : `${base}/turnaround-master.png`;
const style = 'Soft healing storybook illustration matching the reference kitten: pale apricot orange and ivory white coat, large glossy warm brown eyes, delicate warm peach outlines, subtle watercolor-like soft shading, tiny pink nose, small pink scarf. Not flat geometric vector, not thick black outline, not dot eyes. All parts belong to ONE identical kitten. No text, no labels, no watermark, no border, no checkerboard, no ground or shadow. Genuine transparent alpha background.';
let prompt = selection === 'turnaround'
  ? `${style} Character model sheet with exactly TWO full body views side by side: LEFT strictly front view, RIGHT strictly left-facing side profile. Both views are standing naturally on all four paws, not sitting. Same head height, same body proportions, all paws and tail visible, ears upright, eyes open, relaxed neutral pose. Whole animal with generous margins. This is the same orange and white kitten seen from two different cameras, not two different breeds. Clean complete anatomy.`
  : `${style} Animation-ready exploded cutout sprite sheet for the ${selection === 'front' ? 'strict FRONT view' : 'strict left-facing SIDE PROFILE'} standing kitten in the reference. EXACTLY nine separated individual parts, arranged in a regular 3 by 3 grid, one centered part in each equal cell, no part crosses a cell. Row 1 left: rounded HEAD WITHOUT EARS, with full face and eyes; row 1 middle: oval TORSO with pink scarf at neck, NO head, NO legs, NO tail; row 1 right: one long curved striped TAIL, rounded base. Row 2 left: detached left EAR; row 2 middle: detached right EAR; row 2 right: one detached FRONT LEFT LEG including paw. Row 3 left: one detached FRONT RIGHT LEG including paw; row 3 middle: one detached BACK LEFT LEG including paw; row 3 right: one detached BACK RIGHT LEG including paw. The head ear roots are smooth and completed under the separate ears; each leg shoulder root and tail root fully painted for overlapping joints. Parts individually enlarged to fit their cell with 15 percent blank margin; their relative scale will be restored by the rig. Each leg is a full limb, not just a paw. Do not draw an assembled animal anywhere. Clean separate cutouts, no extra decorative components. ${selection === 'side' ? 'All head, ear, body and paws face LEFT in profile. The near and far limb art are separate, not overlapping.' : 'All parts in symmetric front projection; the head faces the viewer.'}`;
if (selection.endsWith('-body')) prompt = `${style} Render exactly ONE detached animal TORSO sprite for a cutout puppet. ${selection === 'front-body' ? 'Strict front view: a vertically oval fluffy kitten chest and belly, white central belly, orange sides. Pink scarf around the narrow top neck. Symmetric upright pear-shaped body.' : 'Strict left-facing side profile: horizontal bean-shaped orange striped kitten torso, ivory lower belly, shoulder at upper left, hip at right, short narrow neck at upper left wearing pink scarf.'} Completely REMOVE head, face, eyes, ears, all legs, all paws and tail. Only a smooth continuous torso shape, no limb stumps, no holes, no joints drawn. Complete the fur under removed limbs. A single isolated cutout body, centered, generous blank margin. Do not draw any other parts. This is a detached component, NOT a whole animal.`;
const form = new FormData();
form.append('image', new Blob([await readFile(reference)]), `pet-rig-${selection}-reference.png`);
form.append('overwrite', 'false');
const uploadResponse = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
if (!uploadResponse.ok) throw new Error(await uploadResponse.text());
const upload = await uploadResponse.json();
const graph = {
  '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
  '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
  '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
  '4': { class_type: 'LoadImage', inputs: { image: upload.name } },
  '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt, negative_prompt: 'text, watermark, UI frames, checkerboard, background scene, extra limbs, duplicated parts, photorealism, 3d render, thick black outline, dot eyes', resolution: 1024, 'images.image_1': ['4', 0] } },
  '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
  '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2], seed: { turnaround: 2026111901, front: 2026111902, side: 2026111903, 'front-body': 2026111904, 'side-body': 2026111905 }[selection], steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
  '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
  '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `pet-rig/${selection}` } },
};
await writeFile(`${base}/${selection}-workflow.json`, JSON.stringify({ reference, prompt: graph }, null, 2));
const submittedResponse = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) });
const submitted = await submittedResponse.json();
if (!submittedResponse.ok) throw new Error(JSON.stringify(submitted));
console.log('queued', selection, submitted.prompt_id);
let result;
for (let attempt = 0; attempt < 450; attempt++) {
  await new Promise(resolve => setTimeout(resolve, 4000));
  result = (await (await fetch(`${server}/history/${submitted.prompt_id}`)).json())[submitted.prompt_id];
  if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
  if (result?.outputs?.['9']?.images?.length) break;
}
if (!result?.outputs?.['9']?.images?.length) throw new Error('Generation timed out');
const file = result.outputs['9'].images[0];
const response = await fetch(`${server}/view?${new URLSearchParams(file)}`);
if (!response.ok) throw new Error(await response.text());
await writeFile(`${base}/${selection}-master.png`, Buffer.from(await response.arrayBuffer()));
await writeFile(`${base}/${selection}-history.json`, JSON.stringify(result, null, 2));
console.log('saved', selection);
