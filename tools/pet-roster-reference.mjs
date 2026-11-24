import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {mkdir, readFile, readdir, writeFile} from 'node:fs/promises';
import {join, resolve, sep} from 'node:path';
import sharp from 'sharp';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const lerp = (a, b, t) => a + (b - a) * t;
function markBox(width, height) {
    const unit = Math.min(width, height);
    const left = width - Math.ceil(unit * .201), right = width - Math.floor(unit * .005);
    const top = height - Math.ceil(unit * .067), bottom = height - Math.floor(unit * .006);
    return {left, top, width: right - left, height: bottom - top};
}

async function learnTextMasks(inputs) {
    const groups = new Map();
    for (const {source} of inputs) {
        const {width, height} = await sharp(source).metadata(), key = `${width}x${height}`;
        const rect = markBox(width, height);
        if (!groups.has(key)) groups.set(key, {rect, n: 0, count: new Uint8Array(rect.width * rect.height)});
        const group = groups.get(key), w = rect.width, h = rect.height;
        const gray = await sharp(source).extract({...rect, height: h + 3}).greyscale().raw().toBuffer();
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            const paper = (gray[h * w + x] + gray[(h + 1) * w + x] + gray[(h + 2) * w + x]) / 3;
            if (paper - gray[y * w + x] >= 9) group.count[y * w + x]++;
        }
        group.n++;
    }
    for (const group of groups.values()) {
        const {rect: {width: w, height: h}, count, n} = group;
        assert.ok(n >= 3, 'A source format needs enough examples to isolate the common mark');
        const radius = Math.ceil(h * .032), mask = new Uint8Array(w * h);
        for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
            if (count[y * w + x] <= n / 2) continue;
            for (let v = Math.max(0, y - radius); v <= Math.min(h - 1, y + radius); v++) {
                mask.fill(1, v * w + Math.max(0, x - radius), v * w + Math.min(w, x + radius + 1));
            }
        }
        group.mask = mask;
        group.maskPixels = mask.reduce((a, b) => a + b, 0);
        assert.ok(group.maskPixels > w * h * .10 && group.maskPixels < w * h * .72, 'Unexpected text footprint');
    }
    return groups;
}

function repairPaper(rgb, width, {rect, mask}) {
    const {left, top, width: w, height: h} = rect;
    const stride = (w + 2) * 3, paper = new Float32Array((h + 2) * stride), targets = [];
    for (let y = -1; y <= h; y++) for (let x = -1; x <= w; x++) {
        const i = (y + 1) * stride + (x + 1) * 3, src = ((top + y) * width + left + x) * 3;
        const marked = x >= 0 && x < w && y >= 0 && y < h && mask[y * w + x];
        if (marked) targets.push([i, src]);
        for (let c = 0; c < 3; c++) paper[i + c] = marked
            ? lerp(rgb[src - (y + 1) * width * 3 + c], rgb[src + (h - y) * width * 3 + c], (y + 1) / (h + 1))
            : rgb[src + c];
    }
    // Extend the local background through the glyph without copying distant shadow down a column.
    for (let pass = 0; pass < 120; pass++) for (const [i] of targets) for (let c = i; c < i + 3; c++) {
        const mean = .25 * (paper[c - 3] + paper[c + 3] + paper[c - stride] + paper[c + stride]);
        paper[c] += 1.55 * (mean - paper[c]);
    }
    for (const [i, src] of targets) for (let c = 0; c < 3; c++) rgb[src + c] = Math.round(paper[i + c]);
}

function assertOutsideUnchanged(before, after, width, height, rect) {
    const {left, top, width: w, height: h} = rect;
    for (let y = 0; y < height; y++) {
        const a = y * width * 3, b = a + width * 3;
        const same = (lo, hi) => before.subarray(lo, hi).equals(after.subarray(lo, hi));
        if (y < top || y >= top + h) assert.ok(same(a, b));
        else assert.ok(same(a, a + left * 3) && same(a + (left + w) * 3, b));
    }
}

function filename(name, number) {
    return number ? `${name} (${number}).png` : `${name}.png`;
}

