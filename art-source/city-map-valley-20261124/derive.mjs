import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const out = name => fileURLToPath(new URL(name, import.meta.url));
const [reference, runtime = out('T_CityMap_Valley.jpg')] = process.argv.slice(2);
assert(reference, 'Usage: node derive.mjs <original PNG> [runtime JPG]');
const source = sharp(reference);
const { width, height, xmpAsString } = await source.metadata();
assert.equal(width, 1686);
assert.equal(height, 2486);
assert(xmpAsString?.includes('TC260:AIGC'), 'Expected the original AI provenance metadata');
const original = await source.removeAlpha().raw().toBuffer();
const pixels = Buffer.from(original);
const region = { left: 1365, top: 2376, right: 1673, bottom: 2470 };
const feather = 18;

// Blend neighboring forest texture into the corner, leaving the rest of the painting untouched.
for (let y = region.top; y < region.bottom; y++) {
    for (let x = region.left; x < region.right; x++) {
        const edge = Math.min(x - region.left, region.right - 1 - x, y - region.top, region.bottom - 1 - y);
        const t = Math.min(1, edge / feather);
        const blend = t * t * (3 - 2 * t);
        const index = (y * width + x) * 3;
        const donor = ((y - 118) * width + x - 12) * 3;
        for (let c = 0; c < 3; c++) pixels[index + c] = Math.round(
            original[index + c] * (1 - blend) + original[donor + c] * blend);
    }
}

const repaired = sharp(pixels, { raw: { width, height, channels: 3 } }).withXmp(xmpAsString);
const { size } = await repaired.resize({ height: 1024 }).jpeg({
    quality: 83, mozjpeg: true,
}).toFile(runtime);
const actual = await sharp(runtime).metadata();
assert.equal(actual.height, 1024);
assert.equal(actual.xmpAsString, xmpAsString);
assert(size < 204800, 'Runtime JPEG exceeds the map terrain planning line');
console.log(JSON.stringify({ originalSha256: createHash('sha256').update(await readFile(reference)).digest('hex'),
    region, width: actual.width, height: actual.height, size }, null, 2));
