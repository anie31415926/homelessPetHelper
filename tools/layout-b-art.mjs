import { createRequire } from 'node:module';
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
    { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) {
    throw new Error('Only a local ComfyUI endpoint is allowed.');
}
const source = 'art-source/rescue-layout-b';
const empty = process.argv.includes('--empty');
const prefix = empty ? 'empty-' : '';
if (process.argv.includes('--record-existing')) {
    const submitted = JSON.parse(await readFile(`${source}/${prefix}submission.json`, 'utf8'));
    const history = await (await fetch(`${server}/history/${submitted.prompt_id}`)).json();
    if (!history[submitted.prompt_id]) throw new Error('No terminal history exists yet.');
    await writeFile(`${source}/${prefix}history.json`, JSON.stringify(history[submitted.prompt_id], null, 2));
    console.log('Recorded existing generation history without submitting a new job.');
    process.exit(0);
}
if (await access(`${source}/${prefix}submission.json`).then(() => true, () => false)) {
    throw new Error('Submission exists. Inspect its history rather than enqueueing again.');
}
await mkdir(source, { recursive: true });
const cat = 'unity/HomelessPetHelper/Assets/EditorScenes/StationCareArt/T_Station_CatTemporary.png';
const reference = empty ? await readFile(`${source}/master.png`) : await sharp({ create: { width: 768, height: 768, channels: 3, background: '#ffffff' } })
    .composite([{ input: await sharp(await readFile(cat)).resize(300, 300).toBuffer(), left: 234, top: 234 }])
    .png().toBuffer();
if (!empty) await writeFile(`${source}/style-reference.png`, reference);
const form = new FormData();
form.append('image', new Blob([reference]), 'rescue-layout-b-style.png');
form.append('overwrite', 'false');
const uploadResponse = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
if (!uploadResponse.ok) throw new Error(await uploadResponse.text());
const upload = await uploadResponse.json();
const prompt = empty ? 'Edit this image. Remove the orange cat in the center COMPLETELY, including its ears, paws, tail, whiskers and shadow. Fill its entire area with the same empty light cool gray pavement as the surrounding floor. Keep the bicycle shelter, bicycle, roof, walls, bushes, colors, lines and composition unchanged. The central and lower area must be completely EMPTY pavement. No animal or person anywhere. No text or UI. Square 768 by 768 full-bleed background.' : `Create a completely NEW EMPTY ENVIRONMENT illustration, square 768 by 768. The attached cat is ONLY a reference for drawing style: thick soft charcoal outlines, very simple flat pastel color blocks, round cute shapes. Do NOT draw that cat or any animal. The subject is a quiet small Chinese residential bicycle parking shelter seen from a low eye-level viewpoint, NOT a map. Wide frontal composition: a simple muted aqua roof across the upper third, pale blue back wall, one bicycle at the far left, a leafy shrub at the far right. A very large empty light cool gray paved floor in the entire central and lower half, clear space for a separate animal sprite. Soft mint foliage around the shelter, cheerful white daylight. Extremely sparse cute 2D cartoon game backdrop, no texture, no realism, no dramatic shadows. Important: no cats, no dogs, no animals, no people, no text, no lettering, no labels, no buttons, no cards, no UI, no watermark. Opaque full-bleed illustration.`;
const graph = {
    '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
    '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
    '4': { class_type: 'LoadImage', inputs: { image: upload.name } },
    '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt,
        negative_prompt: 'cat, dog, animal, person, UI, interface, button, text, watermark, 3D, photorealistic, clutter',
        resolution: 0, 'images.image_1': ['4', 0] } },
    '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1],
        latent_image: ['5', 2], seed: empty ? 2026111822 : 2026111821, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
    '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
    '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: 'rescue-layout-b/backdrop' } },
};
await writeFile(`${source}/${prefix}workflow.json`, JSON.stringify({ reference: empty ? `${source}/master.png` : cat, prompt: graph }, null, 2));
const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prompt: graph }) });
const submitted = await response.json();
if (!response.ok) throw new Error(JSON.stringify(submitted));
await writeFile(`${source}/${prefix}submission.json`, JSON.stringify(submitted, null, 2));
console.log('Queued', submitted.prompt_id);
let result;
const deadline = Date.now() + 25 * 60 * 1000;
while (Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, 4000));
    const history = await (await fetch(`${server}/history/${submitted.prompt_id}`)).json();
    result = history[submitted.prompt_id];
    if (result?.status?.status_str === 'error') {
        await writeFile(`${source}/${prefix}history.json`, JSON.stringify(result, null, 2));
        throw new Error(JSON.stringify(result.status));
    }
    if (result?.outputs?.['9']?.images?.length) break;
}
if (!result?.outputs?.['9']?.images?.length) throw new Error('Timed out. Inspect saved submission before retrying.');
const file = result.outputs['9'].images[0];
const view = await fetch(`${server}/view?${new URLSearchParams(file)}`);
if (!view.ok) throw new Error(await view.text());
const master = Buffer.from(await view.arrayBuffer());
await writeFile(`${source}/${prefix}master.png`, master);
await writeFile(`${source}/${prefix}history.json`, JSON.stringify(result, null, 2));
await sharp(master).flatten({ background: '#ffffff' }).resize(768, 768, { fit: 'inside', withoutEnlargement: true })
    .jpeg({ quality: 84, mozjpeg: true }).toFile(`${source}/${prefix}backdrop.jpg`);
console.log('Saved separate backdrop candidate');
