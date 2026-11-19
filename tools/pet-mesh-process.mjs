import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const source = 'art-source/pet-rig/turnaround-master.png';
const crop = { left: 660, top: 60, width: 600, height: 680 };
await mkdir('public/pet-mesh', { recursive: true });
await mkdir('art-source/pet-mesh', { recursive: true });
const { data, info } = await sharp(source).extract(crop).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const { width: w, height: h } = info;
const seen = new Uint8Array(w * h), queue = new Int32Array(w * h);
let read = 0, end = 0;
const add = i => {
    if (seen[i]) return;
    const offset = i * 4;
    if (Math.min(data[offset], data[offset + 1], data[offset + 2]) < 243) return;
    seen[i] = 1; queue[end++] = i;
};
for (let x = 0; x < w; x++) { add(x); add((h - 1) * w + x); }
for (let y = 0; y < h; y++) { add(y * w); add(y * w + w - 1); }
while (read < end) {
    const i = queue[read++], x = i % w, y = Math.floor(i / w);
    if (x) add(i - 1); if (x + 1 < w) add(i + 1);
    if (y) add(i - w); if (y + 1 < h) add(i + w);
}
for (let i = 0; i < seen.length; i++) data[i * 4 + 3] = seen[i] ? 0 : 255;
await sharp(data, { raw: { width: w, height: h, channels: 4 } }).resize({ height: 512 })
    .png({ compressionLevel: 9, palette: true, colours: 256, dither: .5, effort: 10 }).toFile('public/pet-mesh/cat-side.png');
await sharp(source).extract(crop).resize({ height: 512 }).jpeg({ quality: 90 }).toFile('art-source/pet-mesh/source-crop.jpg');
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#34433e']]) {
    await sharp({ create: { width: 452, height: 512, channels: 4, background } }).composite([{ input: 'public/pet-mesh/cat-side.png', gravity: 'center' }])
        .png().toFile(`art-source/pet-mesh/edge-${name}.png`);
}
await writeFile('art-source/pet-mesh/derivation.json', JSON.stringify({
    source, sourceSha256: createHash('sha256').update(await readFile(source)).digest('hex'), crop,
    operation: 'One whole animal crop. Boundary-connected min RGB >=243 background removal, opacity normalized, uniform resize to height512, PNG256/dither0.5. No part cutting, compositing, inpainting, new generation or relative pixel relocation.',
    output: 'public/pet-mesh/cat-side.png',
}, null, 2));
console.log('Derived one whole-animal texture without splitting or regenerating parts.');
