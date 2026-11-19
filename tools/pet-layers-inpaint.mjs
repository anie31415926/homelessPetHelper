import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) throw new Error('Local ComfyUI only');
const folder = process.env.PET_COMPLETION_FOLDER || 'art-source/pet-layers/ai-completion-v2';
await mkdir(folder, { recursive: true });
if (await access(`${folder}/master.png`).then(() => true, () => false)) throw new Error('Refusing to overwrite generated master');
const crop = { left: 225, top: 330, width: 112, height: 165 };
const { data: mask, info } = await sharp('art-source/pet-layers/completion-mask.png').ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const guide = Buffer.alloc(mask.length);
for (let p = 0; p < mask.length; p += 4) { guide[p] = guide[p + 1] = guide[p + 2] = 165; guide[p + 3] = mask[p + 3]; }
// Composite before crop/resize; Sharp applies transforms before composite in one pipeline.
const assembled = await sharp(guide, { raw: { width: info.width, height: info.height, channels: 4 } })
  .composite([{ input: 'art-source/pet-layers/visible-only.png' }]).png().toBuffer();
await sharp(assembled).extract(crop).resize({ height: 768 }).flatten({ background: '#f0f6f1' }).png().toFile(`${folder}/edit-guide.png`);
const upload = async (path, name) => {
  const form = new FormData(); form.append('image', new Blob([await readFile(path)]), name); form.append('overwrite', 'false');
  const response = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
  if (!response.ok) throw new Error(await response.text()); return (await response.json()).name;
};
const edit = await upload(`${folder}/edit-guide.png`, 'pet-far-hind-edit-guide.png');
const reference = await upload('public/pet-mesh/cat-side.png', 'pet-far-hind-original-cat.png');
const prompt = 'Image 1 is the EDIT TARGET, a partially visible far-side hind leg extracted from the exact kitten in image 2. Image 2 is a REFERENCE ONLY for the same kitten anatomy, apricot fur colours, ivory paw, thin peach outline and gentle soft shading. In image 1 the lower ivory paw and ankle already exist; the neutral GREY curved region above the ankle represents ONLY the missing upper leg previously hidden by the body. Replace ONLY that grey region with anatomically plausible upper hind leg fur that connects smoothly into the existing ankle and paw. Keep the exact image 1 canvas, placement, size, leg silhouette, existing lower paw and left-facing profile unchanged. Soft pale apricot upper thigh, subtle delicate orange stripes, white lower shin, warm thin outline. One complete bent hind leg, upper root at upper right and one paw at lower left, as already indicated by the target silhouette. Do not add a second paw, head, body, torso, tail, scarf, labels, shadows or other objects. Do not make a new independent character design. Preserve the solid pale green background. The upper leg should have a smooth rounded filled root for overlap under the body.';
const graph = {
  '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
  '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
  '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
  '4': { class_type: 'LoadImage', inputs: { image: edit } },
  '10': { class_type: 'LoadImage', inputs: { image: reference } },
  '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], vae: ['3', 0], prompt,
    negative_prompt: 'whole cat, head, torso, extra legs, extra paw, different paw, different layout, cut paw, realistic fur, black outline, lettering, watermark, checkerboard, grey unfinished area',
    resolution: 768, 'images.image_1': ['4', 0], 'images.image_2': ['10', 0] } },
  '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
  '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2], seed: 2026111912, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
  '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
  '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: 'pet-layers/hidden-completion' } },
};
await writeFile(`${folder}/workflow.json`, JSON.stringify({ crop, prompt: graph,
  method: 'Two-reference image edit, not latent masked inpaint. The final saved runtime patch is restricted by the completion mask AFTER decoding; the original visible paw and foreground are never taken from the model result.' }, null, 2));
const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) });
const submitted = await response.json(); if (!response.ok) throw new Error(JSON.stringify(submitted));
console.log('queued hidden completion', submitted.prompt_id);
let result;
for (let i = 0; i < 450; i++) {
  await new Promise(resolve => setTimeout(resolve, 4000));
  result = (await (await fetch(`${server}/history/${submitted.prompt_id}`)).json())[submitted.prompt_id];
  if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
  if (result?.outputs?.['9']?.images?.length) break;
}
if (!result?.outputs?.['9']?.images?.length) throw new Error('Generation timeout');
const file = result.outputs['9'].images[0];
await writeFile(`${folder}/master.png`, Buffer.from(await (await fetch(`${server}/view?${new URLSearchParams(file)}`)).arrayBuffer()));
await writeFile(`${folder}/history.json`, JSON.stringify(result, null, 2));
console.log('saved completion master');
