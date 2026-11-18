import { createRequire } from 'node:module';
import { access, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
    { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const source = 'art-source/rescue-layout-b';
if (await access(`${source}/clean-master.png`).then(() => true, () => false)) {
    throw new Error('Refusing to overwrite a processed candidate.');
}
const { data, info } = await sharp(`${source}/master.png`).removeAlpha().raw().toBuffer({ resolveWithObject: true });
if (info.width !== 768 || info.height !== 768 || info.channels !== 3) {
    throw new Error('Review patch coordinates before processing a different source size.');
}
const result = Buffer.from(data);
const patch = { x: 290, y: 335, width: 230, height: 230, sourceX: 42, feather: 18 };
// Clone the unobstructed pavement at the same height, feathering only the boundary.
for (let y = 0; y < patch.height; y++) {
    for (let x = 0; x < patch.width; x++) {
        const edge = Math.min(x, y, patch.width - 1 - x, patch.height - 1 - y);
        const blend = Math.min(1, edge / patch.feather);
        const target = ((patch.y + y) * info.width + patch.x + x) * 3;
        const from = ((patch.y + y) * info.width + patch.sourceX + x) * 3;
        for (let channel = 0; channel < 3; channel++) {
            result[target + channel] = Math.round(data[target + channel] * (1 - blend) + data[from + channel] * blend);
        }
    }
}
await sharp(result, { raw: info }).png().toFile(`${source}/clean-master.png`);
await sharp(result, { raw: info }).jpeg({ quality: 84, mozjpeg: true }).toFile(`${source}/clean-backdrop.jpg`);
await writeFile(`${source}/postprocess.json`, JSON.stringify({ source: 'master.png', output: 'clean-master.png',
    operation: 'Clone empty pavement from the left at identical y, feather boundary, no landmark relocation or enlargement.',
    patch, jpeg: { width: 768, height: 768, quality: 84, mozjpeg: true } }, null, 2));
console.log('Removed baked animal with a recorded local pavement patch.');
