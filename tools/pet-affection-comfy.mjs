import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import sharp from 'sharp';

const address = 'http://127.0.0.1:8188';
const output = new URL('../art-source/pet-affection-20261127/', import.meta.url);
const reference = new URL('../art-source/pet-assets-wx-20261127/clean/柴犬/宠物游戏操作界面背景设计 (26)-clean.png', import.meta.url);
const seed = 2026112717;
const resolution = 512;
const request = async (endpoint, init) => {
    const response = await fetch(address + endpoint, { ...init, signal: AbortSignal.timeout(30000) });
    if (!response.ok) throw Error(`${response.status}: ${(await response.text()).slice(0, 500)}`);
    return response;
};
async function upload(buffer, name) {
    const form = new FormData();
    form.append('image', new Blob([buffer]), name);
    return (await (await request('/upload/image', { method: 'POST', body: form })).json()).name;
}

const plate = await sharp(await readFile(reference)).resize(resolution, resolution).png().toBuffer();
const mask = await sharp(Buffer.from('<svg width="512" height="512"><rect width="512" height="512" fill="black"/><ellipse cx="198" cy="176" rx="89" ry="66" fill="white"/></svg>')).png().toBuffer();
const baseName = await upload(plate, `pet-affection-${seed}-base.png`);
const maskName = await upload(mask, `pet-affection-${seed}-mask.png`);
const node = (class_type, inputs) => ({ class_type, inputs });
const graph = {
    1: node('UNETLoader', { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' }),
    2: node('CLIPLoader', { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' }),
    3: node('VAELoader', { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' }),
    4: node('LoadImage', { image: baseName }),
    5: node('TextEncodeQwenImage21', {
        clip: ['2', 0], resolution, 'images.image_1': ['4', 0],
        prompt: '只修改同一只水彩柴犬的眼睛、嘴和脸颊。被主人轻轻摸头时，双眼笑成弯弯的小月牙，嘴角自然扬起，轻微腮红，嘴巴微张露一点舌尖。保持鼻子位置、原来的脸宽和奶油白口鼻毛边，耳朵身体脚和尾巴、相机、纸纹、光线不变。不画手、特效或文字。',
        negative_prompt: '手，花，文字，水印，额外肢体，变换视角，卡通粗描边，3D，照片，夸张大嘴，露齿，脸型变化',
    }),
    6: node('ModelSamplingAuraFlow', { model: ['1', 0], shift: 3.1 }),
    7: node('KSampler', {
        model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['14', 0],
        seed, steps: 20, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: .78,
    }),
    8: node('VAEDecode', { samples: ['7', 0], vae: ['3', 0] }),
    9: node('SaveImage', { images: ['15', 0], filename_prefix: `pet-affection-${seed}` }),
    11: node('LoadImage', { image: maskName }),
    12: node('ImageToMask', { image: ['11', 0], channel: 'red' }),
    13: node('VAEEncode', { pixels: ['4', 0], vae: ['3', 0] }),
    14: node('SetLatentNoiseMask', { samples: ['13', 0], mask: ['12', 0] }),
    15: node('ImageCompositeMasked', { destination: ['4', 0], source: ['8', 0], x: 0, y: 0, resize_source: false, mask: ['12', 0] }),
};

await mkdir(output, { recursive: true });
await writeFile(new URL('workflow.json', output), JSON.stringify({
    resolution, seed, inputSha256: createHash('sha256').update(plate).digest('hex'), graph,
}, null, 2));
const { prompt_id: id } = await (await request('/prompt', {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }),
})).json();
console.log('Queued:', id);
const deadline = Date.now() + 300000;
while (Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, 3000));
    const result = (await (await request(`/history/${id}`)).json())[id];
    if (!result) continue;
    await writeFile(new URL('history.json', output), JSON.stringify(result, null, 2));
    const entry = result.outputs?.['9']?.images?.[0];
    if (!entry) throw Error(JSON.stringify(result.status).slice(0, 800));
    const image = await request('/view?' + new URLSearchParams(entry));
    await writeFile(new URL('comfy-alternate.png', output), Buffer.from(await image.arrayBuffer()));
    console.log('Saved candidate:', id);
    process.exit(0);
}
throw Error(`Local ComfyUI job still unresolved: ${id}`);
