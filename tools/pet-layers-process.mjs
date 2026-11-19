import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/pet-layers', runtime = 'public/pet-layers';
const source = 'public/pet-mesh/cat-side.png';
await mkdir(folder, { recursive: true }); await mkdir(runtime, { recursive: true });
const { data: original, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const { width: w, height: h } = info;
const shape = async d => sharp(Buffer.from(`<svg width="${w}" height="${h}"><path fill="white" d="${d}"/></svg>`)).ensureAlpha().raw().toBuffer();
const visiblePath = 'M250 431 Q257 437 267 442 L282 453 Q287 456 278 463 Q267 470 265 479 L267 483 Q252 486 240 482 Q232 480 234 468 Q236 458 248 454 L255 450 Q248 440 250 431Z';
const completionPath = 'M281 352 Q303 354 313 374 Q316 402 299 425 Q281 445 276 460 Q271 472 254 474 Q240 474 241 461 Q242 447 257 430 Q271 413 269 392 Q266 367 281 352Z';
const visibleMask = await shape(visiblePath), completionMask = await shape(completionPath);
const foreground = Buffer.from(original), far = Buffer.alloc(original.length), provenance = Buffer.alloc(original.length);
let direct = 0, completed = 0;
for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
    const p = (y * w + x) * 4;
    const visible = visibleMask[p + 3] >= 128 && original[p + 3] > 0;
    if (visible) {
        original.copy(far, p, p, p + 4); foreground[p + 3] = 0;
        provenance[p] = 99; provenance[p + 1] = 139; provenance[p + 2] = 115; provenance[p + 3] = 255;
        direct++; continue;
    }
    // Only complete pixels hidden by the opaque original foreground at rest.
    // The donor is the SAME image's near hind leg; no generative body part art.
    if (completionMask[p + 3] > 0 && original[p + 3] === 255) {
        const sx = Math.round(281 + (x - 255) * .90), sy = Math.round(364 + (y - 352) * .92);
        if (sx < 0 || sx >= w || sy < 0 || sy >= h) continue;
        const sp = (sy * w + sx) * 4;
        original.copy(far, p, sp, sp + 4);
        far[p + 3] = Math.min(original[sp + 3], completionMask[p + 3]);
        provenance[p] = 215; provenance[p + 1] = 132; provenance[p + 2] = 132; provenance[p + 3] = far[p + 3];
        completed++;
    }
}
const save = (data, path) => sharp(data, { raw: { width: w, height: h, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path);
await save(foreground, `${runtime}/foreground.png`);
await save(far, `${runtime}/far-hind.png`);
await save(provenance, `${folder}/provenance.png`);
const visibleOnly = Buffer.alloc(original.length), editable = Buffer.alloc(original.length);
for (let p = 0; p < original.length; p += 4) {
    if (visibleMask[p + 3] >= 128 && original[p + 3] > 0) original.copy(visibleOnly, p, p, p + 4);
    else if (completionMask[p + 3] > 0 && original[p + 3] === 255) editable[p] = editable[p + 1] = editable[p + 2] = editable[p + 3] = completionMask[p + 3];
}
await save(visibleOnly, `${folder}/visible-only.png`);
await save(editable, `${folder}/completion-mask.png`);
await sharp({ create: { width: w * 3, height: h, channels: 4, background: '#f0f6f1' } }).composite([
    { input: source, left: 0, top: 0 },
    { input: `${runtime}/far-hind.png`, left: w, top: 0 },
    { input: `${folder}/provenance.png`, left: w * 2, top: 0 },
]).png().toFile(`${folder}/separation-review.png`);
await sharp({ create: { width: w, height: h, channels: 4, background: '#00000000' } }).composite([
    { input: `${runtime}/far-hind.png`, left: 0, top: 0 }, { input: `${runtime}/foreground.png`, left: 0, top: 0 },
]).png().toFile(`${folder}/rest-reassembled.png`);
await writeFile(`${folder}/derivation.json`, JSON.stringify({ source,
    sourceSha256: createHash('sha256').update(await readFile(source)).digest('hex'), width: w, height: h,
    visiblePath, completionPath, directPixels: direct, completedPixels: completed,
    donorMapping: { x: 'round(281 + (x-255)*0.90)', y: 'round(364 + (y-352)*0.92)' },
    method: 'Direct visible far hind paw cut to rear layer. Occluded upper portion completed with same-image near-hind donor, only under opaque foreground at rest. No new model generation, no original proportion change, no front-leg/body reconstruction.',
    provenance: 'green=directly separated source pixels; coral=donor-completed hidden pixels',
}, null, 2));
await sharp(source).extract({ left: 220, top: 415, width: 135, height: 90 }).resize(810, 540, { kernel: 'nearest' }).png().toFile(`${folder}/paw-detail.png`);
console.log({ directPixels: direct, completedPixels: completed });
