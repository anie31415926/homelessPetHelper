import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import sharp from 'sharp';
import {resizeVariant, renderAtSize, reviewImages} from '../tools/image-size-preview.mjs';

const image = (width, height) => sharp({create: {width, height, channels: 4,
    background: {r: 80, g: 140, b: 210, alpha: .5}}}).png().toBuffer();

test('resize keeps aspect ratio and never enlarges or changes the input', async () => {
    const source = await image(354, 211);
    const original = Buffer.from(source);
    const smaller = await resizeVariant(source, 192);
    assert.deepEqual([smaller.width, smaller.height], [192, 114]);
    assert.equal(smaller.rgba32Bytes, 192 * 114 * 4);
    const noUpscale = await resizeVariant(source, 512);
    assert.deepEqual([noUpscale.width, noUpscale.height], [354, 211]);
    assert.deepEqual(source, original);
    assert.equal((await sharp(smaller.buffer).metadata()).isPalette, false);
});

test('invalid sizes and image data fail explicitly', async () => {
    const source = await image(10, 10);
    for (const edge of [0, -1, 1.5, NaN, 4097, '128'])
        await assert.rejects(resizeVariant(source, edge), /Invalid long edge/);
    await assert.rejects(resizeVariant(Buffer.from('not an image'), 128));
});

test('display comparison uses an identical footprint even for smaller sources', async () => {
    for (const edge of [64, 128, 256]) {
        const rendered = await renderAtSize(await image(edge, edge / 2), 224, '#ffffff');
        const metadata = await sharp(rendered).metadata();
        assert.deepEqual([metadata.width, metadata.height], [224, 112]);
        const rgba = await sharp(rendered).ensureAlpha().raw().toBuffer();
        assert.equal(rgba[3], 255);
    }
    const footprint = {width: 354, height: 211};
    const original = await renderAtSize(await image(354, 211), 224, '#ffffff', footprint);
    const candidate = await renderAtSize(await image(192, 114), 224, '#ffffff', footprint);
    const a = await sharp(original).metadata();
    const b = await sharp(candidate).metadata();
    assert.deepEqual([a.width, a.height], [b.width, b.height]);
});

test('transparent saturated neighbours do not tint a visible edge after resizing', async () => {
    const pixels = Buffer.alloc(64 * 64 * 4);
    for (let i = 0; i < 64 * 64; i++) pixels.set([0, 255, 0, 0], i * 4);
    for (let y = 16; y < 48; y++) for (let x = 16; x < 48; x++)
        pixels.set([255, 0, 0, 255], (y * 64 + x) * 4);
    const source = await sharp(pixels, {raw: {width: 64, height: 64, channels: 4}}).png().toBuffer();
    const variant = await resizeVariant(source, 32);
    const resized = await sharp(variant.buffer).raw().toBuffer();
    let semiTransparent = 0;
    for (let i = 0; i < resized.length; i += 4) {
        if (resized[i + 3] > 0 && resized[i + 3] < 255) {
            semiTransparent++;
            assert.equal(resized[i + 1], 0);
        }
    }
    assert.ok(semiTransparent > 0);
});

test('review writes previews only and reports provenance without modifying source PNGs', async t => {
    const root = await fs.mkdtemp(path.join(os.tmpdir(), 'image-size-preview-'));
    t.after(() => fs.rm(root, {recursive: true, force: true}));
    const art = path.join(root, 'unity/HomelessPetHelper/Assets/Art/CityMap');
    await fs.mkdir(path.join(art, 'Animals'), {recursive: true});
    await fs.mkdir(path.join(art, 'StorybookLandmarks'), {recursive: true});
    const source = await image(20, 20);
    const files = ['Animals/T_Animal_CatCalico.png', 'Animals/T_Animal_CatOrange.png',
        'Animals/T_Animal_DogWhite.png', 'Animals/T_Animal_Rabbit.png',
        'StorybookLandmarks/T_Storybook_CloudPark.png',
        'StorybookLandmarks/T_Storybook_FlowerGarden.png',
        'StorybookLandmarks/T_Storybook_Treehouse.png',
        'StorybookLandmarks/T_Storybook_Shelter.png'];
    for (const file of files) await fs.writeFile(path.join(art, file), source);
    const report = await reviewImages(root);
    assert.equal(report.images.length, 8);
    assert.match(report.toolSha256, /^[a-f0-9]{64}$/);
    assert.match(report.warning, /NOT Unity\/WeChat/);
    for (const entry of report.images) {
        assert.equal(entry.variants.length, 4);
        assert.ok(entry.variants.every(v => v.paletteCandidate.lossy));
        for (const variant of entry.variants)
            for (const colors of Object.values(variant.displayRmseBySize))
                assert.ok(Object.values(colors).every(Number.isFinite));
        assert.deepEqual(await fs.readFile(path.join(root, entry.sourcePath)), source);
    }
    const output = path.join(root, 'test-results/image-size-review');
    for (const file of ['small-icons.png', 'battlefield-icons.png', 'buildings.png'])
        assert.equal((await sharp(path.join(output, file)).metadata()).format, 'png');
    assert.equal(JSON.parse(await fs.readFile(path.join(output, 'report.json'), 'utf8')).images.length, 8);
});
