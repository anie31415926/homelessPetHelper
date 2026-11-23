import {createHash} from 'node:crypto';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import sharp from 'sharp';
import {packAtlasRegions, composeAtlas} from './lib/image-atlas.mjs';

const base = 'art-source/pet-sit-chibi-20261123', output = 'public/pet-sit-chibi', check = process.argv.includes('--check');
const plan = JSON.parse(await readFile(`${base}/plan.json`, 'utf8')), hash = bytes => createHash('sha256').update(bytes).digest('hex');
if (hash(await readFile(plan.source)) !== plan.sourceSha256) throw new Error('Original identity changed; review plan before rebuilding');
for (const [path, expected] of Object.entries(plan.donorHashes)) if (hash(await readFile(path)) !== expected) throw new Error(`Donor changed; review plan before rebuilding: ${path}`);
const old = JSON.parse(await readFile('public/pet-sit-skin/atlas.json', 'utf8')), images = new Map(), layers = [];
for (const id of ['tail', 'nearHindPaw', 'nearFront', 'nearHindThigh', 'shaft']) {
    const layer = old.layers.find(layer => layer.id === id);
    let png = await sharp('public/pet-sit-skin/atlas.png').extract({left: layer.atlasX, top: layer.atlasY, width: layer.width, height: layer.height}).png().toBuffer();
    if (id === 'nearFront' || id === 'nearHindThigh') {
        const pixels = await sharp(png).ensureAlpha().raw().toBuffer(), start = id === 'nearFront' ? 325 : 330, end = id === 'nearFront' ? 392 : 385;
        for (let y = 0; y < layer.height; y++) for (let x = 0; x < layer.width; x++) {
            const t = Math.max(0, Math.min(1, (y + layer.top - start) / (end - start))), p = (y * layer.width + x) * 4;
            pixels[p + 3] = Math.round(pixels[p + 3] * t * t * (3 - 2 * t));
        }
        png = await sharp(pixels, {raw: {width: layer.width, height: layer.height, channels: 4}}).png().toBuffer();
    }
    images.set(id, png);
    layers.push({...layer, atlasX: undefined, atlasY: undefined});
}
const original = await sharp(plan.source).ensureAlpha().raw().toBuffer(), width = 452, height = 512;
{
    const layer = {id: 'hindLower', left: 247, top: 408, width: 111, height: 73};
    const data = await sharp('art-source/pet-limbs/donor/hind-source.png').extract({left: layer.left, top: layer.top, width: layer.width, height: layer.height}).ensureAlpha().raw().toBuffer();
    for (let y = 0; y < layer.height; y++) for (let x = 0; x < layer.width; x++) {
        const sy = y + layer.top, top = Math.max(0, Math.min(1, (sy - 408) / 20)), bottom = Math.max(0, Math.min(1, (481 - sy) / 14));
        data[(y * layer.width + x) * 4 + 3] = Math.round(data[(y * layer.width + x) * 4 + 3] * top * top * (3 - 2 * top) * bottom * bottom * (3 - 2 * bottom));
    }
    images.set(layer.id, await sharp(data, {raw: {width: layer.width, height: layer.height, channels: 4}}).png().toBuffer()); layers.push(layer);
}
const masks = {
    head: 'M0 0 H452 V244 H240 L226 269 L224 290 L92 320 L86 342 H0Z',
    torso: 'M86 289 Q168 286 218 277 Q320 272 364 318 Q394 350 393 390 Q370 424 319 432 Q231 451 166 414 Q105 380 86 289Z',
};
for (const [id, path] of Object.entries(masks)) {
    const mask = await sharp(Buffer.from(`<svg width="452" height="512"><path fill="white" d="${path}"/></svg>`)).ensureAlpha().raw().toBuffer();
    const pixels = Buffer.from(original); let left = width, top = height, right = 0, bottom = 0;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const p = (y * width + x) * 4; pixels[p + 3] = Math.round(pixels[p + 3] * mask[p + 3] / 255);
        if (pixels[p + 3]) {left = Math.min(left, x); top = Math.min(top, y); right = Math.max(right, x); bottom = Math.max(bottom, y);}
    }
    const crop = {left: Math.max(0, left - 2), top: Math.max(0, top - 2)}; crop.width = Math.min(width - 1, right + 2) - crop.left + 1; crop.height = Math.min(height - 1, bottom + 2) - crop.top + 1;
    images.set(id, await sharp(pixels, {raw: {width, height, channels: 4}}).extract(crop).png().toBuffer()); layers.push({id, ...crop});
}
for (const layer of layers) {
    const pixels = await sharp(images.get(layer.id)).ensureAlpha().raw().toBuffer(); layer.support = [];
    for (let y = 0; y < layer.height; y += 2) {
        const row = []; for (let x = 0; x < layer.width; x++) if (pixels[(y * layer.width + x) * 4 + 3] > 96) row.push(x);
        if (row.length) for (const x of [row[0], row.at(-1)]) layer.support.push([x + layer.left, y + layer.top]);
    }
}
const layout = packAtlasRegions(layers.map(({id, width, height}) => ({id, width: width + 4, height: height + 4})), {maxSize: 1024}), padded = new Map();
for (const [id, png] of images) padded.set(id, await sharp(png).extend({top: 2, right: 2, bottom: 2, left: 2, background: {r: 0, g: 0, b: 0, alpha: 0}}).png().toBuffer());
const png = await composeAtlas(sharp, layout, padded, {compressionLevel: 9});
const inputs = await Promise.all([plan.source, ...Object.keys(plan.donorHashes)].map(async path => ({path, sha256: hash(await readFile(path))})));
const manifest = {schemaVersion: 1, width: layout.width, height: layout.height, source: plan.source, inputs,
    layers: layers.map(layer => {const region = layout.regions.find(region => region.id === layer.id); return {...layer, atlasX: region.x + 2, atlasY: region.y + 2};}), masks,
    method: 'Original head/scarf and torso pixels in original coordinates, existing native-sized limb/tail cutouts, same-source hidden shaft. Shorter fixed bone lengths; no pose crossfade or image elongation.'};
if (!check) await mkdir(output, {recursive: true});
for (const [path, bytes] of [[`${output}/atlas.png`, png], [`${output}/atlas.json`, Buffer.from(JSON.stringify(manifest, null, 2))], [`${base}/derivation.json`, Buffer.from(JSON.stringify({...manifest, atlasSha256: hash(png), bytes: png.length}, null, 2))]]) {
    if (check) {if (!(await readFile(path)).equals(bytes)) throw new Error(`Stale derived output: ${path}`);} else await writeFile(path, bytes);
}
console.log(`${check ? 'PASS' : 'BUILT'} ${layout.width}x${layout.height}, ${png.length} bytes, ${layers.length} source parts`);
