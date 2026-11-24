import assert from 'node:assert/strict';
import sharp from 'sharp';
import { fileURLToPath } from 'node:url';

const [reference, target, master = fileURLToPath(new URL('comfy/valley_00001_.png', import.meta.url))] =
    process.argv.slice(2);
assert(reference && target, 'Usage: node derive-terrain.mjs <reference> <target> [master]');
const { xmpAsString } = await sharp(reference).metadata();
assert(xmpAsString?.includes('TC260:AIGC'));
const result = await sharp(master).resize({ height: 1024 }).withXmp(xmpAsString)
    .jpeg({ quality: 82, mozjpeg: true }).toFile(target);
assert(result.size < 204800);
console.log(result);
