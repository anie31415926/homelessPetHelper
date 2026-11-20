import test from 'node:test';
import assert from 'node:assert/strict';
import sharp from 'sharp';
import {prepareAtlasImage} from '../tools/lib/atlas-padding.mjs';
import {packAtlasRegions, composeAtlas} from '../tools/lib/image-atlas.mjs';
const image = (bytes, width, height) => sharp(Buffer.from(bytes),
    {raw: {width, height, channels: 4}}).png().toBuffer();

test('edge-touching art gets a complete transparent gutter and reversible negative offsets', async () => {
    const rgba = [12, 34, 56, 255, 89, 10, 23, 127, 45, 67, 89, 1, 91, 82, 73, 255];
    const source = await image(rgba, 2, 2);
    const padded = await prepareAtlasImage(sharp, source);
    assert.deepEqual([padded.left, padded.top, padded.width, padded.height], [-2, -2, 6, 6]);
    const pixels = await sharp(padded.image).raw().toBuffer();
    for (let y = 0; y < 6; y++) for (let x = 0; x < 6; x++) {
        const actual = [...pixels.subarray((y * 6 + x) * 4, (y * 6 + x + 1) * 4)];
        assert.deepEqual(actual, x >= 2 && x < 4 && y >= 2 && y < 4
            ? rgba.slice(((y - 2) * 2 + x - 2) * 4, ((y - 2) * 2 + x - 1) * 4) : [0, 0, 0, 0]);
    }
});

test('existing transparent RGB and semi-transparent pixels survive cropping unchanged', async () => {
    const pixels = Buffer.alloc(9 * 9 * 4);
    pixels.set([20, 30, 40, 127], (4 * 9 + 4) * 4);
    pixels.set([50, 60, 70, 0], (3 * 9 + 3) * 4);
    const source = await image(pixels, 9, 9);
    const result = await prepareAtlasImage(sharp, source);
    assert.deepEqual([result.left, result.top, result.width, result.height], [2, 2, 5, 5]);
    assert.deepEqual(await sharp(result.image).raw().toBuffer(),
        await sharp(source).extract({left: 2, top: 2, width: 5, height: 5}).raw().toBuffer());
});

test('extrusion explicitly repeats edge texels without resizing the content', async () => {
    const result = await prepareAtlasImage(sharp, await image([13, 51, 73, 127], 1, 1),
        {padding: 2, trim: false, edge: 'extrude'});
    const pixels = await sharp(result.image).raw().toBuffer();
    for (let i = 0; i < pixels.length; i += 4) assert.deepEqual([...pixels.subarray(i, i + 4)], [13, 51, 73, 127]);
});

test('packed edge-touching red and green art have separate gutters at every content boundary', async () => {
    const images = new Map(), regions = [];
    for (const [id, color] of [['red', [255, 0, 0, 255]], ['green', [0, 255, 0, 255]]]) {
        const result = await prepareAtlasImage(sharp, await image(color, 1, 1));
        images.set(id, result.image);
        regions.push({id, width: result.width, height: result.height});
    }
    const layout = packAtlasRegions(regions, {maxSize: 16});
    const pixels = await sharp(await composeAtlas(sharp, layout, images)).raw().toBuffer();
    for (const r of layout.regions) for (let y = 0; y < r.height; y++) for (let x = 0; x < r.width; x++) {
        const alpha = pixels[((r.y + y) * layout.width + r.x + x) * 4 + 3];
        assert.equal(alpha, x === 2 && y === 2 ? 255 : 0);
    }
});

test('invalid padding, edge policies and empty alpha fail explicitly', async () => {
    const source = await image([1, 2, 3, 0], 1, 1);
    for (const options of [{padding: -1}, {padding: 1.5}, {padding: 65}, {edge: 'guess'}, {trim: 1}]) {
        await assert.rejects(prepareAtlasImage(sharp, source, options), /Invalid/);
    }
    await assert.rejects(prepareAtlasImage(sharp, source), /empty/);
    assert.equal((await prepareAtlasImage(sharp, source, {trim: false})).width, 5);
});
