import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
import { packAtlasRegions, composeAtlas } from '../../tools/lib/image-atlas.mjs';

const base = 'art-source/station-room-20261123';
const width = 336, height = 600;
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
await fs.mkdir(`${base}/derived`, { recursive: true });
const original = await sharp(`${base}/reference.png`).extract({ left: 8, top: 9, width, height })
  .ensureAlpha().raw().toBuffer();
const clean = await sharp(`${base}/clean/master.png`).resize(width, height).ensureAlpha().raw().toBuffer();
const cleaned = Buffer.from(original);
// Keep every pixel outside the local watermark repair, rather than replacing the whole picture.
for (let y = 565; y < height; y++) for (let x = 260; x < width; x++) {
  const i = (y * width + x) * 4;
  const blend = Math.min(1, (x - 260) / 8, (y - 565) / 6);
  for (let c = 0; c < 3; c++) cleaned[i + c] = Math.round(original[i + c] * (1 - blend) + clean[i + c] * blend);
}
await sharp(cleaned, { raw: { width, height, channels: 4 } }).png().toFile(`${base}/cleaned.png`);
const empty = await sharp(`${base}/empty/master.png`).resize(width, height).flatten({ background: '#fff' })
  .png().toBuffer();
await fs.writeFile(`${base}/derived/background.png`, empty);
const definitions = [
  { id: 'tree', source: 'tree', region: [0, 75, 112, 265] },
  { id: 'plant', source: 'plant-local', region: [61, 225, 54, 78], local: true },
  { id: 'pillow', source: 'rear', region: [204, 221, 93, 95] },
  { id: 'cabinet', source: 'rear', region: [296, 188, 40, 134] },
  { id: 'bed', source: 'bed', region: [18, 298, 305, 174] },
];
const records = [], images = new Map(), pieces = new Map();
for (const def of definitions) {
  const sourceFile = `${base}/${def.source}/master.png`;
  const source = await fs.readFile(sourceFile);
  const rgba = def.local ? await sharp({ create: { width, height, channels: 4, background: '#0000' } })
    .composite([{ input: await sharp(source).resize(def.region[2], def.region[3]).png().toBuffer(),
      left: def.region[0], top: def.region[1] }]).raw().toBuffer() :
    await sharp(source).resize(width, height).ensureAlpha().raw().toBuffer();
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const i = (y * width + x) * 4;
    const [rx, ry, rw, rh] = def.region;
    if (x < rx || x >= rx + rw || y < ry || y >= ry + rh ||
      (def.id === 'tree' && x > 56 && y > 222 && y < 300)) rgba[i + 3] = 0;
    else if (rgba[i + 3] < 10) rgba[i + 3] = 0;
    else if (rgba[i + 3] > 247) rgba[i + 3] = 255;
    if (rgba[i + 3] === 0) rgba.fill(0, i, i + 3);
  }
  let left = width, top = height, right = 0, bottom = 0, opaque = 0;
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    if (rgba[(y * width + x) * 4 + 3] < 10) continue;
    left = Math.min(left, x); top = Math.min(top, y);
    right = Math.max(right, x); bottom = Math.max(bottom, y); opaque++;
  }
  if (opaque < 100) throw Error(`Empty furniture ${def.id}`);
  left = Math.max(0, left - 2); top = Math.max(0, top - 2);
  right = Math.min(width - 1, right + 2); bottom = Math.min(height - 1, bottom + 2);
  const crop = { left, top, width: right - left + 1, height: bottom - top + 1 };
  const piece = await sharp(rgba, { raw: { width, height, channels: 4 } }).extract(crop).png().toBuffer();
  await fs.writeFile(`${base}/derived/${def.id}.png`, piece);
  pieces.set(def.id, piece);
  images.set(def.id, await sharp(piece).extend({ top: 2, bottom: 2, left: 2, right: 2,
    background: '#0000' }).png().toBuffer());
  records.push({ id: def.id, source: sourceFile, sourceSha256: hash(source), ...crop, opaque,
    notes: 'ComfyUI RGBA alpha retained; native aspect restored to336x600, outside declared region cleared; alpha<10 cleared, >247 opaque. Tree floating foliage removed; complete plant separately extracted.' });
}
const layout = packAtlasRegions(records.map(r => ({ id: r.id, width: r.width + 4, height: r.height + 4 })),
  { maxSize: 512, alignment: 4 });
const atlas = await composeAtlas(sharp, layout, images, { compressionLevel: 9 });
await fs.writeFile(`${base}/T_StationRoom_Furniture.png`, atlas);
const jpg = await sharp(empty).jpeg({ quality: 90, mozjpeg: true }).toBuffer();
await fs.writeFile(`${base}/T_StationRoom_Background.jpg`, jpg);
const slices = layout.regions.map(r => {
  const record = records.find(item => item.id === r.id);
  return { id: r.id, x: r.x + 2, y: layout.height - r.y - 2 - record.height,
    width: record.width, height: record.height, left: record.left, top: record.top };
});
await fs.writeFile(`${base}/unity-import.json`, JSON.stringify({ width, height,
  atlasWidth: layout.width, atlasHeight: layout.height, slices }, null, 2) + '\n');
const assembled = await sharp(empty).composite(records.map(r => ({ input: pieces.get(r.id),
  left: r.left, top: r.top }))).png().toBuffer();
await fs.writeFile(`${base}/assembled.png`, assembled);
const panels = [];
for (const bg of ['#fff6ec', '#25313d']) {
  panels.push(await sharp(atlas).flatten({ background: bg }).png().toBuffer());
}
await sharp({ create: { width: layout.width * 2, height: layout.height, channels: 4, background: '#fff' } })
  .composite(panels.map((input, i) => ({ input, left: layout.width * i, top: 0 })))
  .png().toFile(`${base}/edge-review.png`);
await fs.writeFile(`${base}/derivation.json`, JSON.stringify({ sourceSha256: hash(await fs.readFile(`${base}/reference.png`)),
  crop: [8, 9, width, height], modelWorkingSize: [352, 608], watermarkRepair: { rect: [260, 565, 76, 35],
    outsideUnchanged: true }, records, atlas: { width: layout.width, height: layout.height, bytes: atlas.length,
    sha256: hash(atlas) }, background: { bytes: jpg.length, sha256: hash(jpg) }, assembledSha256: hash(assembled) }, null, 2) + '\n');
console.log(JSON.stringify({ records, atlasBytes: atlas.length, backgroundBytes: jpg.length }));
