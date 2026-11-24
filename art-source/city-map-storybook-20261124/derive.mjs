import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const [masters, target] = process.argv.slice(2);
assert(masters && target, 'Usage: node derive.mjs <masters folder> <runtime JPEG>');
const source = name => path.join(masters, name + '_00001_.png');
const bounds = { left: 435, top: 100, width: 180, height: 148 };
const mask = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="180" height="148" viewBox="0 0 360 296">
<defs><filter id="soft"><feGaussianBlur stdDeviation="1.1"/></filter></defs>
<polygon fill="white" filter="url(#soft)" points="116,157 139,140 186,126 219,144 220,175
252,164 262,181 232,194 196,215 155,254 119,260 72,244 74,228 109,212 119,198"/></svg>`);
const cart = await sharp(source('storybook')).extract(bounds).ensureAlpha().composite([
    { input: mask, blend: 'dest-in' },
]).png().toBuffer();
// Pull the restored cart and rails back toward the cave, clear of the windmill blades.
const painted = await sharp(source('refined')).composite([
    { input: cart, left: bounds.left, top: bounds.top - 16 },
    { input: fileURLToPath(new URL('factory-sign.svg', import.meta.url)), left: 523, top: 718 },
]).png().toBuffer();
const image = sharp(painted).resize({ height: 1024 });
const result = await image.jpeg({ quality: 86, mozjpeg: true }).toFile(target);
assert.equal(result.width, 673);
assert.equal(result.height, 1024);
assert(result.size < 204800);
console.log(JSON.stringify(result));
