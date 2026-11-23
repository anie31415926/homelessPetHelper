import {createHash} from 'node:crypto';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import sharp from 'sharp';
import {packAtlasRegions, composeAtlas} from './lib/image-atlas.mjs';

const base = 'art-source/pet-sit-skin-20261123', out = 'public/pet-sit-skin', check = process.argv.includes('--check');
const manifest = JSON.parse(await readFile('public/pet-transition/atlas.json', 'utf8'));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const inputPaths = ['public/pet-transition/atlas.png', 'public/pet-transition/atlas.json', 'art-source/pet-limbs/donor/front-source.png', 'art-source/pet-limbs/donor/hind-source.png'];
const inputs = await Promise.all(inputPaths.map(async path => ({path, sha256: hash(await readFile(path))})));
const plan = JSON.parse(await readFile(`${base}/plan.json`, 'utf8'));
for (const input of inputs) if (plan.sourceHashes[input.path] !== input.sha256) throw new Error(`Source changed; review binding plan before rebuilding: ${input.path}`);
if (!check) await mkdir(out, {recursive: true});
const layers = [], images = new Map();
async function add(id, pixels, box) {
    images.set(id, pixels); layers.push({id, ...box});
}
for (const id of ['head', 'torso', 'tail', 'nearHindPaw']) {
    const layer = manifest.layers.find(layer => layer.id === id);
    let png = await sharp(inputPaths[0]).extract({left: layer.atlasX, top: layer.atlasY, width: layer.width, height: layer.height}).png().toBuffer();
    if (id === 'head') {
        const data = await sharp(png).ensureAlpha().raw().toBuffer();
        for (let y = 0; y < layer.height; y++) for (let x = 0; x < layer.width; x++) {
            const sx = x + layer.left, sy = y + layer.top;
            if (sx > 110 && sy > 275 && sy > 346 - .354 * sx) data[(y * layer.width + x) * 4 + 3] = 0;
        }
        png = await sharp(data, {raw: {width: layer.width, height: layer.height, channels: 4}}).png().toBuffer();
    }
    await add(id, png, {left: layer.left, top: layer.top, width: layer.width, height: layer.height});
}
const front = await sharp(inputPaths[2]).ensureAlpha().raw().toBuffer();
const hind = await sharp(inputPaths[3]).ensureAlpha().raw().toBuffer();
const sourceWidth = 452;
async function masked(id, source, box, path) {
    const mask = await sharp(Buffer.from(`<svg width="${box.width}" height="${box.height}"><path fill="white" d="${path}"/></svg>`)).ensureAlpha().raw().toBuffer();
    const pixels = Buffer.alloc(box.width * box.height * 4);
    for (let y = 0; y < box.height; y++) for (let x = 0; x < box.width; x++) {
        const p = (y * box.width + x) * 4, sx = box.left + x, sy = box.top + y;
        let q = (sy * sourceWidth + sx) * 4;
        if (source[q + 3] < 240 && sy > 407) {
            let donor = sy; while (donor > 380 && source[(donor * sourceWidth + sx) * 4 + 3] < 240) donor--;
            q = (donor * sourceWidth + sx) * 4;
        }
        source.copy(pixels, p, q, q + 3); pixels[p + 3] = Math.round(source[q + 3] * mask[p + 3] / 255);
    }
    await add(id, await sharp(pixels, {raw: {width: box.width, height: box.height, channels: 4}}).png().toBuffer(), box);
}
await add('nearFront', await sharp(inputPaths[2]).extract({left: 102, top: 320, width: 92, height: 180}).png().toBuffer(), {left: 102, top: 320, width: 92, height: 180});
await masked('nearHindThigh', hind, {left: 244, top: 316, width: 116, height: 126}, 'M60 2 C92 5 115 38 112 78 C110 108 88 125 57 125 C22 125 3 107 2 75 C1 37 29 1 60 2Z');

