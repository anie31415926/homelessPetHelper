import assert from 'node:assert/strict';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const [source, output] = process.argv.slice(2);
assert(source && output, 'Usage: node split-landmarks.mjs <painted JPEG> <output folder>');
const { width, height, landmarks } = JSON.parse(await readFile(new URL('landmarks.json', import.meta.url)));
await mkdir(output, { recursive: true });
const { data: pixels, info } = await sharp(source).removeAlpha().raw().toBuffer({ resolveWithObject: true });
assert.deepEqual([info.width, info.height, info.channels], [width, height, 3]);
const svg = body => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}">${body}</svg>`);
const pathMask = outline => sharp(svg(`<path fill="white" d="${outline}"/>`)).extractChannel(3).raw().toBuffer();
const layered = [];
const removals = new Uint8Array(width * height);
for (const { name, surface, outline } of landmarks) {
    const shape = await pathMask(outline);
    const { data: alpha } = await sharp(shape, { raw: { width, height, channels: 1 } })
        .blur(.65).raw({ depth: 'uchar' }).extractChannel(0).toBuffer({ resolveWithObject: true });
    let minX = width, minY = height, maxX = 0, maxY = 0;
    for (let i = 0; i < alpha.length; i++) {
        if (!alpha[i]) continue;
        const x = i % width, y = Math.floor(i / width);
        minX = Math.min(minX, x); minY = Math.min(minY, y);
        maxX = Math.max(maxX, x); maxY = Math.max(maxY, y);
        if (alpha[i] >= 15) {
            removals[i] = surface === 'river' ? 2 : 1;
        }
    }
    const box = { left: minX - 1, top: minY - 1, width: maxX - minX + 3, height: maxY - minY + 3 };
    const sprite = Buffer.alloc(box.width * box.height * 4);
    for (let y = 0; y < box.height; y++) for (let x = 0; x < box.width; x++) {
        const i = (box.top + y) * width + box.left + x, j = (y * box.width + x) * 4;
        pixels.copy(sprite, j, i * 3, i * 3 + 3);
        sprite[j + 3] = alpha[i];
    }
    const file = `T_Storybook_${name}.png`;
    await sharp(sprite, { raw: { width: box.width, height: box.height, channels: 4 } })
        .png({ palette: true, colours: 256, dither: 0 }).toFile(path.join(output, file));
    layered.push({ name, file, ...box });
}

for (const [outline, surface] of [
    ['M404 180 L431 178 475 201 476 232 442 235 412 218 Z', 1],
    ['M274 911 Q290 884 315 898 Q337 919 341 948 L328 959 282 960 273 934 Z', 1],
]) {
    const fill = await pathMask(outline);
    for (let i = 0; i < fill.length; i++) if (fill[i] >= 128) removals[i] = surface;
}
// Continue the surrounding washes through the cutouts. Grain is sampled from the same painting.
const background = Buffer.from(pixels);
const spots = [];
const filled = Uint8Array.from(removals, value => value ? 0 : 1);
let front = [];
const offsets = [-width, -1, 1, width];
for (let i = 0; i < removals.length; i++) if (removals[i]) {
    spots.push(i);
    if (offsets.some(offset => filled[i + offset])) front.push(i);
}
for (let distance = 1; front.length; distance++) {
    const next = [];
    for (const i of front) {
        const samples = offsets.map(offset => i + offset).filter(index => filled[index] && filled[index] < distance + 1);
        if (!samples.length) continue;
        for (let c = 0; c < 3; c++) background[i * 3 + c] = Math.round(
            samples.reduce((sum, index) => sum + background[index * 3 + c], 0) / samples.length);
        filled[i] = distance + 1;
    }
    for (const i of front) for (const offset of offsets) {
        const index = i + offset;
        if (!removals[index] || filled[index]) continue;
        filled[index] = 255;
        next.push(index);
    }
    front = next;
}
const continuation = await pathMask([
    'M347 239 Q382 204 431 207 Q469 210 511 197 Q550 185 607 164 L619 184 Q560 204 520 216 Q474 231 430 225 Q393 221 363 255 Z',
    'M477 383 Q489 354 516 320 Q537 304 553 315 Q531 322 522 344 Q511 365 511 390 Z',
    'M395 805 C361 832 335 850 320 879 C305 905 321 928 331 950 L298 964 C286 940 272 918 286 879 C298 848 325 826 356 805 Z',
    'M176 853 Q206 882 236 894 Q267 909 302 905 L313 928 Q261 936 221 915 Q189 893 157 862 Z',
].join(' '));
const mirror = (value, span) => span - Math.abs(value % (span * 2) - span);
const grass = [[437, 803, 43, 55], [419, 493, 37, 63], [390, 555, 50, 37]];
const smoothed = Float32Array.from(background);
for (let iteration = 0; iteration < 75; iteration++) for (const i of spots) {
    for (let c = 0; c < 3; c++) {
        const j = i * 3 + c;
        smoothed[j] = (smoothed[j - width * 3] + smoothed[j - 3] + smoothed[j + 3] + smoothed[j + width * 3]) / 4;
    }
}
for (const i of spots) {
    const x = i % width, y = Math.floor(i / width);
    const river = removals[i] === 2;
    const patch = river ? [62, 532, 23, 67] : grass[Math.floor((y + 30) / 161) % grass.length];
    const [px, py, spanX, spanY] = patch;
    const warpX = x + 49 + Math.floor(19 * Math.sin(y / 22));
    const warpY = y + 67 + Math.floor(15 * Math.sin(x / 26));
    const sample = ((py + mirror(warpY, spanY)) * width + px + mirror(warpX, spanX)) * 3;
    const weight = Math.min(1, (filled[i] - 1) / 10);
    const trail = continuation[i] / 255;
    for (let c = 0; c < 3; c++) {
        const texture = pixels[sample + c];
        const tint = (river ? [105, 202, 176] : [205, 219, 122])[c];
        const detail = Math.max(-16, Math.min(16, texture - tint));
        const wash = smoothed[i * 3 + c] * (1 - .72 * weight) + (tint + detail * .85) * .72 * weight;
        const clay = [250, 225, 161][c] + (texture - [204, 218, 123][c]) * .14;
        background[i * 3 + c] = Math.round(Math.max(0, Math.min(255, (1 - trail) * wash + trail * clay)));
    }
}
const ground = await sharp(background, { raw: { width, height, channels: 3 } })
    .jpeg({ quality: 86, mozjpeg: true }).toBuffer();
await writeFile(path.join(output, 'T_CityMap_Neighborhood.jpg'), ground);
await writeFile(path.join(output, 'placement.json'), JSON.stringify({ width, height, layered }, null, 2) + '\n');
await sharp(ground).composite(layered.map(({ file, left, top }) => ({ input: path.join(output, file), left, top })))
    .png().toFile(path.join(output, 'reassembled.png'));
