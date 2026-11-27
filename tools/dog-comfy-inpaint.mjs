// 通过本地 ComfyUI（Qwen-Image 2.1 edit）做单块补绘：上传底稿+mask，提交 graph，取回结果并自动贴回。
// 用法：node tools/dog-comfy-inpaint.mjs <neck|belly|hip> [--seed N] [--resolution 768]
import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import sharp from 'sharp';

const server = 'http://127.0.0.1:8188';
const root = path.resolve(import.meta.dirname, '..');
const inputDir = path.join(root, 'art-source/dog-rig-prototype-20261127/comfy-input');
const outRoot = path.join(root, 'art-source/dog-rig-prototype-20261127/comfy-out');
const targets = {
    neck: {
        plate: 'plate-512.png', mask: 'mask-neck-back.png', seed: 2026112701,
        prompt: '只补画这张水彩柴犬插画里被头部挡住的那一小块：颈后到肩胛的暖橙色被毛，带手绘水彩笔触和细褐色轮廓边缘，与周围毛发自然衔接。不要画头、眼睛、四肢、尾巴或任何背景，只填满白色区域。'
    },
    belly: {
        plate: 'plate-512.png', mask: 'mask-belly.png', seed: 2026112702,
        prompt: '只补画这张水彩柴犬插画里前腿之间与胸腹下方的白色奶油毛和暖色过渡，手绘水彩笔触、细褐色轮廓，与周围毛发自然衔接。不要画腿、头、尾巴或背景，只填满白色区域。'
    },
    hip: {
        plate: 'plate-512.png', mask: 'mask-hip-back.png', seed: 2026112703,
        prompt: '只补画这张水彩柴犬插画里尾巴与臀部后面的躯干侧影：暖橙色被毛、圆形臀部轮廓线，手绘水彩笔触，与周围毛发自然衔接。不要画尾巴、腿、头或背景，只填满白色区域。'
    }
};

const name = process.argv[2];
if (!targets[name]) throw Error('Supply one of: ' + Object.keys(targets).join(', '));
const target = targets[name];
const resolution = Number(process.argv[process.argv.indexOf('--resolution') + 1]) || 768;
const seedArg = process.argv.includes('--seed') ? Number(process.argv[process.argv.indexOf('--seed') + 1]) : target.seed;
const negative = '写实照片，3D渲染，厚涂油画，黑色背景，地面阴影，文字，水印，多个动物，额外的头或腿，深色描边';
const promptCn = target.prompt;

const api = async (pathname, init) => {
    const response = await fetch(server + pathname, init);
    if (!response.ok) throw Error((await response.text()).slice(0, 400));
    return response;
};
const sha = b => createHash('sha256').update(b).digest('hex');

async function upload(buffer, filename) {
    const form = new FormData();
    form.append('image', new Blob([buffer]), filename);
    form.append('overwrite', 'true');
    const json = await (await api('/upload/image', { method: 'POST', body: form })).json();
    return json.name;
}

const plate = await sharp(path.join(inputDir, target.plate)).resize(resolution, resolution, { kernel: 'lanczos3' }).png().toBuffer();
const mask = await sharp(path.join(inputDir, target.mask)).resize(resolution, resolution, { kernel: 'nearest' }).png().toBuffer();
const plateName = await upload(plate, `dog-${name}-plate-${resolution}.png`);
const maskName = await upload(mask, `dog-${name}-mask-${resolution}.png`);

const graph = {
    '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
    '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
    '4': { class_type: 'LoadImage', inputs: { image: plateName } },
    '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt: promptCn, negative_prompt: negative, resolution, 'images.image_1': ['4', 0] } },
    '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
    '11': { class_type: 'LoadImage', inputs: { image: maskName } },
    '12': { class_type: 'ImageToMask', inputs: { image: ['11', 0], channel: 'red' } },
    '13': { class_type: 'VAEEncode', inputs: { pixels: ['4', 0], vae: ['3', 0] } },
    '14': { class_type: 'SetLatentNoiseMask', inputs: { samples: ['13', 0], mask: ['12', 0] } },
    '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['14', 0], seed: seedArg, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
    '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
    '15': { class_type: 'ImageCompositeMasked', inputs: { destination: ['4', 0], source: ['8', 0], x: 0, y: 0, resize_source: false, mask: ['12', 0] } },
    '9': { class_type: 'SaveImage', inputs: { images: ['15', 0], filename_prefix: `dog-${name}` } }
};

const outDir = path.join(outRoot, name);
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'workflow.json'), JSON.stringify({ server, resolution, seed: seedArg, inputs: { plate: plateName, mask: maskName, plateSha256: sha(plate), maskSha256: sha(mask) }, graph }, null, 2));

const { prompt_id: id } = await (await api('/prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) })).json();
fs.writeFileSync(path.join(outDir, 'submission.json'), JSON.stringify({ id }));
console.log('Queued', name, id);

let job = null;
for (let i = 0; i < 90; i++) {
    await new Promise(r => setTimeout(r, 3000));
    const h = await (await api('/history/' + id)).json();
    job = h[id];
    if (job) break;
}
if (!job) throw Error('ComfyUI job not finished in 270s');
fs.writeFileSync(path.join(outDir, 'history.json'), JSON.stringify(job, null, 2));
const image = job.outputs?.['9']?.images?.[0];
if (!image) throw Error(JSON.stringify(job.status).slice(0, 300));

const bytes = Buffer.from(await (await api('/view?' + new URLSearchParams(image))).arrayBuffer());
fs.writeFileSync(path.join(outDir, 'raw.png'), bytes);

// 回贴到 512 底稿：按原 512 尺寸合成，便于直接比对。
const base512 = await sharp(path.join(inputDir, target.plate)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const result512 = await sharp(bytes).resize(512, 512, { kernel: 'lanczos3' }).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const mask512 = await sharp(path.join(inputDir, target.mask)).resize(512, 512, { kernel: 'nearest' }).ensureAlpha().raw().toBuffer();
const composed = Buffer.alloc(512 * 512 * 4);
for (let i = 0; i < composed.length; i += 4) {
    const m = mask512[i] > 127 ? 1 : 0;
    for (let k = 0; k < 4; k++) composed[i + k] = m ? result512.data[i + k] : base512.data[i + k];
}
await sharp(composed, { raw: { width: 512, height: 512, channels: 4 } }).png().toFile(path.join(outDir, 'composited-512.png'));
console.log(JSON.stringify({ name, id, rawBytes: bytes.length }));
