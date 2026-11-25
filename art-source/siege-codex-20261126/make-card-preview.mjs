// 情报面板版式预览 v2（形容词属性 + 描述 + 费用图标 + 属性说明区）。
import {readFile} from 'node:fs/promises';
import sharp from 'sharp';

const iconDir = 'art-source/siege-codex-20261126/icons';
const units = 'art-source/treehouse-field-20261125/units-clean';
const mech = 'art-source/treehouse-field-20261125/mechanisms';
const W = 780;
const H = 1080;
const PAPER = '#FFFDF6';
const INK = '#2A3D30';
const SUB = '#384D41';
const WARN = '#8E4D30';
const FONT = 'Microsoft YaHei';

const data = async file => 'data:image/png;base64,' + (await readFile(file)).toString('base64');
const body = [];
const text = [];
const line = (x, y, size, color, content, weight = 400, anchor = 'start') =>
    text.push(`<text x="${x}" y="${y}" font-size="${size}" font-weight="${weight}" fill="${color}" text-anchor="${anchor}">${content}</text>`);
const image = async (file, x, y, size) =>
    body.push(`<image x="${x}" y="${y}" width="${size}" height="${size}" preserveAspectRatio="xMidYMid meet" href="${await data(file)}"/>`);

// 头部
line(44, 62, 40, INK, '道具与机关', 700);
line(44, 104, 23, SUB, '送到它面前，信任满 70 就能带回站。');
body.push(`<rect x="44" y="128" width="240" height="48" rx="24" fill="#D1EDC9"/>`);
body.push(`<rect x="296" y="128" width="240" height="48" rx="24" fill="#FFE3C9"/>`);
line(164, 162, 25, INK, '我的道具', 700, 'middle');
line(416, 162, 25, INK, '对方防线', 400, 'middle');

// 卡 A：我方
const a = 200;
body.push(`<rect x="44" y="${a}" width="692" height="352" rx="16" fill="${PAPER}" stroke="#E8DFC8" stroke-width="2"/>`);
await image(`${units}/fish.png`, 64, a + 32, 132);
line(232, a + 62, 34, INK, '小鱼干', 700);
await image(`${iconDir}/cost-ink.png`, 664, a + 38, 30);
line(704, a + 62, 26, SUB, '2', 700);

const attrsA = [
    ['durability', '耐久', '耐打'], ['power', '威力', '一般'], ['attack', '出手', '快'],
];
for (let i = 0; i < attrsA.length; i++) {
    const [icon, label, value] = attrsA[i];
    await image(`${iconDir}/${icon}-ink.png`, 232 + i * 150, a + 86, 26);
    line(232 + i * 150 + 34, a + 108, 22, SUB, `${label} ${value}`);
}
const attrsB = [['range', '射程', '一般'], ['speed', '步速', '慢'], ['delivery', '抵达', '多']];
for (let i = 0; i < attrsB.length; i++) {
    const [icon, label, value] = attrsB[i];
    await image(`${iconDir}/${icon}-ink.png`, 232 + i * 150, a + 126, 26);
    line(232 + i * 150 + 34, a + 148, 22, SUB, `${label} ${value}`);
}
line(232, a + 194, 23, INK, '描述：猫记食品厂出品，东海小鱼十八道慢烘，');
line(232, a + 228, 23, INK, '没有一只猫能拒绝它的味道。');
line(232, a + 272, 23, SUB, '合体：三条小鱼干可以合体，能力全面增强。');
line(232, a + 312, 21, SUB, '合出来是「鱼干花束」。');

// 卡 B：对方机关
const b = 572;
body.push(`<rect x="44" y="${b}" width="692" height="300" rx="16" fill="${PAPER}" stroke="#E8DFC8" stroke-width="2"/>`);
await image(`${mech}/tower-scratch.png`, 64, b + 30, 126);
line(232, b + 58, 34, INK, '抓板拍爪台', 700);
line(700, b + 58, 24, SUB, '对方机关', 400, 'end');
line(232, b + 104, 22, SUB, '耐久 结实 · 射程 远 · 出手 慢');
line(232, b + 150, 23, INK, '描述：猫抓板底下藏了弹簧，影子先亮，');
line(232, b + 182, 23, INK, '爪子后到，专挑站得最稳的那个拍。');
line(232, b + 226, 22, WARN, '应对：看到影子先撤人；拆掉它就没这一巴掌了。');
line(232, b + 266, 21, SUB, '同类：拖鞋跷跷板（狗狗阵地）。');

// 说明区
line(44, 928, 26, INK, '这些词是什么意思', 700);
const legend = [
    ['durability', '耐久', '能挨几下才倒'],
    ['power', '威力', '一下能打掉多少'],
    ['attack', '出手', '两下之间隔多久'],
    ['range', '射程', '站多远就能打到'],
    ['speed', '步速', '跑得多快'],
    ['delivery', '抵达', '送到它面前加多少信任'],
];
for (let i = 0; i < legend.length; i++) {
    const [icon, label, desc] = legend[i];
    const x = 44 + (i % 2) * 350;
    const y = 960 + Math.floor(i / 2) * 38;
    await image(`${iconDir}/${icon}-ink.png`, x, y - 22, 24);
    line(x + 32, y, 20, SUB, `${label}　${desc}`);
}

const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" font-family="${FONT}">
  <rect width="${W}" height="${H}" fill="#FDF8E9"/>
  ${body.join('')}
  ${text.join('')}
</svg>`;

await sharp(Buffer.from(svg)).png().toFile('art-source/siege-codex-20261126/card-preview.png');
console.log('ok');
