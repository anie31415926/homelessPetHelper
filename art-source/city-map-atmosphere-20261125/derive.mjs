import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const output = process.argv[2];
assert(output, 'Usage: node derive.mjs <output PNG>');
const artwork = fileURLToPath(new URL('atmosphere.svg', import.meta.url));
const image = await sharp(artwork).png({ compressionLevel: 9 }).toFile(output);
assert.equal(image.width, 256);
assert.equal(image.height, 128);
assert(image.size <= 24 * 1024);
