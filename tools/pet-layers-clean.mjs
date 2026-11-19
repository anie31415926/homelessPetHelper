import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/pet-layers/cleanup', runtime = 'public/pet-layers';
await mkdir(folder, { recursive: true });
const read = path => sharp(path).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const [{ data: original, info }, { data: before }, { data: donor }, { data: visible }] = await Promise.all([
    read('public/pet-mesh/cat-side.png'), read(`${runtime}/far-hind-ai.png`), read(`${runtime}/far-hind.png`),
    read('art-source/pet-layers/visible-only.png'),
]);
const { width, height } = info;
if (width !== 452 || height !== 512 || [before, donor, visible].some(data => data.length !== original.length)) {
    throw new Error('Cleanup is calibrated for the original 452x512 same-source layers only');
}
const silhouette = 'M281 353 C299 354 312 369 312 386 C312 410 288 438 280 455 C274 464 266 469 264 481 C256 483 246 483 240 479 C234 477 235 466 242 461 C247 457 254 456 256 451 C251 446 249 440 250 434 C260 424 272 407 271 392 C269 371 272 357 281 353Z';
const cutPath = 'M226 432 Q238 434 250 431 Q257 437 267 442 L282 453 Q287 456 278 463 Q267 470 265 479 L267 493 L226 493Z';
async function rasterPath(path) {
    const supersampled = await sharp(Buffer.from(`<svg width="${width * 4}" height="${height * 4}" viewBox="0 0 ${width} ${height}"><path fill="white" d="${path}"/></svg>`))
        .ensureAlpha().raw().toBuffer();
    const result = Buffer.alloc(width * height * 4);
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        let alpha = 0;
        for (let yy = 0; yy < 4; yy++) for (let xx = 0; xx < 4; xx++) alpha += supersampled[((y * 4 + yy) * width * 4 + x * 4 + xx) * 4 + 3];
        const p = (y * width + x) * 4;
        result.set([255, 255, 255, Math.round(alpha / 16)], p);
    }
    return result;
}
const mask = await rasterPath(silhouette), cutMask = await rasterPath(cutPath);
const offset = (x, y) => (y * width + x) * 4;
const save = (data, path) => sharp(data, { raw: { width, height, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path);

function neighbours(x, y, radius, accept) {
    const result = [];
    for (let dy = -radius; dy <= radius; dy++) for (let dx = -radius; dx <= radius; dx++) {
        const xx = x + dx, yy = y + dy, distance = dx * dx + dy * dy;
        if (xx < 0 || yy < 0 || xx >= width || yy >= height || distance > radius * radius) continue;
        const p = offset(xx, yy);
        if (accept(p, xx, yy)) result.push({ p, distance });
    }
    return result.sort((a, b) => a.distance - b.distance);
}

const edgeDistance = new Float32Array(width * height).fill(10);
for (let y = 348; y < 490; y++) for (let x = 228; x < 319; x++) {
    const p = offset(x, y);
    if (!mask[p + 3]) continue;
    const outside = neighbours(x, y, 4, p => mask[p + 3] < 128)[0];
    edgeDistance[y * width + x] = outside ? Math.sqrt(outside.distance) : 5;
}

function cleanLayer(input) {
    const output = Buffer.alloc(input.length);
    for (let y = 348; y < 490; y++) for (let x = 228; x < 319; x++) {
        const p = offset(x, y), alpha = mask[p + 3];
        if (!alpha) continue;
        const nearest = input[p + 3] >= 200 ? p : neighbours(x, y, 6, p => input[p + 3] >= 200)[0]?.p;
        if (nearest == null) throw new Error(`Silhouette has no source colour at ${x},${y}`);
        input.copy(output, p, nearest, nearest + 3); output[p + 3] = alpha;
        if (edgeDistance[y * width + x] > 2) continue;
        const whiteFringe = Math.min(output[p], output[p + 1], output[p + 2]) > 226 || output[p] - output[p + 2] < 24;
        if (!whiteFringe) continue;
        const samples = neighbours(x, y, 5, p => input[p + 3] >= 240 && input[p] - input[p + 2] > 45).slice(0, 3);
        if (!samples.length) continue;
        for (let c = 0; c < 3; c++) output[p + c] = Math.round(samples.reduce((sum, sample) => sum + input[sample.p + c], 0) / samples.length);
    }
    return output;
}

function smoothJoin(input) {
    const output = Buffer.from(input), band = [];
    for (let y = 425; y <= 457; y++) for (let x = 246; x <= 285; x++) {
        const p = offset(x, y), line = 431 + (x - 250) * .6875;
        if (Math.abs(y - line) <= 4.5 && mask[p + 3] === 255 && edgeDistance[y * width + x] >= 3) band.push(p);
    }
    // Solve a narrow colour repair band; fixed surrounding fur and paw detail stay sharp.
    for (let step = 0; step < 100; step++) {
        const previous = Buffer.from(output);
        for (const p of band) for (let c = 0; c < 3; c++) output[p + c] = Math.round(
            (previous[p - 4 + c] + previous[p + 4 + c] + previous[p - width * 4 + c] + previous[p + width * 4 + c]) / 4);
    }
    return { output, band };
}

function dilateRgb(input) {
    const output = Buffer.from(input);
    for (let y = 346; y < 492; y++) for (let x = 226; x < 321; x++) {
        const p = offset(x, y);
        if (input[p + 3]) continue;
        const nearest = neighbours(x, y, 3, p => input[p + 3] > 128)[0];
        if (nearest) input.copy(output, p, nearest.p, nearest.p + 3);
    }
    return output;
}

const aiRepair = smoothJoin(cleanLayer(before)), donorRepair = smoothJoin(cleanLayer(donor));
const ai = dilateRgb(aiRepair.output), cleanDonor = dilateRgb(donorRepair.output);
const front = Buffer.from(original);
for (let p = 0; p < original.length; p += 4) front[p + 3] = Math.round(original[p + 3] * (1 - cutMask[p + 3] / 255));
await save(ai, `${runtime}/far-hind-ai-clean.png`); await save(cleanDonor, `${runtime}/far-hind-clean.png`);
await save(front, `${runtime}/foreground-clean.png`);
await save(mask, `${folder}/silhouette.png`); await save(cutMask, `${folder}/foreground-cut-mask.png`);
const repairMask = Buffer.alloc(mask.length);
for (const p of aiRepair.band) repairMask.set([215, 132, 132, 255], p);
await save(repairMask, `${folder}/join-repair-mask.png`);
const protectedMask = Buffer.alloc(mask.length), repairs = new Set(aiRepair.band);
let protectedPixels = 0;
for (let p = 0; p < mask.length; p += 4) {
    if (visible[p + 3] !== 255 || mask[p + 3] !== 255 || edgeDistance[p / 4] <= 2 || repairs.has(p)) continue;
    for (let c = 0; c < 4; c++) if (ai[p + c] !== original[p + c]) throw new Error(`Protected paw pixel changed at ${p / 4}`);
    protectedMask.set([99, 139, 115, 255], p); protectedPixels++;
}
await save(protectedMask, `${folder}/protected-paw.png`);

const crop = { left: 225, top: 330, width: 112, height: 165 };
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#283e3b']]) {
    const crops = await Promise.all(['far-hind-ai.png', 'far-hind-ai-clean.png'].map(file => sharp(`${runtime}/${file}`).extract(crop).png().toBuffer()));
    const pair = await sharp({ create: { width: crop.width * 2, height: crop.height, channels: 4, background } }).composite([
        { input: crops[0], left: 0, top: 0 }, { input: crops[1], left: crop.width, top: 0 },
    ]).png().toBuffer();
    await sharp(pair).resize({ height: 660 }).png().toFile(`${folder}/compare-${name}.png`);
}
const detail = await sharp(`${runtime}/far-hind-ai-clean.png`).extract(crop).png().toBuffer();
await sharp(detail).resize({ height: 660 }).png().toFile(`${folder}/leg-detail.png`);
const frontCrop = { left: 220, top: 410, width: 135, height: 90 };
const frontDetail = await sharp(`${runtime}/foreground.png`).extract(frontCrop).resize(810, 540, { kernel: 'nearest' }).flatten({ background: '#283e3b' }).png().toBuffer();
const grid = Array.from({ length: 14 }, (_, i) => `<path d="M${i * 60} 0V540" stroke="#97b8ad" opacity=".3"/><text x="${i * 60 + 2}" y="16">${220 + i * 10}</text>`).join('') +
    Array.from({ length: 9 }, (_, i) => `<path d="M0 ${i * 60}H810" stroke="#97b8ad" opacity=".3"/><text x="2" y="${i * 60 + 32}">${410 + i * 10}</text>`).join('');
await sharp(frontDetail).composite([{ input: Buffer.from(`<svg width="810" height="540"><g fill="#d7ebe1" font-family="sans-serif" font-size="14">${grid}</g></svg>`) }]).png().toFile(`${folder}/foreground-detail-before.png`);
await sharp(`${runtime}/foreground-clean.png`).extract(frontCrop).resize(810, 540, { kernel: 'nearest' }).flatten({ background: '#283e3b' }).png().toFile(`${folder}/foreground-detail-after.png`);
const hash = async path => createHash('sha256').update(await readFile(path)).digest('hex');
await writeFile(`${folder}/derivation.json`, JSON.stringify({ source: 'public/pet-mesh/cat-side.png',
    sourceSha256: await hash('public/pet-mesh/cat-side.png'), modelLayerSha256: await hash(`${runtime}/far-hind-ai.png`),
    donorLayerSha256: await hash(`${runtime}/far-hind.png`), visibleMaskSha256: await hash('art-source/pet-layers/visible-only.png'),
    silhouette, cutPath, width, height, repairBandPixels: aiRepair.band.length, protectedPixels,
    method: 'Supersampled continuous leg contour, local white-fringe colour replacement from same-image peach outline, narrow harmonic colour repair at cut seam, 3px transparent RGB dilation. No new model generation; original files preserved.',
    restrictions: 'Only far leg and local foreground cut change. Original cat file and foreground outside cut mask are unchanged. Protected core paw texture remains original; edge and seam pixels may change in this authorized cleanup.',
}, null, 2));
console.log({ repairBandPixels: aiRepair.band.length, protectedPixels });
