// 生成遮挡用毛皮贴片：Qwen-Image 2.1 文生图 → 裁中心 → 键白取 alpha → 512 透明底贴片。
// 用法：node tools/dog-comfy-patch.mjs <neck|belly|hip> [--seed N]
import fs from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import sharp from 'sharp';

const server = 'http://127.0.0.1:8188';
const root = path.resolve(import.meta.dirname, '..');
const outRoot = path.join(root, 'art-source/dog-rig-prototype-20261127/patches');

const targets = {
    neck: {
        seed: 2026112711,
        prompt: '柴犬颈后与肩胛部位的暖橙色水彩被毛近景，画面被毛皮铺满，手绘水彩笔触、细褐色毛边与自然的毛流分束，奶油白与蜜桃橙过渡。不要出现头、眼睛、耳朵、腿、爪、尾巴，不要整只动物，不要背景元素或文字。'
    },
    belly: {
        seed: 2026112712,
        prompt: '柴犬胸腹下方与两前腿之间的被毛近景：奶油白胸毛向下过渡到暖橙色腹部，手绘水彩笔触、细褐色毛边、柔软蓬松的毛束。不要出现头、脸、眼睛、爪子、尾巴，不要整只动物，不要背景或文字。'
    },
    hip: {
        seed: 2026112713,
        prompt: '柴犬臀部与尾根后方的被毛近景：圆形臀部的暖橙色水彩毛块，毛流从背线向下过渡，手绘水彩笔触与细褐色轮廓。不要出现完整尾巴、头、脸、腿、爪，不要整只动物，不要背景或文字。'
    }
};
const name = process.argv[2];
if (!targets[name]) throw Error('Supply one of: ' + Object.keys(targets[name] ? [] : Object.keys(targets)).join(', '));
const target = targets[name];
const seed = process.argv.includes('--seed') ? Number(process.argv[process.argv.indexOf('--seed') + 1]) : target.seed;
const negative = '整只狗，全身像，头部，脸，眼睛，耳朵，腿，爪子，尾巴，地面，背景，文字，水印，写实照片，3D渲染，厚涂油画，深色描边';

const styleRef = process.argv.includes('--ref');
const platePath = path.join(root, 'art-source/dog-rig-prototype-20261127/comfy-input/plate-512.png');
const api = async (p, init) => {
    const r = await fetch(server + p, init);
    if (!r.ok) throw Error((await r.text()).slice(0, 400));
    return r;
};
const json = v => ({ method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(v) });
let refName = null;
if (styleRef) {
    const form = new FormData();
    form.append('image', new Blob([await sharp(platePath).png().toBuffer()]), `dog-patch-style-${name}.png`);
    form.append('overwrite', 'true');
    refName = (await (await api('/upload/image', { method: 'POST', body: form })).json()).name;
}

const graph = {
    '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
    '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
    '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt: target.prompt
            + (refName ? ' 参考图只用于学习画风：儿童绘本手绘水彩、块状色斑与细褐色铅笔边，不要复制参考图中的柴犬、头部、四肢或背景。' : ''),
        negative_prompt: negative + '，照片级毛发，细致毛丝，真实摄影纹理', resolution: 768,
        ...(refName ? { 'images.image_1': ['10', 0] } : {}) } },
    ...(refName ? { '10': { class_type: 'LoadImage', inputs: { image: refName } } } : {}),
    '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2], seed, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
    '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
    '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `dog-patch-${name}` } }
};

const outDir = path.join(outRoot, name);
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'workflow.json'), JSON.stringify({ server, seed, prompt: target.prompt, negative, graph }, null, 2));
const { prompt_id: id } = await (await api('/prompt', json({ prompt: graph }))).json();
fs.writeFileSync(path.join(outDir, 'submission.json'), JSON.stringify({ id }));
console.log('Queued', name, id);

let job = null;
for (let i = 0; i < 80; i++) {
    await new Promise(r => setTimeout(r, 3000));
    job = (await (await api('/history/' + id)).json())[id];
    if (job) break;
}
if (!job) throw Error('job timeout');
fs.writeFileSync(path.join(outDir, 'history.json'), JSON.stringify(job, null, 2));
const image = job.outputs?.['9']?.images?.[0];
if (!image) throw Error(JSON.stringify(job.status).slice(0, 300));
const bytes = Buffer.from(await (await api('/view?' + new URLSearchParams(image))).arrayBuffer());
fs.writeFileSync(path.join(outDir, 'raw-768.png'), bytes);

// 裁中心 512（保留笔触细节），再按“边缘连通的白”键出透明，最后羽化边缘。
const { data, info } = await sharp(bytes).extract({ left: 128, top: 128, width: 512, height: 512 })
    .removeAlpha().raw().toBuffer({ resolveWithObject: true });
const w = info.width, h = info.height, alpha = new Uint8Array(w * h).fill(255);
const isPaper = i => data[i * 3] > 238 && data[i * 3 + 1] > 238 && data[i * 3 + 2] > 232;
const queue = new Int32Array(w * h);
let end = 0;
const push = i => { if (alpha[i] === 255 && isPaper(i)) { alpha[i] = 0; queue[end++] = i; } };
for (let x = 0; x < w; x++) { push(x); push((h - 1) * w + x); }
for (let y = 0; y < h; y++) { push(y * w); push(y * w + w - 1); }
for (let c = 0; c < end; c++) {
    const i = queue[c], x = i % w;
    if (x > 0) push(i - 1);
    if (x < w - 1) push(i + 1);
    if (i >= w) push(i - w);
    if (i < w * (h - 1)) push(i + w);
}
const rgba = Buffer.alloc(w * h * 4);
for (let i = 0; i < w * h; i++) {
    rgba[i * 4] = data[i * 3]; rgba[i * 4 + 1] = data[i * 3 + 1]; rgba[i * 4 + 2] = data[i * 3 + 2];
    rgba[i * 4 + 3] = alpha[i];
}
const patch = await sharp(rgba, { raw: { width: w, height: h, channels: 4 } })
    .blur(0.6).png({ compressionLevel: 9 }).toBuffer();
await sharp(patch).toFile(path.join(outDir, 'patch-512.png'));

// 白底预览，方便直接看形状。
await sharp(patch).flatten({ background: '#ffffff' }).toFile(path.join(outDir, 'patch-preview.png'));
console.log(JSON.stringify({ name, id, sha256: createHash('sha256').update(patch).digest('hex').slice(0, 16) }));
