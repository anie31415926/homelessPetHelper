import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/pet-layers', generated = `${folder}/ai-completion-v2`;
const source = 'public/pet-mesh/cat-side.png', output = 'public/pet-layers/far-hind-ai.png';
const read = path => sharp(path).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const [{ data: original, info }, { data: visible }, { data: mask }, { data: model, info: modelInfo }] = await Promise.all([
    read(source), read(`${folder}/visible-only.png`), read(`${folder}/completion-mask.png`), read(`${generated}/master.png`),
]);
const { width, height } = info;
const far = Buffer.from(visible), provenance = Buffer.alloc(far.length);

function spans(data, w, h, contains) {
    return Array.from({ length: h }, (_, y) => {
        let left = w, right = -1;
        for (let x = 0; x < w; x++) if (contains(data, (y * w + x) * 4)) { left = Math.min(left, x); right = x; }
        return right >= left ? { left, right } : null;
    });
}
const targetUnion = Buffer.from(visible);
for (let p = 0; p < mask.length; p += 4) targetUnion[p + 3] = Math.max(visible[p + 3], mask[p + 3]);
const targetRows = spans(targetUnion, width, height, (data, p) => data[p + 3] > 0);
// The model returns a complete leg on green, not a pixel-aligned transparent patch.
// Fit its warm silhouette row-by-row into the guide, then take ONLY hidden pixels.
const modelRows = spans(model, modelInfo.width, modelInfo.height, (data, p) => data[p] > data[p + 1] + 3 && data[p + 1] > data[p + 2] + 3);
const extent = rows => [rows.findIndex(Boolean), rows.findLastIndex(Boolean)];
const [top, bottom] = extent(targetRows), [modelTop, modelBottom] = extent(modelRows);
if (modelTop < 0 || modelBottom - modelTop < modelInfo.height * .5) throw new Error('Generated leg silhouette missing');
let completed = 0, direct = 0;
for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const p = (y * width + x) * 4;
    if (visible[p + 3]) { provenance.set([99, 139, 115, 255], p); direct++; continue; }
    if (!mask[p + 3]) continue;
    if (original[p + 3] !== 255) throw new Error('Completion escaped opaque original coverage');
    const row = targetRows[y], sy = Math.round(modelTop + (y - top) / (bottom - top) * (modelBottom - modelTop));
    const modelRow = modelRows[sy];
    if (!modelRow) throw new Error(`Missing model scanline ${sy}`);
    const ratio = (x - row.left) / Math.max(1, row.right - row.left);
    const sx = Math.round(modelRow.left + ratio * (modelRow.right - modelRow.left));
    const sp = (sy * modelInfo.width + sx) * 4;
    model.copy(far, p, sp, sp + 3); far[p + 3] = mask[p + 3];
    // Match the generated side of the join to the locked original fur, never repaint the paw.
    let nearest = 121, weight = 0;
    const colour = [0, 0, 0];
    for (let dy = -10; dy <= 10; dy++) for (let dx = -10; dx <= 10; dx++) {
        const xx = x + dx, yy = y + dy, distance = dx * dx + dy * dy;
        if (xx < 0 || xx >= width || yy < 0 || yy >= height || distance > 100) continue;
        const vp = (yy * width + xx) * 4;
        if (visible[vp + 3] < 250) continue;
        nearest = Math.min(nearest, distance);
        const influence = Math.exp(-distance / 18); weight += influence;
        for (let channel = 0; channel < 3; channel++) colour[channel] += visible[vp + channel] * influence;
    }
    if (weight) {
        const blend = .95 * (1 - Math.sqrt(nearest) / 11);
        for (let channel = 0; channel < 3; channel++) far[p + channel] = Math.round(far[p + channel] * (1 - blend) + colour[channel] / weight * blend);
    }
    provenance.set([215, 132, 132, mask[p + 3]], p); completed++;
}
const save = (data, path) => sharp(data, { raw: { width, height, channels: 4 } }).png({ compressionLevel: 9 }).toFile(path);
await save(far, output); await save(provenance, `${folder}/ai-provenance.png`);
await sharp(output).extract({ left: 225, top: 330, width: 112, height: 165 }).resize({ height: 660 }).png().toFile(`${folder}/ai-leg-detail.png`);
await sharp({ create: { width, height, channels: 4, background: '#00000000' } }).composite([
    { input: output }, { input: 'public/pet-layers/foreground.png' },
]).png().toFile(`${folder}/ai-rest-reassembled.png`);
await sharp({ create: { width: width * 3, height, channels: 4, background: '#f0f6f1' } }).composite([
    { input: source, left: 0, top: 0 }, { input: output, left: width, top: 0 },
    { input: `${folder}/ai-provenance.png`, left: width * 2, top: 0 },
]).png().toFile(`${folder}/ai-review.png`);
const hash = async path => createHash('sha256').update(await readFile(path)).digest('hex');
await writeFile(`${folder}/ai-derivation.json`, JSON.stringify({ source, sourceSha256: await hash(source),
    model: `${generated}/master.png`, modelSha256: await hash(`${generated}/master.png`), output,
    width, height, directPixels: direct, generatedPixels: completed,
    method: 'Warm-colour model silhouette scanline-fit to original guide union; copy generated RGB only inside completion-mask; match generated join to nearby locked paw colours over 10px. Original visible RGBA copied verbatim. No model pixels used for foreground or visible paw.',
    targetExtent: [top, bottom], modelExtent: [modelTop, modelBottom],
    limitations: 'Post-decode compositing mask, not diffusion-level masked inpainting. Generated hidden anatomy is plausible invention, not recovered original pixels. One hind leg only.',
}, null, 2));
console.log({ directPixels: direct, generatedPixels: completed, output });