async function repair(source, destination, expectedInputHash, masks) {
    const bytes = await readFile(source);
    const original = await sharp(bytes).removeAlpha().toColourspace('srgb').raw().toBuffer({resolveWithObject: true});
    const {width, height, channels} = original.info;
    assert.equal(channels, 3);
    const group = masks.get(`${width}x${height}`);
    repairPaper(original.data, width, group);
    const region = group.rect;
    const data = await sharp(original.data, {raw: {width, height, channels}}).png().toBuffer();
    const decodedBefore = await sharp(bytes).removeAlpha().toColourspace('srgb').raw().toBuffer();
    const decodedAfter = await sharp(data).raw().toBuffer();
    assertOutsideUnchanged(decodedBefore, decodedAfter, width, height, region);
    for (let y = 0; y < region.height; y++) for (let x = 0; x < region.width; x++) {
        if (group.mask[y * region.width + x]) continue;
        const i = ((region.top + y) * width + region.left + x) * 3;
        assert.equal(decodedBefore.readUIntBE(i, 3), decodedAfter.readUIntBE(i, 3));
    }
    assert.ok(!decodedBefore.equals(decodedAfter), 'Expected target to contain a mark');
    assert.equal(sha256(bytes), expectedInputHash, 'Source changed during processing');
    await writeFile(destination, data);
    return {width, height, region, maskPixels: group.maskPixels, outputSha256: sha256(data)};
}

async function board(sheets, output) {
    const tiles = [];
    for (const [index, sheet] of sheets.entries()) {
        const x = index % 2 * 600, y = Math.floor(index / 2) * 226;
        const title = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="600" height="30"><text x="18" y="22" font-family="Segoe UI" font-size="17" fill="#294039">${sheet.id}</text></svg>`);
        tiles.push({input: title, left: x, top: y});
        for (const [path, left, width] of [[sheet.identityFile, 14, 178], [sheet.turnaroundFile, 204, 380]]) {
            tiles.push({input: await sharp(join(output, path)).resize(width, 184, {fit: 'contain', background: '#f7f7f2'}).png().toBuffer(), left: x + left, top: y + 32});
        }
    }
    await sharp({create: {width: 1200, height: Math.ceil(sheets.length / 2) * 226, channels: 3, background: '#f0f2ee'}})
        .composite(tiles).png().toFile(join(output, 'roster-board.png'));
}

async function main() {
    assert.ok(process.argv[2], 'Pass the source folder as the first argument');
    const source = resolve(process.argv[2]), output = resolve('test-results/pet-roster-20261124');
    assert.ok(source !== output && !source.startsWith(output + sep));
    const config = JSON.parse(await readFile('art-source/pet-roster-20261124/roster.json', 'utf8'));
    const inputs = [];
    for (const animal of config.animals) {
        const group = join(source, animal.folder);
        const names = (await readdir(group)).filter(name => name.endsWith('.png')).sort((a, b) => a.localeCompare(b, 'en', {numeric: true}));
        for (const name of names) {
            const relative = `${animal.folder}/${name}`;
            if (!config.outOfScope.includes(relative)) inputs.push({id: animal.id, name, relative, source: join(group, name)});
        }
    }
    const masks = await learnTextMasks(inputs);
    const files = [], sheets = [];
    for (const animal of config.animals) {
        const dest = join(output, 'cleaned', animal.folder), hashes = new Map();
        await mkdir(dest, {recursive: true});
        for (const {name, relative, source: input} of inputs.filter(input => input.id === animal.id)) {
            const inputSha256 = sha256(await readFile(input));
            const entry = await repair(input, join(dest, name), inputSha256, masks);
            const duplicateOf = hashes.get(inputSha256) ?? null;
            hashes.set(inputSha256, duplicateOf ?? relative);
            files.push({relative, id: animal.id, inputSha256, duplicateOf, ...entry});
        }
        const inCleaned = number => `cleaned/${animal.folder}/${filename(config.sourceName, number)}`;
        sheets.push({...animal, identityFile: inCleaned(animal.identity), turnaroundFile: inCleaned(animal.turnaround)});
    }
    await board(sheets, output);
    const result = {schemaVersion: 1, sourceLabel: config.sourceLabel, animals: sheets, files, outOfScope: config.outOfScope, verification: 'Decoded RGB unchanged outside each localized text mask, including the rest of the repair rectangle.'};
    await writeFile(join(output, 'manifest.json'), JSON.stringify(result, null, 2) + '\n');
    console.log(`PASS ${files.length} cleaned PNGs, ${sheets.length} animals, ${files.filter(file => file.duplicateOf).length} exact duplicate inputs; outside repair rectangles unchanged.`);
}

await main();
