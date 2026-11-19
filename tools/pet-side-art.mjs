import { access, mkdir, readFile, writeFile } from 'node:fs/promises';

const variant = process.argv[2] || 'v1';
if (!['v1', 'v2'].includes(variant)) throw new Error('Unknown variant');
const base = variant === 'v1' ? 'art-source/pet-motion/side-rest' : 'art-source/pet-motion/side-rest/v2';
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) throw new Error('Local ComfyUI only');
await mkdir(base, { recursive: true });
if (await access(`${base}/master.png`).then(() => true, () => false)) throw new Error('Refusing to overwrite master');
const reference = 'public/pet-mesh/cat-side.png';
let prompt = 'Change ONLY the pose of this exact same apricot orange and ivory kitten with pink scarf, keeping its face, coat pattern, proportions and delicate warm watercolor storybook style. ONE complete kitten lying fully ON ITS SIDE on the floor, relaxed side-reclining. Torso horizontal and resting on its flank, cheek and head resting low on its outstretched forepaws at LEFT. Hind legs relaxed and extended toward the lower right, with an unbroken rounded hip contour. Tail rests along the ground curving to the RIGHT, separate from the paws. Eyes peacefully half open, ears relaxed. The animal is NOT standing, NOT sitting, NOT sphinx pose, NOT crouching with feet tucked underneath. Show the entire animal including all ears, paws and tail, no crop, generous blank margin. A single connected finished illustration, NOT a sprite sheet, NOT detached parts. Flat solid pale cyan background RGB 180 225 235, no floor line, no ground shadow, no text, no watermark. Keep the ivory white fur white, not cyan.';
if (variant === 'v2') prompt += ' CRITICAL: keep the original chibi baby proportions exactly: extremely large round head, head WITHOUT ears as wide as the ENTIRE torso from neck to rump, tiny short plump body and short thick paws, large warm brown eyes. The head occupies approximately FORTY-FIVE PERCENT of the full nose-to-rump length. Simple sparse soft orange coat markings, smooth gentle peach contour. Do NOT replace it with a realistic kitten, do NOT lengthen the body, do NOT shrink the head, do NOT add detailed fur strands or realistic dense tabby stripes. Both eyes open with sleepy expression, not one winking eye. Cheek rests on front paw while the belly faces the viewer in a true side-reclining position.';
const form = new FormData();
form.append('image', new Blob([await readFile(reference)]), 'pet-side-rest-reference.png');
form.append('overwrite', 'false');
const uploaded = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
if (!uploaded.ok) throw new Error(await uploaded.text());
const upload = await uploaded.json();
const graph = {
    '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
    '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
    '4': { class_type: 'LoadImage', inputs: { image: upload.name } },
    '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt, negative_prompt: 'standing, sitting, sphinx, crouching, raised chest, extra limbs, detached parts, text, watermark, checkerboard, background scene, photorealism, 3d, thick black outlines', resolution: 768, 'images.image_1': ['4', 0] } },
    '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2], seed: variant === 'v2' ? 2026111932 : 2026111931, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
    '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
    '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: 'pet-motion/side-rest' } },
};
await writeFile(`${base}/workflow.json`, JSON.stringify({ reference, prompt: graph }, null, 2));
const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) });
const submitted = await response.json();
if (!response.ok) throw new Error(JSON.stringify(submitted));
await writeFile(`${base}/submission.json`, JSON.stringify(submitted, null, 2));
console.log('queued', submitted.prompt_id);
for (let attempt = 0; attempt < 450; attempt++) {
    await new Promise(resolve => setTimeout(resolve, 4000));
    const result = (await (await fetch(`${server}/history/${submitted.prompt_id}`)).json())[submitted.prompt_id];
    if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
    const file = result?.outputs?.['9']?.images?.[0];
    if (!file) continue;
    const image = await fetch(`${server}/view?${new URLSearchParams(file)}`);
    if (!image.ok) throw new Error(await image.text());
    await writeFile(`${base}/master.png`, Buffer.from(await image.arrayBuffer()));
    await writeFile(`${base}/history.json`, JSON.stringify(result, null, 2));
    console.log('saved', `${base}/master.png`);
    process.exit(0);
}
throw new Error('Generation timed out; inspect the recorded submission before retrying');
