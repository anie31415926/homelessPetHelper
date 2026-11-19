import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const source = 'public/pet-mesh/cat-side.png', folder = 'art-source/pet-limbs/donor', runtime = 'public/pet-limbs';
const { data: original, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const width = 452, height = 512, raw = { width, height, channels: 4 };
if (info.width !== width || info.height !== height) throw new Error('Original cat dimensions changed');
await mkdir(folder, { recursive: true }); await mkdir(runtime, { recursive: true });

const definitions = {
    front: {
        path: 'M127 322 C151 317 175 327 183 350 C190 369 194 391 190 409 L175 465 C172 487 163 497 136 498 C116 500 104 494 105 481 C105 470 114 462 121 455 C122 436 117 415 114 397 C107 378 105 356 111 340 C115 330 120 325 127 322Z',
        offset: [-25, -7], rootY: 322,
    },
    hind: {
        path: 'M288 318 C321 315 352 343 357 375 C360 402 355 427 347 452 C338 480 328 497 301 499 C281 501 262 497 263 484 C263 473 274 464 283 457 C268 448 254 436 249 420 C243 402 247 382 255 364 C261 343 272 326 288 318Z',
        offset: [-31, -8], rootY: 318,
    },
};
const save = (data, path) => sharp(data, { raw }).png({ compressionLevel: 9 }).toFile(path);
async function maskFor(path) {
    const high = await sharp(Buffer.from(`<svg width="${width * 4}" height="${height * 4}" viewBox="0 0 ${width} ${height}"><path fill="white" d="${path}"/></svg>`)).ensureAlpha().raw().toBuffer();
    const mask = Buffer.alloc(width * height);
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        let sum = 0;
        for (let dy = 0; dy < 4; dy++) for (let dx = 0; dx < 4; dx++) sum += high[((y * 4 + dy) * width * 4 + x * 4 + dx) * 4 + 3];
        mask[y * width + x] = Math.round(sum / 16);
    }
    return mask;
}
function extent(data) {
    let left = width, right = -1, top = height, bottom = -1, pixels = 0;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) if (data[(y * width + x) * 4 + 3] > 64) {
        left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y); pixels++;
    }
    return { left, right, top, bottom, width: right - left + 1, height: bottom - top + 1, pixels };
}
const records = {};
for (const [kind, definition] of Object.entries(definitions)) {
    const mask = await maskFor(definition.path), donor = Buffer.alloc(original.length), output = Buffer.alloc(original.length);
    const maskImage = Buffer.alloc(original.length), [dx, dy] = definition.offset;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const p = (y * width + x) * 4, alpha = Math.round(original[p + 3] * mask[y * width + x] / 255);
        maskImage.set([255, 255, 255, mask[y * width + x]], p);
        if (!alpha) continue;
        original.copy(donor, p, p, p + 3); donor[p + 3] = alpha;
        const tx = x + dx, ty = y + dy;
        if (tx < 0 || ty < 0 || tx >= width || ty >= height) throw new Error('Copied limb leaves canvas');
        const target = (ty * width + tx) * 4;
        // Identical source scale; shadow is a colour change, never an alpha/size reduction.
        const shade = .94 - .04 * Math.max(0, 1 - (y - definition.rootY) / 150);
        for (let c = 0; c < 3; c++) output[target + c] = Math.round(original[p + c] * shade);
        output[target + 3] = alpha;
    }
    await save(donor, `${folder}/${kind}-source.png`); await save(maskImage, `${folder}/${kind}-mask.png`);
    await save(output, `${runtime}/far-${kind}-copy.png`);
    records[kind] = { ...definition, sourceExtent: extent(donor), outputExtent: extent(output), scale: [1, 1] };
}
const foreground = await readFile('public/pet-limbs/foreground.png');
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#385453']]) {
    const front = await readFile(`${runtime}/far-front-copy.png`), hind = await readFile(`${runtime}/far-hind-copy.png`);
    await sharp({ create: { width, height, channels: 4, background } }).composite([{ input: hind }, { input: front }]).png().toFile(`${folder}/whole-${name}.png`);
    await sharp({ create: { width, height, channels: 4, background } }).composite([{ input: hind }, { input: front }, { input: foreground }]).png().toFile(`${folder}/assembled-${name}.png`);
}
await writeFile(`${folder}/derivation.json`, JSON.stringify({ source, sourceSha256: createHash('sha256').update(await readFile(source)).digest('hex'), records,
    method: 'Copy actual near front/hind legs from original cat with masked alpha, integer translation, scale exactly 1. No model, contour fitting, or original far-paw overlays. RGB shadow multiplier = .94 - .04 * max(0, 1 - (sourceY - rootY) / 150). Foreground reused unchanged.' }, null, 2));
console.log(JSON.stringify(records, null, 2));
