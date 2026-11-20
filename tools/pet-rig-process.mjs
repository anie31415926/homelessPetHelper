import { mkdir, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { composeAtlas } from './lib/image-atlas.mjs';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const root = 'art-source/pet-rig';
await mkdir('public/pet-rig', { recursive: true });

// The model ignored the alpha request. Remove only near-white pixels connected
// to the crop boundary, preserving the enclosed white coat and eye highlights.
async function cut(source, rect, shape) {
  const { data, info } = await sharp(`${root}/${source}-master.png`).extract(rect).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h } = info;
  const seen = new Uint8Array(w * h), queue = new Int32Array(w * h);
  let read = 0, end = 0;
  const add = i => {
    if (seen[i]) return;
    const p = i * 4;
    if (Math.min(data[p], data[p + 1], data[p + 2]) < 243) return;
    seen[i] = 1; queue[end++] = i;
  };
  for (let x = 0; x < w; x++) { add(x); add((h - 1) * w + x); }
  for (let y = 0; y < h; y++) { add(y * w); add(y * w + w - 1); }
  while (read < end) {
    const i = queue[read++], x = i % w, y = Math.floor(i / w);
    if (x) add(i - 1); if (x + 1 < w) add(i + 1);
    if (y) add(i - w); if (y + 1 < h) add(i + w);
  }
  for (let i = 0; i < seen.length; i++) if (seen[i]) data[i * 4 + 3] = 0;
  let result = sharp(data, { raw: { width: w, height: h, channels: 4 } });
  if (shape) result = result.composite([{ input: Buffer.from(`<svg width="${w}" height="${h}"><path d="${shape}" fill="white"/></svg>`), blend: 'dest-in' }]);
  return result.png().toBuffer();
}
const definitions = {
  front: [
    ['head', 'front', { left: 60, top: 65, width: 310, height: 192 }, 'M38 43 Q155 -5 264 43 Q290 87 285 134 Q255 186 154 190 Q54 186 28 139 Q15 85 38 43Z'],
    ['body', 'front-body', { left: 483, top: 344, width: 314, height: 355 }, 'M70 0 L250 0 Q314 94 295 227 Q282 348 157 353 Q25 346 18 227 Q0 95 70 0Z'],
    ['tail', 'front', { left: 879, top: 72, width: 369, height: 189 }],
    ['earL', 'front', { left: 123, top: 320, width: 140, height: 167 }],
    ['earR', 'front', { left: 628, top: 321, width: 122, height: 160 }],
    ['frontL', 'front', { left: 505, top: 605, width: 120, height: 190 }],
    ['frontR', 'front', { left: 643, top: 608, width: 120, height: 190 }],
    ['backL', 'front', { left: 505, top: 605, width: 120, height: 190 }],
    ['backR', 'front', { left: 643, top: 608, width: 120, height: 190 }],
  ],
  side: [
    ['head', 'side', { left: 150, top: 319, width: 159, height: 144 }, 'M14 54 Q45 29 101 36 Q145 42 150 91 Q140 133 88 136 Q30 137 7 97Z'],
    ['body', 'side-body', { left: 271, top: 338, width: 629, height: 288 }, 'M45 5 Q125 -5 226 18 Q450 -2 554 89 Q628 157 611 272 Q560 280 510 224 Q399 226 340 236 Q192 244 88 212 Q7 174 8 91Z'],
    ['tail', 'front', { left: 879, top: 72, width: 369, height: 189 }],
    ['earL', 'front', { left: 123, top: 320, width: 140, height: 167 }],
    ['earR', 'front', { left: 628, top: 321, width: 122, height: 160 }],
    ['frontL', 'front', { left: 505, top: 605, width: 120, height: 190 }],
    ['frontR', 'front', { left: 643, top: 608, width: 120, height: 190 }],
    ['backL', 'front', { left: 505, top: 605, width: 120, height: 190 }],
    ['backR', 'front', { left: 643, top: 608, width: 120, height: 190 }],
  ],
};
const index = { size: 768, cell: 256, views: {} };
for (const [view, parts] of Object.entries(definitions)) {
  const regions = [], images = new Map();
  index.views[view] = {};
  for (let i = 0; i < parts.length; i++) {
    const [name, source, rect, shape] = parts[i];
    const buffer = await sharp(await cut(source, rect, shape)).trim({ threshold: 5 }).resize(224, 224, { fit: 'inside' }).png().toBuffer();
    const { width, height } = await sharp(buffer).metadata();
    const x = (i % 3) * 256 + Math.floor((256 - width) / 2), y = Math.floor(i / 3) * 256 + Math.floor((256 - height) / 2);
    regions.push({ id:name, x, y, width, height });
    images.set(name, buffer);
    index.views[view][name] = { x, y, width, height };
  }
  await writeFile(`public/pet-rig/${view}-atlas.png`, await composeAtlas(sharp,
    { width:768, height:768, regions }, images,
    { compressionLevel:9, palette:true, colours:256, dither:.5, effort:10 }));
}
await sharp(`${root}/turnaround-master.png`).resize(768).jpeg({ quality: 88 }).toFile('public/pet-rig/turnaround.jpg');
await writeFile('public/pet-rig/atlas.json', JSON.stringify(index, null, 2));
await writeFile(`${root}/derivation.json`, JSON.stringify({ method: 'Boundary-connected near-white flood removal (min RGB 243), explicit source crops and curved masks, crop trim, max 224px part, 768px atlas, 256-colour PNG dither 0.5. Opaque reference resized to 768 JPEG88. Front limb art and front ears reused in side view and scaled for this trial; not a production anatomical kit.', definitions, index }, null, 2));
for (const background of ['#f3f8f4', '#34433e']) {
  await sharp({ create: { width: 1536, height: 768, channels: 4, background } }).composite([
    { input: 'public/pet-rig/front-atlas.png', left: 0, top: 0 }, { input: 'public/pet-rig/side-atlas.png', left: 768, top: 0 },
  ]).png().toFile(`${root}/edge-${background === '#f3f8f4' ? 'light' : 'dark'}.png`);
}
console.log('Created two 768px atlases and derivation record.');
