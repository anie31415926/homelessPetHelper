import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import sharp from 'sharp';
import { tailArt, tailHinge } from '../src/pet-affection/rig.js';

const source = new URL('../public/pet-affection/shiba.webp', import.meta.url);
const output = new URL('../public/pet-affection/shiba-tail-layers.webp', import.meta.url);
const provenance = new URL('../art-source/pet-affection-20261127/tail-derivation.json', import.meta.url);
const size = 512;
const { crop, atlas } = tailArt;
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const clamp = value => Math.max(0, Math.min(1, value));
const smooth = value => { const t = clamp(value); return t * t * (3 - 2 * t); };

// The lower contour follows the painted line where the curl crosses the back.
const curl = 'M329 222 C313 199 325 161 363 148 C394 136 422 143 442 163 C469 185 470 227 448 256 C437 271 424 280 416 275 C411 262 402 252 391 244 C381 237 376 237 366 238 C351 238 337 231 329 222 Z';
const rump = 'M323 222 C336 220 350 222 364 223 C387 224 403 240 411 258 C420 280 421 316 422 327 L315 328 Z';
const repair = 'M324 216 C333 219 344 225 354 226 C375 227 391 231 403 244 C409 250 422 269 424 285 L462 286 L465 140 L322 140 Z';
const rootFur = 'M398 250 C404 251 409 254 413 260 C417 266 420 273 419 280 C412 283 404 279 399 274 C395 269 393 259 398 250 Z';

async function mask(d, blur = 0) {
    const svg = Buffer.from(`<svg width="512" height="512"><path d="${d}" fill="white"/></svg>`);
    const channel = sharp(svg).extractChannel(3);
    if (blur) channel.blur(blur);
    return channel.raw().toBuffer();
}

const sourceBytes = await readFile(source);
const pixels = await sharp(sourceBytes).ensureAlpha().raw().toBuffer();
const [tailMask, bodyMask, repairMask, rootMask] = await Promise.all([
    mask(curl, .75), mask(rump, .5), mask(repair, 3), mask(rootFur, 1.8),
]);
const body = Buffer.from(pixels);
const tail = Buffer.alloc(crop.width * crop.height * 4);
const root = Buffer.alloc(tail.length);
const { from, to } = tailHinge;
const dx = to.x - from.x, dy = to.y - from.y;
const axisLength = Math.hypot(dx, dy);

for (let y = crop.y; y < crop.y + crop.height; y++) {
    for (let x = crop.x; x < crop.x + crop.width; x++) {
        const i = y * size + x;
        const p = i * 4;
        const t = ((y - crop.y) * crop.width + x - crop.x) * 4;
        pixels.copy(tail, t, p, p + 3);
        pixels.copy(root, t, p, p + 3);
        // Trim only the actual attachment. The curl lip further left must lift as one piece.
        const across = (dy * (x - from.x) - dx * (y - from.y)) / axisLength;
        const overlap = 1 - smooth((x - 390) / 13) * (1 - smooth((across + 14) / 8));
        tail[t + 3] = Math.round(pixels[p + 3] * tailMask[i] / 255 * overlap);
        root[t + 3] = Math.round(pixels[p + 3] * rootMask[i] / 255);

        // Borrow nearby ochre fur below the join; repaint only the area normally covered by the curl.
        const side = 324 - 7 * smooth((223 - y) / 11);
        const blend = Math.max(tailMask[i] / 255, repairMask[i] / 255)
            * smooth((x - side) / 9);
        if (blend === 0) continue;
        const cloneAt = ((y + 28) * size + (x - 14)) * 4;
        body[p + 3] = Math.round(pixels[p + 3] * (1 - blend) + bodyMask[i] * blend);
        for (let c = 0; c < 3; c++) {
            body[p + c] = Math.round(pixels[p + c] * (1 - blend) + pixels[cloneAt + c] * blend);
        }
    }
}

const packed = Buffer.alloc(atlas.width * atlas.height * 4);
for (let y = 0; y < size; y++) {
    body.copy(packed, y * atlas.width * 4, y * size * 4, (y + 1) * size * 4);
    if (y < crop.height) tail.copy(packed, (y * atlas.width + atlas.tailX) * 4,
        y * crop.width * 4, (y + 1) * crop.width * 4);
    if (y < crop.height) root.copy(packed, ((y + atlas.rootY) * atlas.width + atlas.tailX) * 4,
        y * crop.width * 4, (y + 1) * crop.width * 4);
}
const result = await sharp(packed, { raw: { width: atlas.width, height: atlas.height, channels: 4 } })
    .webp({ lossless: true }).toBuffer();
await writeFile(output, result);
await writeFile(provenance, JSON.stringify({
    tool: 'Sharp; fitted curl, stationary root-fur overlay, and nearby fur clone for the rump',
    source: { file: 'public/pet-affection/shiba.webp', sha256: sha(sourceBytes) },
    result: { file: 'public/pet-affection/shiba-tail-layers.webp', bytes: result.length, sha256: sha(result) },
    layout: tailArt,
    hinge: { from, to },
}, null, 2) + '\n');
console.log(`Split-tail atlas: ${result.length} bytes`);
