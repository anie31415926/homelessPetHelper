import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/pet-limbs', runtime = 'public/pet-limbs', width = 452, height = 512;
const source = 'public/pet-mesh/cat-side.png';
await mkdir(folder, { recursive: true }); await mkdir(runtime, { recursive: true });
const read = path => sharp(path).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const { data: original, info } = await read(source);
if (info.width !== width || info.height !== height) throw new Error('Original cat dimensions changed');
const specs = {
    front: {
        silhouette: 'M121 315 C142 312 156 327 154 348 C151 372 135 392 132 414 L130 451 C133 462 131 478 124 484 C116 490 101 488 96 482 C90 477 92 467 101 462 L107 456 C106 439 105 418 102 401 C98 382 94 361 98 343 C101 329 108 319 121 315Z',
        visible: 'M98 391 Q106 403 108 420 L111 447 Q114 456 117 460 Q111 465 109 472 Q107 480 110 484 Q101 484 96 479 Q91 475 95 467 L103 459 L105 437Z',
        cut: 'M96 384 Q107 398 110 419 L114 450 Q116 457 119 461 Q113 465 111 471 Q108 478 111 484 L113 488 L87 492 L87 450 L96 430Z',
        crop: { left: 77, top: 295, width: 100, height: 207 },
        anatomy: 'One entire FAR-SIDE FRONT LEG, from the rounded shoulder cap at the top through upper arm, elbow, forearm, wrist and the complete small ivory paw at the bottom. Slender front-leg anatomy, not a hind thigh. The slight elbow bend and paw position follow image 1 exactly.',
        seed: 2026111921,
    },
    hind: {
        silhouette: 'M302 318 C327 316 350 336 349 364 C348 390 315 421 282 440 L296 451 C300 458 289 466 281 472 L280 482 C267 488 245 487 238 481 C230 475 234 464 244 460 L257 451 C246 443 239 431 244 421 C259 399 267 383 269 359 C268 337 280 320 302 318Z',
        visible: 'M250 434 Q259 440 268 446 L280 454 Q282 457 276 463 Q267 471 264 481 Q252 484 241 480 Q234 478 236 467 Q241 460 250 456 L256 451Z',
        cut: 'M226 432 Q238 434 250 431 Q257 437 267 442 L282 453 Q287 456 278 463 Q267 470 265 479 L267 493 L226 493Z',
        crop: { left: 216, top: 295, width: 153, height: 207 },
        anatomy: 'One entire FAR-SIDE HIND LEG, beginning with a broad rounded hip-root and thigh at the top, a forward-left knee, a backward-right hock bend, then the full metatarsus and ivory paw pointing left. Keep the knee-to-hock zigzag indicated in image 1. Not a front leg, not a short shin, and not a single sausage-shaped limb. The thigh is broad because it normally hides under the kitten body.',
        seed: 2026111922,
    },
};
const raw = { width, height, channels: 4 };
const save = (data, path) => sharp(data, { raw }).png({ compressionLevel: 9 }).toFile(path);
async function shape(path) {
    const large = await sharp(Buffer.from(`<svg width="${width * 4}" height="${height * 4}" viewBox="0 0 ${width} ${height}"><path fill="white" d="${path}"/></svg>`)).ensureAlpha().raw().toBuffer();
    const result = Buffer.alloc(original.length);
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        let alpha = 0;
        for (let yy = 0; yy < 4; yy++) for (let xx = 0; xx < 4; xx++) alpha += large[((y * 4 + yy) * width * 4 + x * 4 + xx) * 4 + 3];
        result.set([255, 255, 255, Math.round(alpha / 16)], (y * width + x) * 4);
    }
    return result;
}
const front = Buffer.from(original), prepared = {};
for (const [kind, spec] of Object.entries(specs)) {
    const mask = await shape(spec.silhouette), visibleMask = await shape(spec.visible), cut = await shape(spec.cut);
    const visible = Buffer.alloc(original.length), guide = Buffer.alloc(original.length);
    for (let p = 0; p < original.length; p += 4) {
        front[p + 3] = Math.round(front[p + 3] * (1 - cut[p + 3] / 255));
        if (mask[p + 3]) guide.set([158, 165, 159, mask[p + 3]], p);
        if (!visibleMask[p + 3] || !original[p + 3] || !mask[p + 3]) continue;
        original.copy(visible, p, p, p + 4);
        visible[p + 3] = Math.min(visible[p + 3], visibleMask[p + 3], mask[p + 3]);
    }
    const assembled = await sharp(guide, { raw }).composite([{ input: await sharp(visible, { raw }).png().toBuffer() }]).png().toBuffer();
    await sharp(assembled).extract(spec.crop).resize({ height: 768 }).flatten({ background: '#e9f2ed' }).png().toFile(`${folder}/${kind}-guide.png`);
    await save(mask, `${folder}/${kind}-silhouette.png`); await save(visible, `${folder}/${kind}-visible.png`); await save(cut, `${folder}/${kind}-cut.png`);
    prepared[kind] = { mask, visible, cut };
}
await save(front, `${runtime}/foreground.png`);
await sharp(source).extract({ left: 80, top: 375, width: 115, height: 130 }).resize({ height: 650, kernel: 'nearest' }).flatten({ background: '#283e3b' }).png().toFile(`${folder}/front-source-detail.png`);
await writeFile(`${folder}/spec.json`, JSON.stringify({ source, width, height, specs }, null, 2));
if (process.argv.includes('--prepare')) { console.log('Prepared full shoulder/hip-to-paw guides'); process.exit(0); }

