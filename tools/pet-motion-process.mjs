import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/pet-motion', runtime = 'public/pet-motion', source = 'public/pet-mesh/cat-side.png';
const width = 452, height = 512, raw = { width, height, channels: 4 };
await mkdir(folder, { recursive: true }); await mkdir(runtime, { recursive: true });
const { data: original, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
if (info.width !== width || info.height !== height) throw new Error('Original cat coordinates changed');
const save = (data, path) => sharp(data, { raw }).png({ compressionLevel: 9 }).toFile(path);
const smooth = (a, b, value) => { const t = Math.max(0, Math.min(1, (value - a) / (b - a))); return t * t * (3 - 2 * t); };
function bleedTransparentRgb(data) {
    let known = Uint8Array.from({ length: width * height }, (_, i) => data[i * 4 + 3] > 16 ? 1 : 0);
    for (let pass = 0; pass < 3; pass++) {
        const next = known.slice(), previous = Buffer.from(data);
        for (let y = 1; y < height - 1; y++) for (let x = 1; x < width - 1; x++) {
            const i = y * width + x; if (known[i] || data[i * 4 + 3]) continue;
            const neighbour = [i - 1, i + 1, i - width, i + width].find(index => known[index]);
            if (neighbour === undefined) continue;
            previous.copy(data, i * 4, neighbour * 4, neighbour * 4 + 3); next[i] = 1;
        }
        known = next;
    }
}
const bodyPath = 'M0 0 H452 V512 H359 V446 C351 430 342 421 325 418 C300 420 275 432 247 434 C214 435 191 429 173 420 C150 409 127 399 111 390 C94 382 82 370 75 355 H0Z';
async function maskFor(path) {
    const large = await sharp(Buffer.from(`<svg width="1808" height="2048" viewBox="0 0 452 512"><path fill="white" d="${path}"/></svg>`)).ensureAlpha().raw().toBuffer();
    const mask = Buffer.alloc(original.length);
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        let sum = 0;
        for (let yy = 0; yy < 4; yy++) for (let xx = 0; xx < 4; xx++) sum += large[((y * 4 + yy) * width * 4 + x * 4 + xx) * 4 + 3];
        mask.set([255, 255, 255, Math.round(sum / 16)], (y * width + x) * 4);
    }
    return mask;
}
const bodyMask = await maskFor(bodyPath), body = Buffer.from(original), patchMask = Buffer.alloc(original.length);
const patches = [
    { cx: 184, cy: 403, rx: 13, ry: 31, offset: [24, -12] },
    { cx: 254, cy: 410, rx: 17, ry: 35, offset: [-22, -26] },
];
let patchedPixels = 0;
for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const p = (y * width + x) * 4;
    body[p + 3] = Math.round(original[p + 3] * bodyMask[p + 3] / 255);
    if (!body[p + 3]) continue;
    for (const patch of patches) {
        const distance = Math.hypot((x - patch.cx) / patch.rx, (y - patch.cy) / patch.ry);
        const weight = 1 - smooth(.35, 1, distance); if (!weight) continue;
        const sample = ((y + patch.offset[1]) * width + x + patch.offset[0]) * 4;
        if (original[sample + 3] < 250) throw new Error('Torso patch sample is not solid fur');
        // Blend toward the nearby torso colour only inside the documented seam mask.
        for (let c = 0; c < 3; c++) body[p + c] = Math.round(original[p + c] * (1 - weight) + original[sample + c] * weight);
        patchMask.set([255, 255, 255, Math.round(weight * 255)], p); patchedPixels++;
    }
}
await save(body, `${runtime}/body.png`); await save(bodyMask, `${folder}/body-mask.png`); await save(patchMask, `${folder}/patch-mask.png`);
for (const kind of ['front', 'hind']) {
    const { data } = await sharp(`art-source/pet-limbs/donor/${kind}-source.png`).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const root = kind === 'front' ? 321 : 318;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) data[(y * width + x) * 4 + 3] = Math.round(data[(y * width + x) * 4 + 3] * smooth(root, root + 53, y));
    bleedTransparentRgb(data);
    await save(data, `${runtime}/near-${kind}.png`);
}
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#385453']]) {
    const layers = ['public/pet-limbs/far-hind-copy.png', 'public/pet-limbs/far-front-copy.png', `${runtime}/body.png`, `${runtime}/near-hind.png`, `${runtime}/near-front.png`];
    await sharp({ create: { width, height, channels: 4, background } }).composite(await Promise.all(layers.map(async path => ({ input: await readFile(path) })))).png().toFile(`${folder}/assembled-${name}.png`);
    await sharp(body, { raw }).flatten({ background }).png().toFile(`${folder}/body-${name}.png`);
}
await writeFile(`${folder}/derivation.json`, JSON.stringify({ source, sourceSha256: createHash('sha256').update(await readFile(source)).digest('hex'), bodyPath, patches, patchedPixels,
    nearLegs: { source: 'art-source/pet-limbs/donor/*-source.png', scale: 1, rootAlphaFade: 53, transparentRgbBleed: 3 },
    method: 'Same-source torso silhouette and local fur seam repair; native near legs with root-only alpha feather. Reuse approved far-leg copies unchanged. No generation or original-file mutation.' }, null, 2));
console.log('Prepared same-source four-leg layers; patched torso seam pixels:', patchedPixels);
