// 逆塔防情报小图标：统一 192×192 单色线稿，供 16–24L 显示。
// 生成两份：ink（主文字色 #2A3D30）与 white（运行时由 UI tint 染色）。
import {mkdir, writeFile} from 'node:fs/promises';
import sharp from 'sharp';

const OUT = 'art-source/siege-codex-20261126/icons';
const INK = '#2A3D30';
const canvas = 192;
const stroke = 10;

const svg = (body, color) => `<svg xmlns="http://www.w3.org/2000/svg" width="${canvas}" height="${canvas}" viewBox="0 0 192 192">
  <g fill="none" stroke="${color}" stroke-width="${stroke}" stroke-linecap="round" stroke-linejoin="round">${body}</g>
</svg>`;

// 每个图标都填满中央 150×150 的可读区，四周留 21 的安全边。
const icons = {
    cost: `
    <path d="M44 100h104l-12 44c-2 8-8 12-16 12H72c-8 0-14-4-16-12z"/>
    <path d="M54 100c0-18 18-30 42-30s42 12 42 30"/>
    <path d="M70 92c6-6 14-8 22-8M98 84c8 0 16 2 22 8" stroke-width="7"/>
    <path d="M78 128h36" stroke-width="7"/>`,
    durability: `
    <path d="M96 38l46 20v38c0 34-22 54-46 66-24-12-46-32-46-66V58z"/>
    <path d="M96 76v34M80 93h32" stroke-width="7"/>`,
    power: `
    <ellipse cx="96" cy="118" rx="26" ry="21"/>
    <ellipse cx="62" cy="86" rx="13" ry="15"/>
    <ellipse cx="92" cy="72" rx="12" ry="14"/>
    <ellipse cx="124" cy="78" rx="12" ry="14"/>
    <ellipse cx="144" cy="104" rx="10" ry="12"/>`,
    attack: `
    <path d="M60 44h72M60 148h72"/>
    <path d="M66 44c0 30 26 34 30 52 4-18 30-22 30-52"/>
    <path d="M66 148c0-30 26-34 30-52 4 18 30 22 30 52"/>`,
    range: `
    <path d="M52 96h88"/>
    <path d="M64 84l-12 12 12 12"/>
    <path d="M128 84l12 12-12 12"/>`,
    speed: `
    <path d="M58 66l26 20-26 20"/>
    <path d="M92 66l26 20-26 20"/>
    <path d="M60 128h72" stroke-dasharray="18 12" stroke-width="7"/>`,
    delivery: `
    <path d="M62 150V44h68v106"/>
    <path d="M62 44c18 10 34-10 50 0 8 5 12 2 18-2" stroke-width="8"/>
    <circle cx="116" cy="112" r="7" stroke-width="6"/>`,
    merge: `
    <circle cx="52" cy="70" r="16"/>
    <circle cx="52" cy="126" r="16"/>
    <path d="M74 98h20"/>
    <path d="M86 88l12 10-12 10" stroke-width="8"/>
    <circle cx="136" cy="98" r="26" stroke-width="12"/>`,
};

const preview = async files => {
    const pad = 16;
    const cell = 128;
    const width = files.length * cell + pad * 2;
    const height = cell + pad * 2;
    const resized = await Promise.all(files.map(file => sharp(file.buffer).resize(96, 96).png().toBuffer()));
    const layers = resized.map((buffer, i) => ({
        input: buffer,
        left: pad + i * cell + (cell - 96) / 2,
        top: pad + (cell - 96) / 2,
    }));
    return sharp({create: {width, height, channels: 4, background: {r: 253, g: 248, b: 233, alpha: 1}}})
        .composite(layers).png().toBuffer();
};

await mkdir(OUT, {recursive: true});
const order = Object.keys(icons);
const buffers = [];
for (const name of order) {
    const buffer = await sharp(Buffer.from(svg(icons[name], INK))).png().toBuffer();
    await writeFile(`${OUT}/${name}-ink.png`, buffer);
    buffers.push({name, buffer});
}
await writeFile(`${OUT}/preview.png`, await preview(buffers));
await writeFile(`${OUT}/README.md`, `# 逆塔防情报小图标

192×192 单色线稿（stroke ${stroke}px），显示档 16–24L，颜色 #2A3D30（纸底深字）。
每个图形都限制在中央 150×150 内，四周 21px 安全边。由 make-icons.mjs 生成，改形状请改脚本后重跑，不要手改 PNG。
不使用单独的白色版本：当前只用在奶油纸底，深色字直接用 ink 版。
`);
console.log(order.join(', '));
