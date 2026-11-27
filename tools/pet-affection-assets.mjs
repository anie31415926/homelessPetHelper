import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const root = fileURLToPath(new URL('../', import.meta.url));
const output = path.join(root, 'public/pet-affection');
const provenance = path.join(root, 'art-source/pet-affection-20261127/derivation.json');
const size = 512;
const face = { x: 108, y: 107, width: 181, height: 133 };
const clamp = v => Math.max(0, Math.min(1, v));
const smooth = v => { const t = clamp(v); return t * t * (3 - 2 * t); };
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const rgba = { width: size, height: size, channels: 4 };
const input = id => `art-source/pet-assets-wx-20261127/clean/柴犬/宠物游戏操作界面背景设计 (${id})-clean.png`;

async function reference(id) {
    const bytes = await readFile(path.join(root, input(id)));
    const pixels = await sharp(bytes).resize(size, size).ensureAlpha().raw().toBuffer();
    return { pixels, sha256: sha(bytes) };
}

// An art-specific silhouette envelope protects white fur and excludes the baked floor shadow.
const silhouette = 'M82 182 C80 158 96 135 111 117 C111 96 117 56 128 45 C140 32 168 61 185 81 C203 78 227 84 235 88 C250 77 280 50 294 55 C307 59 301 105 301 134 C310 151 321 183 318 216 L331 220 C322 191 335 172 352 162 C369 149 392 146 406 151 C432 154 452 185 454 205 C462 234 441 264 420 271 C418 278 426 289 424 315 C422 341 411 381 408 409 L405 454 C407 466 401 474 387 475 L365 474 C359 471 365 448 368 428 C360 413 344 401 341 383 C333 404 329 425 322 452 C319 467 298 470 286 465 C276 460 287 449 289 438 L291 407 C282 438 275 467 262 488 C256 496 225 499 215 490 C210 486 215 468 219 457 L225 405 L210 404 C200 425 196 455 193 473 C191 485 175 492 157 486 C143 484 148 473 154 463 C147 429 140 401 129 376 C117 350 99 328 93 305 C86 286 86 254 95 231 C85 224 80 200 82 182 Z';

async function cutBase(pixels) {
    const envelope = await sharp(Buffer.from(`<svg width="512" height="512"><path d="${silhouette}" fill="white"/></svg>`))
        .ensureAlpha().raw().toBuffer();
    const interior = await sharp(envelope, { raw: rgba }).extractChannel(3).blur(3).raw().toBuffer();
    const result = Buffer.from(pixels);
    for (let i = 0; i < pixels.length; i += 4) {
        const core = smooth((interior[i / 4] / 255 - .52) / .45);
        const gate = envelope[i + 3] / 255;
        const distance = Math.hypot(252 - pixels[i], 251 - pixels[i + 1], 244 - pixels[i + 2]);
        const matte = smooth((distance - 8) / 29);
        const alpha = gate * Math.max(core, matte);
        result[i + 3] = Math.round(255 * alpha);
    }
    return sharp(result, { raw: rgba }).webp({ lossless: true }).toBuffer();
}

function facePatch(pixels) {
    const result = Buffer.alloc(face.width * face.height * 4);
    for (let y = 0; y < face.height; y++) {
        for (let x = 0; x < face.width; x++) {
            const sx = face.x + x;
            const sy = face.y + y;
            const source = (sy * size + sx) * 4;
            const dest = (y * face.width + x) * 4;
            pixels.copy(result, dest, source, source + 3);
            const radius = Math.hypot((sx - 198) / 90, (sy - 174) / 66);
            result[dest + 3] = Math.round(255 * smooth((1 - radius) / .28));
        }
    }
    return sharp(result, { raw: { width: face.width, height: face.height, channels: 4 } }).png().toBuffer();
}

const [calm, soft, happy] = await Promise.all([26, 33, 34].map(reference));
const [base, softPatch, happyPatch] = await Promise.all([
    cutBase(calm.pixels), facePatch(soft.pixels), facePatch(happy.pixels),
]);
const atlas = await sharp({ create: { width: face.width * 2, height: face.height, channels: 4, background: '#0000' } })
    .composite([{ input: softPatch, left: 0, top: 0 }, { input: happyPatch, left: face.width, top: 0 }])
    .webp({ lossless: true }).toBuffer();

await mkdir(output, { recursive: true });
await mkdir(path.dirname(provenance), { recursive: true });
await writeFile(path.join(output, 'shiba.webp'), base);
await writeFile(path.join(output, 'faces.webp'), atlas);
await writeFile(provenance, JSON.stringify({
    tool: 'Sharp 0.35.5; resize, hand-fit alpha envelope, same-coordinate feathered RGBA patches',
    inputs: [calm, soft, happy].map((asset, index) => ({
        role: ['calm', 'soft', 'happy'][index], file: input([26, 33, 34][index]), sha256: asset.sha256,
    })),
    size, face, cells: ['soft', 'happy'],
    outputs: [base, atlas].map((bytes, index) => ({
        file: ['shiba.webp', 'faces.webp'][index], bytes: bytes.length, sha256: sha(bytes),
    })),
}, null, 2) + '\n');
console.log('Shiba base and face atlas:', base.length, atlas.length, 'bytes');
