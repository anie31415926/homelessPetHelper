import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const selection = process.argv[2] || 'side';
if (!['side', 'sit'].includes(selection)) throw new Error('Unknown complete pose');
const base = `art-source/pet-motion/${selection}-rest`, source = selection === 'side' ? `${base}/v2/master.png` : `${base}/master.png`;
const output = `public/pet-motion/${selection}-rest.png`, target = selection === 'sit' ? { width: 430, height: 450 } : { width: 410, height: 260 };
const { data, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const { width, height } = info, seen = new Uint8Array(width * height), queue = new Int32Array(width * height);
let read = 0, end = 0;
const chroma = i => Math.min(data[i * 4 + 1] - data[i * 4], data[i * 4 + 2] - data[i * 4]);
const add = i => { if (!seen[i] && chroma(i) > 6) { seen[i] = 1; queue[end++] = i; } };
for (let x = 0; x < width; x++) { add(x); add((height - 1) * width + x); }
for (let y = 0; y < height; y++) { add(y * width); add(y * width + width - 1); }
// Cyan enclosed by the curled tail is background too; seed strongly chromatic holes.
for (let i = 0; i < seen.length; i++) if (chroma(i) > 40) add(i);
while (read < end) {
    const i = queue[read++], x = i % width, y = Math.floor(i / width);
    if (x) add(i - 1); if (x + 1 < width) add(i + 1);
    if (y) add(i - width); if (y + 1 < height) add(i + width);
}
let left = width, top = height, right = 0, bottom = 0;
for (let i = 0; i < seen.length; i++) {
    const p = i * 4;
    if (seen[i]) {
        const t = Math.max(0, Math.min(1, (chroma(i) - 6) / 34)), a = 1 - t * t * (3 - 2 * t);
        data[p + 3] = Math.round(a * 255);
        // Remove cyan spill from the narrow semitransparent contour, not the white interior.
        if (a > 0 && a < 1) for (let c = 0; c < 3; c++) data[p + c] = Math.max(0, Math.min(255, (data[p + c] - (1 - a) * [175, 231, 232][c]) / a));
    }
    if (data[p + 3] > 3) {
        left = Math.min(left, i % width); right = Math.max(right, i % width);
        top = Math.min(top, Math.floor(i / width)); bottom = Math.max(bottom, Math.floor(i / width));
    }
}
const crop = { left, top, width: right - left + 1, height: bottom - top + 1 };
const cutout = await sharp(data, { raw: { width, height, channels: 4 } }).extract(crop).resize({ ...target, fit: 'inside' }).png().toBuffer();
const size = await sharp(cutout).metadata(), placement = { left: Math.floor((452 - size.width) / 2), top: 498 - size.height };
await sharp({ create: { width: 452, height: 512, channels: 4, background: '#00000000' } }).composite([{ input: cutout, ...placement }]).png({ compressionLevel: 9 }).toFile(output);
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#34433e']]) {
    await sharp({ create: { width: 452, height: 512, channels: 4, background } }).composite([{ input: output }]).png().toFile(`${base}/${name}.png`);
}
await writeFile(`${base}/derivation.json`, JSON.stringify({
    source, sourceSha256: createHash('sha256').update(await readFile(source)).digest('hex'), crop, placement, scaled: { width: size.width, height: size.height }, output,
    target,
    operation: 'Cyan chroma matte: boundary plus strong-chroma>40 enclosed seeds, flood min(G-R,B-R)>6, smooth alpha6..40; narrow edge RGB decontamination against175/231/232. Whole animal cropped uniformly to target then placed on452x512, bottom498. No detached parts, no original standing-image overwrite.',
}, null, 2));
console.log(JSON.stringify({ crop, size: [size.width, size.height], placement }));
