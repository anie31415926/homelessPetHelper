import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const art = new URL('../../unity/HomelessPetHelper/Assets/Art/Loading/', import.meta.url);
const font = process.argv[2];
if (!font) throw new Error('Usage: node tools/wechat/build-cover.mjs <full Noto Sans SC font path>');

const width = 540;
const height = 960;
const compositionScale = .82;
const compositionCenterY = 440;
const title = '毛茸茸救助站';
const atlas = fileURLToPath(new URL('T_Loading_Elements.png', art));
const { sprites } = JSON.parse(await readFile(new URL('LoadingSlices.json', art), 'utf8'));

async function sprite(name, size, centerX, top) {
    const [left, atlasTop, spriteWidth, spriteHeight] = sprites.find(item => item.name === name).rect;
    const limit = Math.round(size * compositionScale);
    const pixels = await sharp(atlas).extract({left, top: atlasTop, width: spriteWidth, height: spriteHeight})
        .resize({width: limit, height: limit, fit: 'inside'}).png().toBuffer({resolveWithObject: true});
    return {input: pixels.data, left: Math.round(scaleAround(centerX, width / 2) - pixels.info.width / 2),
        top: Math.round(scaleAround(top, compositionCenterY))};
}

function scaleAround(value, center) {
    return center + (value - center) * compositionScale;
}

const lettering = await sharp({text: {text: `<span foreground="#36583D" weight="bold">${title}</span>`,
    font: `Noto Sans SC ${Math.round(58 * compositionScale)}`, fontfile: font, rgba: true}})
    .png().toBuffer({resolveWithObject: true});
const sign = Buffer.from(`<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg">
  <g transform="translate(${width / 2} ${compositionCenterY}) scale(${compositionScale}) translate(${-width / 2} ${-compositionCenterY})">
  <rect x="55" y="231" width="430" height="119" rx="39" fill="#fff9e8" fill-opacity=".89" stroke="#d6aa72" stroke-width="3"/>
  <path d="M185 375 Q270 384 355 375" stroke="#b9a16d" stroke-width="3" stroke-linecap="round" fill="none"/>
  </g>
</svg>`);
const cover = await sharp(fileURLToPath(new URL('T_Loading_Background.png', art))).resize(width, height)
    .composite([{input: sign}, {input: lettering.data, left: Math.round((width - lettering.info.width) / 2),
        top: Math.round(scaleAround(290, compositionCenterY) - lettering.info.height / 2)}, ...await Promise.all([
        sprite('House', 302, 270, 399), sprite('OrbitGinger', 134, 137, 598),
        sprite('DoorRight', 124, 405, 578), sprite('HeartUpper', 56, 270, 151),
        sprite('StarLeft', 43, 111, 407), sprite('StarRight', 39, 437, 436)
    ])]).jpeg({quality: 84, mozjpeg: true}).toFile(fileURLToPath(new URL('T_WeChat_Cover.jpg', art)));
console.log(`WeChat cover: ${cover.width}×${cover.height}, ${cover.size} bytes`);
