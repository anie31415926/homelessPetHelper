// 生成“拆层与补绘”标注图：第 0 帧放大 4× 后叠分区与遮挡补绘区域。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const root = path.resolve(import.meta.dirname, '..');
const sheet = path.join(root, 'unity/HomelessPetHelper/Assets/Art/PetSit/T_Station_ShibaMotion.png');
const out = path.join(root, 'art-source/dog-rig-prototype-20261127/layer-plan.png');
const size = 504;

const layers = [
    { name: '头 head', box: [20, 12, 208, 208], fill: '#ff9f4333', stroke: '#e07b1a', anchor: 'top' },
    { name: '躯干 body', box: [180, 108, 190, 250], fill: '#54a0ff33', stroke: '#2f6fbf', anchor: 'top' },
    { name: '前腿×2', box: [126, 292, 80, 170], fill: '#2ed57333', stroke: '#1e9c53', anchor: 'bottom' },
    { name: '后腿×2', box: [302, 284, 96, 178], fill: '#26de8133', stroke: '#14915a', anchor: 'bottom' },
    { name: '卷尾 tail', box: [340, 104, 136, 150], fill: '#c56cf033', stroke: '#8e35c0', anchor: 'top' }
];
const occlusions = [
    { name: '补1 头后颈', box: [186, 150, 62, 92] },
    { name: '补2 腿后腹', box: [196, 286, 122, 104] },
    { name: '补3 尾后躯干', box: [318, 148, 58, 112] }
];

const labelStyle = (color, sizePx) =>
    `font-family="Microsoft YaHei, SimHei, sans-serif" font-size="${sizePx}" fill="${color}" stroke="#ffffff" stroke-width="3" paint-order="stroke"`;

const rects = layers.map(l => {
    const [x, y, w, h] = l.box;
    const ty = l.anchor === 'top' ? y + 24 : y + h - 10;
    return `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${l.fill}" stroke="${l.stroke}" stroke-width="2"/>
<text x="${x + 6}" y="${ty}" ${labelStyle(l.stroke, 18)}>${l.name}</text>`;
}).join('\n');
const dashed = occlusions.map(o => {
    const [x, y, w, h] = o.box;
    return `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#ffffff22" stroke="#d63031" stroke-width="2" stroke-dasharray="8 6"/>
<text x="${x + 4}" y="${y + 18}" ${labelStyle('#b02525', 15)}>${o.name}</text>`;
}).join('\n');

const svg = `<svg width="${size}" height="${size}" xmlns="http://www.w3.org/2000/svg">
${rects}
${dashed}
</svg>`;

const base = await sharp(sheet).extract({ left: 4, top: 2, width: 126, height: 126 })
    .resize(size, size, { kernel: 'lanczos3' }).png().toBuffer();
await sharp(base).composite([{ input: Buffer.from(svg), left: 0, top: 0 }]).png().toFile(out);
console.log(JSON.stringify({ out, layers: layers.length, occlusions: occlusions.length }));
