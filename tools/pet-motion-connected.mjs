import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/pet-motion', source = 'public/pet-mesh/cat-side.png';
const width = 452, height = 512, raw = { width, height, channels: 4 };
const read = async path => {
    const { data, info } = await sharp(path).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    if (info.width !== width || info.height !== height) throw new Error(`Source coordinates changed: ${path}`);
    return data;
};
const original = await read(source), bodyMask = await read(`${folder}/body-mask.png`);
const hindMask = await read('art-source/pet-limbs/donor/hind-mask.png');
const repairedBody = await read('public/pet-motion/body.png');
const connected = Buffer.from(original), mask = Buffer.alloc(original.length);
for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const p = (y * width + x) * 4, alpha = Math.max(bodyMask[p + 3], hindMask[p + 3]);
    mask.set([255, 255, 255, alpha], p);
    connected[p + 3] = Math.round(original[p + 3] * alpha / 255);
    // Only the removed front leg needs seam repair; keep the real connected thigh line.
    if (x >= 171 && x <= 197 && y >= 372 && y <= 434) repairedBody.copy(connected, p, p, p + 3);
}
await sharp(connected, { raw }).png({ compressionLevel: 9 }).toFile('public/pet-motion/lie-connected.png');
await sharp(mask, { raw }).png({ compressionLevel: 9 }).toFile(`${folder}/connected-mask.png`);
for (const [name, background] of [['light', '#f0f6f1'], ['dark', '#385453']]) {
    await sharp(connected, { raw }).flatten({ background }).png().toFile(`${folder}/connected-${name}.png`);
}
await writeFile(`${folder}/connected-derivation.json`, JSON.stringify({
    source, sourceSha256: createHash('sha256').update(await readFile(source)).digest('hex'),
    alpha: 'original alpha * max(body-mask alpha, donor/hind-mask alpha) / 255',
    rgb: 'original RGB, except existing front seam repair in x171..197 y372..434; no hind repair or root alpha fade',
    masks: [`${folder}/body-mask.png`, 'art-source/pet-limbs/donor/hind-mask.png'],
    method: 'One uncut body/hip/hind texture for a shared indexed skin. No generation, original or accepted animation texture mutation.',
}, null, 2));
console.log('Prepared connected lying body/hip/hind from original pixels.');