function modelSpans(data, w, h) {
    const rows = Array.from({ length: h }, () => null);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const p = (y * w + x) * 4;
        if (data[p] < data[p + 1] + 4 || data[p + 1] < data[p + 2] + 4) continue;
        if (!rows[y]) rows[y] = { left: x, right: x }; else rows[y].right = x;
    }
    return rows;
}
const records = {};
for (const kind of Object.keys(specs)) {
    const { data: model, info: modelInfo } = await read(`${folder}/${kind}/master.png`);
    const rows = modelSpans(model, modelInfo.width, modelInfo.height);
    const modelTop = rows.findIndex(Boolean), modelBottom = rows.findLastIndex(Boolean);
    if (modelTop < 0 || modelBottom - modelTop < modelInfo.height * .5) throw new Error(`${kind}: incomplete generated leg`);
    const { mask, visible } = prepared[kind], targetRows = [];
    for (let y = 0; y < height; y++) {
        let left = width, right = -1;
        for (let x = 0; x < width; x++) if (mask[(y * width + x) * 4 + 3] > 0) { left = Math.min(left, x); right = x; }
        targetRows[y] = right < 0 ? null : { left, right };
    }
    const top = targetRows.findIndex(Boolean), bottom = targetRows.findLastIndex(Boolean), output = Buffer.alloc(original.length);
    const protectedMask = Buffer.alloc(original.length), provenance = Buffer.alloc(original.length);
    let core = 0, generated = 0;
    for (let y = top; y <= bottom; y++) for (let x = 0; x < width; x++) {
        const p = (y * width + x) * 4;
        if (!mask[p + 3]) continue;
        const target = targetRows[y], sy = Math.round(modelTop + (y - top) / (bottom - top) * (modelBottom - modelTop));
        const row = rows[sy]; if (!row || !target) throw new Error(`${kind}: missing scanline`);
        const inset = Math.min(3, (row.right - row.left) / 6);
        const sx = Math.round(row.left + inset + (x - target.left) / Math.max(1, target.right - target.left) * (row.right - row.left - inset * 2));
        const sp = (sy * modelInfo.width + Math.max(row.left, Math.min(row.right, sx))) * 4;
        model.copy(output, p, sp, sp + 3); output[p + 3] = mask[p + 3];
        let distance = 4;
        if (visible[p + 3]) for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) {
            const xx = x + dx, yy = y + dy;
            if (xx < 0 || yy < 0 || xx >= width || yy >= height || visible[(yy * width + xx) * 4 + 3] < 240) distance = Math.min(distance, Math.hypot(dx, dy));
        }
        if (visible[p + 3] === 255 && mask[p + 3] === 255 && distance >= 2) {
            original.copy(output, p, p, p + 4); protectedMask.set([99, 139, 115, 255], p); core++;
            provenance.set([99, 139, 115, 255], p);
        } else {
            const weight = visible[p + 3] / 255 * Math.min(1, distance / 2);
            for (let c = 0; c < 3; c++) output[p + c] = Math.round(output[p + c] * (1 - weight) + original[p + c] * weight);
            provenance.set([215, 132, 132, mask[p + 3]], p); generated++;
        }
    }
    const beforeEdges = Buffer.from(output);
    // Extend the locked original fur colours into the new area, not a hard sticker seam.
    for (let y = top; y <= bottom; y++) for (let x = 1; x < width - 1; x++) {
        const p = (y * width + x) * 4;
        if (!mask[p + 3] || protectedMask[p + 3]) continue;
        let nearest = 145, weight = 0; const colour = [0, 0, 0];
        for (let dy = -10; dy <= 10; dy++) for (let dx = -10; dx <= 10; dx++) {
            const xx = x + dx, yy = y + dy, distance = dx * dx + dy * dy;
            if (xx < 0 || yy < 0 || xx >= width || yy >= height || distance > 100) continue;
            const sp = (yy * width + xx) * 4; if (!protectedMask[sp + 3]) continue;
            const influence = Math.exp(-distance / 24); nearest = Math.min(nearest, distance); weight += influence;
            for (let c = 0; c < 3; c++) colour[c] += original[sp + c] * influence;
        }
        if (!weight) continue;
        const blend = .95 * (1 - Math.sqrt(nearest) / 12);
        for (let c = 0; c < 3; c++) output[p + c] = Math.round(output[p + c] * (1 - blend) + colour[c] / weight * blend);
    }
    for (let y = top; y <= bottom; y++) for (let x = 1; x < width - 1; x++) {
        const p = (y * width + x) * 4;
        if (!mask[p + 3] || protectedMask[p + 3]) continue;
        const edge = [-4, 4, -width * 4, width * 4].some(step => mask[p + step + 3] < 128);
        if (!edge || Math.min(output[p], output[p + 1], output[p + 2]) < 225) continue;
        let nearest;
        for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) {
            const sp = ((y + dy) * width + x + dx) * 4, distance = dx * dx + dy * dy;
            if (beforeEdges[sp + 3] < 240 || beforeEdges[sp] - beforeEdges[sp + 2] < 40 || (nearest && distance >= nearest.distance)) continue;
            nearest = { p: sp, distance };
        }
        if (nearest) beforeEdges.copy(output, p, nearest.p, nearest.p + 3);
    }
    await save(output, `${runtime}/far-${kind}.png`); await save(protectedMask, `${folder}/${kind}-protected.png`); await save(provenance, `${folder}/${kind}-provenance.png`);
    records[kind] = { originalCorePixels: core, modelOrEdgePixels: generated, targetExtent: [top, bottom], modelExtent: [modelTop, modelBottom] };
    const detail = await sharp(`${runtime}/far-${kind}.png`).extract(specs[kind].crop).png().toBuffer();
    await sharp(detail).resize({ height: 828 }).flatten({ background: '#283e3b' }).png().toFile(`${folder}/${kind}-detail.png`);
}
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#283e3b']]) {
    await sharp({ create: { width: width * 3, height, channels: 4, background } }).composite([
        { input: source, left: 0, top: 0 }, { input: `${runtime}/far-front.png`, left: width, top: 0 },
        { input: `${runtime}/far-hind.png`, left: width * 2, top: 0 },
    ]).png().toFile(`${folder}/whole-limbs-${name}.png`);
}
const sourceSha256 = createHash('sha256').update(await readFile(source)).digest('hex');
await writeFile(`${folder}/derivation.json`, JSON.stringify({ source, sourceSha256, records,
    method: 'Two complete far-side limbs from shoulder/hip roots to entire paws. Eroded original visible cores copied verbatim, model-colour silhouette fitted into full guides only for missing extent/edges, nearby original fur blended into generated area over 10px, local pale edge colour repair. Foreground is original minus both visible far limbs. Post-decode masks, not diffusion-level masked inpaint.',
}, null, 2));
console.log(records);
