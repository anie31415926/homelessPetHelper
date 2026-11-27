// 给 ComfyUI 准备输入：512 干净底稿 + 三张补绘 mask（白=重绘，黑=保留）。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const root = path.resolve(import.meta.dirname, '..');
const sheet = path.join(root, 'unity/HomelessPetHelper/Assets/Art/PetSit/T_Station_ShibaMotion.png');
const outDir = path.join(root, 'art-source/dog-rig-prototype-20261127/comfy-input');
fs.mkdirSync(outDir, { recursive: true });
const size = 512;
const scale = size / 504;   // layer-plan 标注图是 504 画布，这里统一换算

// 与 layer-plan 同一套框，按 512 画布换算，四周各外扩 8px 给 inpaint 留融合带。
const masks = [
    { name: 'mask-neck-back', box: [186, 150, 62, 92] },
    { name: 'mask-belly', box: [196, 286, 122, 104] },
    { name: 'mask-hip-back', box: [318, 148, 58, 112] }
].map(m => {
    const [x, y, w, h] = m.box.map(v => Math.round(v * scale));
    return { ...m, rect: [x - 8, y - 8, w + 16, h + 16] };
});

await sharp(sheet).extract({ left: 4, top: 2, width: 126, height: 126 })
    .resize(size, size, { kernel: 'lanczos3' }).png().toFile(path.join(outDir, 'plate-512.png'));

for (const mask of masks) {
    const [x, y, w, h] = mask.rect;
    const svg = `<svg width="${size}" height="${size}" xmlns="http://www.w3.org/2000/svg">
<rect width="${size}" height="${size}" fill="#000"/>
<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#fff"/>
</svg>`;
    await sharp(Buffer.from(svg)).png().toFile(path.join(outDir, mask.name + '.png'));
}

// 预览：底稿上叠半透明红色标出三块重绘区，给美术/自己核对位置。
const overlay = masks.map(m => {
    const [x, y, w, h] = m.rect;
    return `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#ff2d5544" stroke="#ff2d55" stroke-width="2"/>`;
}).join('');
await sharp(path.join(outDir, 'plate-512.png'))
    .composite([{ input: Buffer.from(`<svg width="${size}" height="${size}">${overlay}</svg>`) }])
    .png().toFile(path.join(outDir, 'masks-preview.png'));

fs.writeFileSync(path.join(outDir, 'README.md'), [
    '# ComfyUI 输入',
    '',
    '- plate-512.png：512×512 干净底稿，来自 T_Station_ShibaMotion 第 0 帧（126px lanczos 放大），透明底。所有重绘都以它为基准，不要另生成底稿。',
    '- mask-neck-back.png / mask-belly.png / mask-hip-back.png：补绘用的 inpaint mask，白=重绘，黑=保留，已在标注框外扩 8px。',
    '- masks-preview.png：三块 mask 位置核对图（红框）。',
    '',
    '用法：ComfyUI 里 mask 节点接 GrowMask 6–8px + MaskBlur 6，denoise 0.45–0.6，尺寸严格 512×512，别开高分修复（会改尺度）。',
    ''
].join('\n'));
console.log(JSON.stringify({ size, masks: masks.map(m => [m.name, m.rect]) }));