// Round hidden caps use the same coat rows, rather than stretched pose images or painted flat fills.
const width = 72, height = 192, radius = 35.5, pixels = Buffer.alloc(width * height * 4);
for (let y = 0; y < height; y++) {
    const sy = Math.round(365 + y / (height - 1) * 78), row = [];
    for (let x = 105; x < 192; x++) if (front[(sy * sourceWidth + x) * 4 + 3] > 220) row.push(x);
    if (!row.length) throw new Error(`Empty donor row ${sy}`);
    const left = row[0], right = row.at(-1), cy = Math.max(radius, Math.min(height - 1 - radius, y));
    for (let x = 0; x < width; x++) {
        const alpha = Math.max(0, Math.min(1, radius + .5 - Math.hypot(x - radius, y - cy)));
        const sx = Math.round(left + (right - left) * x / (width - 1)), q = (sy * sourceWidth + sx) * 4, p = (y * width + x) * 4;
        front.copy(pixels, p, q, q + 3); pixels[p + 3] = Math.round(alpha * 255);
    }
}
await add('shaft', await sharp(pixels, {raw: {width, height, channels: 4}}).png().toBuffer(), {left: 0, top: 0, width, height});
const neckWidth = 100, neckHeight = 120, neck = Buffer.alloc(neckWidth * neckHeight * 4);
for (let y = 0; y < neckHeight; y++) for (let x = 0; x < neckWidth; x++) {
    const dx = (x - 49.5) / 49, dy = (y - 59.5) / 59, alpha = Math.max(0, Math.min(1, (1 - Math.hypot(dx, dy)) * 50));
    const sx = Math.round(121 + x / 99 * 42), sy = Math.round(359 + y / 119 * 45), q = (sy * sourceWidth + sx) * 4, p = (y * neckWidth + x) * 4;
    front.copy(neck, p, q, q + 3); neck[p + 3] = Math.round(alpha * 255);
}
await add('neck', await sharp(neck, {raw: {width: neckWidth, height: neckHeight, channels: 4}}).png().toBuffer(), {left: 0, top: 0, width: neckWidth, height: neckHeight});
const tail = layers.find(layer => layer.id === 'tail'), tailPixels = await sharp(images.get('tail')).ensureAlpha().raw().toBuffer();
tail.support = [];
for (let y = 0; y < tail.height; y += 2) {
    const row = []; for (let x = 0; x < tail.width; x++) if (tailPixels[(y * tail.width + x) * 4 + 3] > 64) row.push(x);
    if (row.length) for (const x of [row[0], row.at(-1)]) tail.support.push([x + tail.left, y + tail.top]);
}
const layout = packAtlasRegions(layers.map(({id, width, height}) => ({id, width: width + 4, height: height + 4})), {maxSize: 1024});
const padded = new Map();
for (const [id, png] of images) padded.set(id, await sharp(png).extend({top: 2, right: 2, bottom: 2, left: 2, background: {r: 0, g: 0, b: 0, alpha: 0}}).png().toBuffer());
const atlas = await composeAtlas(sharp, layout, padded, {compressionLevel: 9});
const result = {schemaVersion: 1, width: layout.width, height: layout.height, source: manifest.source, inputs,
    layers: layers.map(layer => {const region = layout.regions.find(r => r.id === layer.id); return {...layer, atlasX: region.x + 2, atlasY: region.y + 2};}),
    method: 'Original head/scarf exact crop; torso/tail/paw original crops, unfaded source foreleg, closed thigh mask with same-column hidden completion, capsule/neck coat-row resampling. No new model output.'};
for (const [path, bytes] of [[`${out}/atlas.png`, atlas], [`${out}/atlas.json`, Buffer.from(JSON.stringify(result, null, 2))], [`${base}/derivation.json`, Buffer.from(JSON.stringify({...result, atlasSha256: hash(atlas), bytes: atlas.length}, null, 2))]]) {
    if (check) {if (!(await readFile(path)).equals(bytes)) throw new Error(`Stale derived output: ${path}`);} else await writeFile(path, bytes);
}
console.log(`${check ? 'PASS' : 'BUILT'} ${layout.width}x${layout.height}, ${atlas.length} bytes, ${layers.length} source-derived parts`);
