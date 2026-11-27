import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { chromium, expect } from '@playwright/test';
import sharp from 'sharp';
import { advance, createPatches, move, settle } from '../public/pet-fur-flaps/model.js';
import { distribute, maxPatches, patchCount, validateProfile } from '../public/pet-fur-flaps/profile.js';
import { buildEdgeTexture, edgeRows, edgeWidth } from '../public/pet-fur-flaps/fur-edge.js';

const origin = process.env.PET_FUR_URL || 'http://127.0.0.1:5197';
const url = `${origin}/pet-fur-flaps/index.html`;
const output = new URL('../test-results/pet-fur-flaps/', import.meta.url);
const files = ['index.html', 'style.css', 'main.js', 'model.js', 'shader.js', 'profile.js', 'editor.js', 'doudou.json', 'fur-edge.js'].map(file => `public/pet-fur-flaps/${file}`)
    .concat(['public/pet-fur/style.css', 'public/pet-affection/shiba.webp', 'tests/pet-fur-flaps-browser.mjs']);
async function fingerprint() {
    return Object.fromEntries(await Promise.all(files.map(async file => [file,
        createHash('sha256').update(await readFile(new URL(`../${file}`, import.meta.url))).digest('hex'),
    ])));
}
const before = await fingerprint();
const edge = buildEdgeTexture();
assert.equal(edge.length, edgeRows * edgeWidth * 2);
for (let row = 0; row < edgeRows; row++) {
    const strip = edge.subarray(row * edgeWidth * 2, (row + 1) * edgeWidth * 2).filter((_, index) => index % 2 === 0);
    assert.ok(Math.max(...strip) - Math.min(...strip) > 115);
    if (row) assert.notDeepEqual(strip, edge.subarray((row - 1) * edgeWidth * 2, row * edgeWidth * 2).filter((_, index) => index % 2 === 0));
}
const save = (name, data) => writeFile(new URL(name, output), data);
const settings = validateProfile(JSON.parse(await readFile(new URL('../public/pet-fur-flaps/doudou.json', import.meta.url), 'utf8')));
const configure = (changes = {}) => ({ ...settings.regions[1], x: 146, y: 249, angle: 100, columns: 1, layers: 1,
    length: 63, patchWidth: 64, spread: 0, jitter: 0, arc: 0, stagger: 0, ...changes });
const profile = createPatches([settings.regions[0], configure()]);
const forehead = profile.find(patch => patch.region === 'forehead');
const chest = profile.find(patch => patch.region === 'chest');
assert.ok(profile.every(patch => patch.lift > .35));
const defaultCount = patchCount(settings.regions);
const chestConfig = settings.regions.find(region => region.id === 'chest');
assert.equal(defaultCount, 62);
assert.deepEqual(distribute(settings.regions), distribute(settings.regions));
const withLayers = distribute([configure({ columns: 2, layers: 2, depth: 22, undercoat: .6 })]);
assert.equal(withLayers.length, 4);
assert.ok(withLayers[0].length < withLayers[2].length && withLayers[0].y > withLayers[2].y);
const withStagger = distribute([configure({ columns: 2, layers: 2, depth: 0, stagger: 1 })]);
assert.notEqual(withStagger[0].x, withStagger[2].x);
assert.throws(() => validateProfile({ ...settings, regions: [configure({ teeth: 19 })] }), /region/);
assert.throws(() => validateProfile({ ...settings, regions: [configure({ warmthRange: [.4, .1] })] }), /region/);
assert.throws(() => validateProfile({ ...settings, regions: [configure(), configure()] }), /region/);
assert.throws(() => validateProfile({ ...settings, regions: settings.regions.map(r => ({ ...r, columns: 9, layers: 5 })) }), /budget/);
assert.equal(move(profile, 'brush', 135, 311, 146, 249), false);
assert.equal(chest.roughness, 1);
assert.ok(move(profile, 'brush', 146, 249, 135, 311));
assert.ok(chest.roughness > 0 && chest.roughness < 1);
assert.equal(forehead.roughness, 1);
assert.ok(move(profile, 'brush', 146, 249, 135, 311));
assert.equal(chest.roughness, 0);
assert.ok(move(profile, 'brush', 135, 311, 146, 249));
assert.ok(chest.roughness > 0 && chest.roughness < 1);
assert.equal(forehead.roughness, 1);
assert.ok(move(profile, 'brush', 146, 249, 135, 311));
assert.equal(chest.roughness, 0);
settle(profile, 'pet');
assert.ok(profile.every(patch => patch.lift === 0));
assert.ok(move(profile, 'pet', 135, 311, 146, 249));
assert.ok(advance(profile, 'pet', 70, false));
assert.ok(chest.lift > .35);
for (let i = 0; i < 100; i++) advance(profile, 'pet', 16, false);
assert.equal(chest.lift, 0);
settle(profile, 'brush');
assert.equal(chest.lift, 0);
settle(profile, 'brush', true);
assert.equal(chest.lift, chest.initial);
assert.ok(profile.some(patch => patch.curl > .2));

const back = createPatches(settings.regions.filter(region => region.id === 'flank'));
assert.ok(back.length > 0 && back.every(patch => patch.gx > .90 && patch.gy < 0), 'back fur points toward the tail');
assert.equal(move(back, 'brush', 324, 278, 324, 221), false, 'upward brush no longer follows the back fur');
assert.ok(move(back, 'brush', 307, 273, 363, 258), 'shoulder to tail smooths the back');
assert.ok(move(back, 'brush', 363, 258, 307, 273), 'tail to shoulder lifts the back');
const ruff = createPatches(settings.regions.filter(region => region.id === 'ruff-edge'));
const single = ruff[2];
assert.equal(ruff.length, 8);
assert.ok(move(ruff, 'brush', single.x, single.y, single.x + single.gx * 18, single.y + single.gy * 18));
assert.deepEqual(ruff.filter(patch => patch.roughness < 1).map(patch => patch.id), [single.id], 'a narrow stroke can move just one outer tuft');

const petA = createPatches(settings.regions);
const petB = createPatches(settings.regions);
settle(petA, 'pet');
settle(petB, 'pet');
assert.ok(move(petA, 'pet', 161, 331, 161, 243));
for (let y = 331; y > 243; y -= 4) move(petB, 'pet', 161, y, 161, y - 4);
assert.ok(petA.every((patch, i) => Math.abs(patch.flick - petB[i].flick) < 1e-6));
const petTrail = petA.filter(patch => patch.flick >= .10);
assert.ok(petTrail.length >= 8 && petTrail.every(patch => patch.region !== 'forehead' && patch.region !== 'flank'));
assert.ok(Math.max(...petTrail.map(patch => patch.linger)) - Math.min(...petTrail.map(patch => patch.linger)) > 12);
assert.ok(advance(petA, 'pet', 16, false));
assert.ok(petTrail.every(patch => patch.lift > 0 && patch.lift < patch.initial), 'the first frame never jumps to maximum lift');
for (let i = 0; i < 130; i++) advance(petA, 'pet', 16, false);
assert.ok(petA.every(patch => patch.lift === 0 && patch.roughness === 1), 'the trail settles without changing the combed pose');

await mkdir(output, { recursive: true });
const flags = ['--enable-webgl', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'];
async function launch(args) {
    try { return await chromium.launch({ args }); }
    catch (error) {
        if (!error.message.includes("Executable doesn't exist")) throw error;
        return chromium.launch({ channel: 'msedge', args });
    }
}

const bad = [];
const report = { layout: [], trailTufts: petTrail.length };
const button = (page, name) => page.getByRole('button', { name, exact: true });
const canvas = page => page.locator('canvas');
const box = page => canvas(page).boundingBox();
const take = page => page.locator('.portrait').screenshot();
const at = (bounds, x, y) => ({ x: bounds.x + (x - 70) * bounds.width / 400, y: bounds.y + (y - 33) * bounds.height / 470 });

async function open(page, state = 'ready') {
    page.on('pageerror', error => bad.push(error.message));
    page.on('response', response => { if (response.status() >= 400) bad.push(response.url()); });
    await page.goto(url);
    await expect(page.locator('main')).toHaveAttribute('data-ready', state);
}

async function swipe(page, [x0, y0], [x1, y1], keepDown = false) {
    const bounds = await box(page);
    const start = at(bounds, x0, y0);
    const end = at(bounds, x1, y1);
    await page.mouse.move(start.x, start.y);
    await page.mouse.down();
    await page.mouse.move(end.x, end.y, { steps: 5 });
    if (!keepDown) await page.mouse.up();
}

async function changed(before, after, rect = [0, 0, 400, 470], threshold = 18) {
    const { data: a, info } = await sharp(before).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const b = await sharp(after).removeAlpha().raw().toBuffer();
    assert.equal(a.length, b.length);
    const [x0, y0, x1, y1] = rect;
    let total = 0;
    for (let y = Math.ceil(y0 * info.height / 470); y < Math.floor(y1 * info.height / 470); y++) {
        for (let x = Math.ceil(x0 * info.width / 400); x < Math.floor(x1 * info.width / 400); x++) {
            const i = (y * info.width + x) * info.channels;
            total += Math.abs(a[i] - b[i]) + Math.abs(a[i + 1] - b[i + 1]) + Math.abs(a[i + 2] - b[i + 2]) > threshold;
        }
    }
    return total;
}

const chestLeft = [48, 232, 103, 277];
const outerChest = [25, 231, 53, 269];
const upperHead = [81, 56, 136, 98];
const faceBand = [38, 111, 221, 194];
const feetBand = [0, 377, 400, 470];
const root = [146, 249];
const tip = [135, 311];
const browser = await launch(flags);

async function measureLight(page) {
    return page.evaluate(async () => {
        const { makeRenderer } = await import('./shader.js');
        const texture = document.createElement('canvas');
        texture.width = texture.height = 512;
        const paint = texture.getContext('2d');
        paint.fillStyle = 'white';
        paint.fillRect(0, 0, 512, 512);
        function probe(angle, teeth = 7, shadow = 1, seed = .08, toothDepth = .20) {
            const target = document.createElement('canvas');
            target.width = 400;
            target.height = 470;
            const renderer = makeRenderer(target, texture, { x: 70, y: 33, width: 400, height: 470 }, 1);
            const gl = target.getContext('webgl');
            renderer.setLight({ angle, shadow });
            renderer.draw([{ x: 250, y: 170, gx: 0, gy: 1, width: 100, length: 80, lift: .85, curl: .35,
                teeth, toothDepth, seed }]);
            const pixels = new Uint8Array(400 * 470 * 4);
            gl.readPixels(0, 0, 400, 470, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
            let mass = 0, moment = 0, peak = 0, signature = 0;
            for (let y = 157; y < 217; y++) {
                for (let x = 130; x < 230; x++) {
                    const i = ((469 - y) * 400 + x) * 4;
                    signature += pixels[i + 3] * (x + y * 2);
                    const loss = (pixels[i + 3] - pixels[i]) / 255;
                    if (loss < .055) continue;
                    peak = Math.max(peak, loss);
                    mass += loss;
                    moment += x * loss;
                }
            }
            gl.getExtension('WEBGL_lose_context')?.loseContext();
            return { mass, x: moment / mass, peak, signature };
        }
        return { right: probe(37), left: probe(143), away: probe(90), toward: probe(-90),
            moreTeeth: probe(90, 12), noShadow: probe(90, 7, 0),
            otherEdge: probe(90, 7, 1, .33), shallowEdge: probe(90, 7, 1, .08, .02) };
    });
}

async function measureTextureAlignment(page) {
    return page.evaluate(async () => {
        const { makeRenderer } = await import('./shader.js');
        const source = document.createElement('canvas');
        source.width = 512;
        source.height = 384;
        const paint = source.getContext('2d');
        const colors = ['#f22521', '#20df39', '#2725eb'];
        const tile = 12;
        const label = (x, y) => (Math.floor(x / tile) + Math.floor(y / tile) * 2) % colors.length;
        for (let y = 0; y < source.height; y += tile) {
            for (let x = 0; x < source.width; x += tile) {
                paint.fillStyle = colors[label(x, y)];
                paint.fillRect(x, y, tile, tile);
            }
        }
        const crop = { x: 57, y: 29, width: 320, height: 300 };
        const target = document.createElement('canvas');
        target.width = crop.width;
        target.height = crop.height;
        const renderer = makeRenderer(target, source, crop, 1);
        const gl = target.getContext('webgl');
        const pixels = new Uint8Array(crop.width * crop.height * 4);
        return [90, -124].flatMap(angle => [.45, .92].map(lift => {
            const radians = angle * Math.PI / 180;
            renderer.setLight({ angle, shadow: 0 });
            renderer.draw([{ x: 223, y: 167, gx: Math.cos(radians), gy: Math.sin(radians), width: 84,
                length: 96, lift, curl: .71, teeth: 7, toothDepth: .20, seed: .38 }]);
            gl.readPixels(0, 0, crop.width, crop.height, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
            let checked = 0, mismatches = 0;
            for (let y = 0; y < crop.height; y++) {
                for (let x = 0; x < crop.width; x++) {
                    const sx = crop.x + x, sy = crop.y + y;
                    const i = ((crop.height - 1 - y) * crop.width + x) * 4;
                    if (pixels[i + 3] < 245 || sx % tile < 1 || sx % tile >= tile - 1 ||
                        sy % tile < 1 || sy % tile >= tile - 1) continue;
                    const channel = label(sx, sy);
                    checked++;
                    if (pixels[i + channel] <= Math.max(pixels[i + (channel + 1) % 3], pixels[i + (channel + 2) % 3])) mismatches++;
                }
            }
            return { angle, lift, checked, mismatches };
        }));
    });
}

async function measureCoatBoundary(page) {
    return page.evaluate(async () => {
        const { makeRenderer } = await import('./shader.js');
        const source = document.createElement('canvas');
        source.width = 256;
        source.height = 128;
        const paint = source.getContext('2d');
        paint.fillStyle = '#fff3e7';
        paint.fillRect(0, 0, 128, 128);
        paint.fillStyle = '#dc985b';
        paint.fillRect(128, 0, 128, 128);
        const target = document.createElement('canvas');
        target.width = source.width;
        target.height = source.height;
        const renderer = makeRenderer(target, source, { x: 0, y: 0, width: 256, height: 128 }, 1);
        const gl = target.getContext('webgl');
        const pixels = new Uint8Array(256 * 128 * 4);
        renderer.setLight({ angle: 53, shadow: .76 });
        function probe(warmthRange) {
            renderer.draw([{ x: 128, y: 20, gx: 0, gy: 1, width: 150, length: 86, lift: .86, curl: .72,
                teeth: 7, toothDepth: .2, seed: .38, warmthRange }]);
            gl.readPixels(0, 0, 256, 128, gl.RGBA, gl.UNSIGNED_BYTE, pixels);
            let white = 0, brown = 0;
            for (let y = 0; y < 128; y++) {
                for (let x = 0; x < 256; x++) {
                    if (pixels[(y * 256 + x) * 4 + 3] <= 2) continue;
                    if (x < 128) white++;
                    else brown++;
                }
            }
            return { white, brown };
        }
        return { chest: probe([0, .17]), back: probe([.33, 1.6]), unrestricted: probe() };
    });
}

try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await page.addInitScript(() => {
        const original = WebGLRenderingContext.prototype.drawArrays;
        window.draws = [];
        WebGLRenderingContext.prototype.drawArrays = function (...args) {
            window.draws.push(args[2]);
            return original.apply(this, args);
        };
    });
    await open(page);
    report.coatBoundary = await measureCoatBoundary(page);
    assert.ok(report.coatBoundary.chest.white > 100 && report.coatBoundary.back.brown > 100);
    assert.equal(report.coatBoundary.chest.brown, 0);
    assert.equal(report.coatBoundary.back.white, 0);
    assert.ok(report.coatBoundary.unrestricted.white > 100 && report.coatBoundary.unrestricted.brown > 100);
    report.textureAlignment = await measureTextureAlignment(page);
    for (const sample of report.textureAlignment) {
        assert.ok(sample.checked > 200);
        assert.equal(sample.mismatches, 0, `raised tuft must sample the visible portrait coordinates: ${JSON.stringify(sample)}`);
    }
    report.lighting = await measureLight(page);
    const { right, left, away, toward } = report.lighting;
    assert.ok(right.x > left.x + 3, `turning the light moves the cast shadow: ${JSON.stringify(report.lighting)}`);
    assert.ok(away.mass > toward.mass * 1.7, 'toward-light flap casts less visible shadow');
    assert.ok(away.peak < .32 && away.peak > .12, 'soft shadow never approaches black');
    assert.notEqual(away.signature, report.lighting.moreTeeth.signature);
    assert.notEqual(away.signature, report.lighting.otherEdge.signature);
    assert.notEqual(away.signature, report.lighting.shallowEdge.signature);
    assert.ok(report.lighting.noShadow.mass < away.mass * .08, 'turning off the cast shadow leaves only faint painted marks');
    for (const [width, height] of [[1280, 900], [390, 844], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        const metrics = await page.evaluate(() => ({
            horizontal: document.documentElement.scrollWidth > innerWidth,
            footer: document.querySelector('footer').getBoundingClientRect().bottom <= innerHeight + 1,
            clip: [...document.querySelectorAll('button')].some(el => el.scrollWidth > el.clientWidth + 1),
            canvasWidth: document.querySelector('canvas').getBoundingClientRect().width,
        }));
        assert.ok(!metrics.horizontal && metrics.footer && !metrics.clip && metrics.canvasWidth >= 206);
        report.layout.push({ width, height, ...metrics });
        await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
        await save(`brush-${width}x${height}.png`, await page.screenshot());
    }
    await page.setViewportSize({ width: 390, height: 844 });
    const rough = await take(page);
    await button(page, '抚摸').click();
    const flat = await take(page);
    report.initialChest = await changed(flat, rough, chestLeft);
    assert.ok(report.initialChest > 100, 'brush mode visibly starts with lifted chest fur');
    report.outerTuftPixels = await changed(flat, rough, outerChest);
    assert.ok(report.outerTuftPixels > 40, 'small outer tufts remain visible over the white chest');
    assert.ok(await changed(flat, rough, upperHead) > 65, 'the shorter forehead tuft also starts lifted');
    assert.equal(await changed(flat, rough, faceBand), 0);
    assert.equal(await changed(flat, rough, feetBand, 0), 0);
    await save('flat-390x844.png', await page.screenshot());

    await page.evaluate(() => { window.draws.length = 0; });
    await swipe(page, tip, root, true);
    const lifted = await take(page);
    report.mouseLift = await changed(flat, lifted, chestLeft);
    assert.ok(report.mouseLift > 85, `reverse stroke lifts the whole flap: ${report.mouseLift}`);
    assert.equal(await changed(flat, lifted, feetBand, 0), 0);
    await save('lifted-chest.png', lifted);
    await canvas(page).dispatchEvent('pointercancel', { pointerId: 1 });
    await page.mouse.up();
    await expect.poll(async () => changed(flat, await take(page), chestLeft, 0)).toBe(0);
    report.petBatchMaxVertices = Math.max(...await page.evaluate(() => window.draws));
    assert.ok(report.petBatchMaxVertices > 0 && report.petBatchMaxVertices < patchCount(settings.regions) * 6);

    await button(page, '梳理').click();
    await swipe(page, tip, root);
    assert.equal(await changed(rough, await take(page), chestLeft, 0), 0, 'reverse brushing does not finish the patch');
    await swipe(page, root, tip);
    await swipe(page, root, tip);
    await expect.poll(async () => changed(flat, await take(page), chestLeft)).toBeLessThan(report.initialChest * .85);
    const brushed = await take(page);
    assert.equal(await changed(rough, brushed, upperHead, 0), 0, 'brushing chest does not smooth forehead');
    await save('brushed-chest.png', brushed);

    await swipe(page, tip, root);
    await expect.poll(async () => changed(brushed, await take(page), chestLeft)).toBeGreaterThan(40);
    await page.waitForTimeout(850);
    const raisedAgain = await take(page);
    assert.ok(await changed(flat, raisedAgain, chestLeft) > 70, 'reverse comb remains raised after release');
    assert.equal(await changed(rough, raisedAgain, upperHead, 0), 0, 'restyling is local');
    await save('reverse-brushed-chest.png', raisedAgain);
    await button(page, '抚摸').click();
    await expect.poll(async () => changed(raisedAgain, await take(page), chestLeft)).toBeGreaterThan(40);
    await button(page, '梳理').click();
    await expect.poll(async () => changed(raisedAgain, await take(page), chestLeft, 0)).toBe(0);
    await swipe(page, root, tip);
    await swipe(page, root, tip);
    await expect.poll(async () => changed(raisedAgain, await take(page), chestLeft)).toBeGreaterThan(40);

    await button(page, '复位').click();
    await expect.poll(async () => changed(rough, await take(page), chestLeft, 0)).toBe(0);
    await canvas(page).focus();
    for (let i = 0; i < 6; i++) await page.keyboard.press('Enter');
    await expect.poll(async () => changed(flat, await take(page), chestLeft)).toBe(0);
    assert.equal(await changed(flat, await take(page), upperHead), 0);
    await page.keyboard.press('Shift+Enter');
    await expect.poll(async () => changed(flat, await take(page), chestLeft)).toBeGreaterThan(70);

    await button(page, '复位').click();
    await expect.poll(async () => changed(rough, await take(page), chestLeft, 0)).toBe(0);
    await page.evaluate(() => { window.draws.length = 0; });
    await page.waitForTimeout(220);
    assert.deepEqual(await page.evaluate(() => window.draws), []);
    await button(page, '复位').click();
    const vertices = patchCount(settings.regions) * 6;
    await expect.poll(() => page.evaluate(() => window.draws.slice())).toEqual([vertices]);
    await page.waitForTimeout(200);
    assert.deepEqual(await page.evaluate(() => window.draws), [vertices]);
    report.dirtyBrushDraws = 1;
    report.dirtyBrushVertices = vertices;
    report.idleWebGlDraws = 0;

    const { width, height } = await sharp(rough).metadata();
    await save('comparison.png', await sharp({ create: { width: width * 3, height, channels: 3, background: '#d6e8da' } })
        .composite([flat, rough, brushed].map((input, i) => ({ input, left: i * width, top: 0 }))).png().toBuffer());

    const tune = button(page, '毛发参数');
    await tune.click();
    const editor = page.getByRole('complementary', { name: '毛发参数' });
    await expect(editor).toBeVisible();
    await editor.getByRole('combobox').selectOption({ label: '胸口' });
    await expect(editor.getByLabel('总片数')).toHaveText(`${defaultCount} / ${maxPatches}`);
    const guide = page.locator('.region-guide');
    await expect(guide).toBeVisible();
    const initialOutline = await guide.locator('polygon').getAttribute('points');
    const lengthRange = editor.getByRole('slider', { name: '单片长度' });
    await lengthRange.fill('42');
    assert.notEqual(await guide.locator('polygon').getAttribute('points'), initialOutline);
    await editor.getByRole('slider', { name: '毛流方向' }).fill('101');
    await editor.getByRole('slider', { name: '每层片数' }).fill('7');
    await expect(editor.getByLabel('总片数')).toHaveText(`${defaultCount + chestConfig.layers} / ${maxPatches}`);
    await editor.getByRole('slider', { name: '层数' }).fill('1');
    await expect(editor.getByLabel('总片数')).toHaveText(`${defaultCount - chestConfig.columns * chestConfig.layers + 7} / ${maxPatches}`);
    const download = page.waitForEvent('download');
    await button(page, '导出配置').click();
    const exported = await download;
    assert.equal(exported.suggestedFilename(), 'doudou-shiba-fur.json');
    const loaded = validateProfile(JSON.parse(await readFile(await exported.path(), 'utf8')));
    assert.deepEqual([loaded.regions[1].length, loaded.regions[1].angle, loaded.regions[1].columns, loaded.regions[1].layers], [42, 101, 7, 1]);
    await editor.getByRole('checkbox', { name: '显示范围' }).uncheck();
    await expect(guide).toBeHidden();
    await button(page, '恢复参数').click();
    await expect(lengthRange).toHaveValue(String(settings.regions[1].length));
    await expect(editor.getByLabel('总片数')).toHaveText(`${defaultCount} / ${maxPatches}`);
    await editor.getByRole('checkbox', { name: '显示范围' }).check();
    for (const [w, h] of [[390, 844], [320, 568], [1280, 900]]) {
        await page.setViewportSize({ width: w, height: h });
        await save(`editor-${w}x${h}.png`, await page.screenshot());
        const layout = await page.evaluate(() => ({ scrollWidth: document.documentElement.scrollWidth,
            bottom: document.querySelector('footer').getBoundingClientRect().bottom }));
        assert.ok(layout.scrollWidth <= w && layout.bottom <= h + 1, `${w}x${h}: ${JSON.stringify(layout)}`);
    }
    await tune.click();
    await expect(editor).toBeHidden();
    report.editor = true;

    const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    await open(mobile);
    const initial = await take(mobile);
    const session = await mobile.context().newCDPSession(mobile);
    const bounds = await box(mobile);
    async function touchSwipe(start, end) {
        await session.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [at(bounds, ...start)] });
        await session.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [at(bounds, 141, 278)] });
        await session.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [at(bounds, ...end)] });
        await session.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    }
    await touchSwipe(root, tip);
    await touchSwipe(root, tip);
    await expect.poll(async () => changed(initial, await take(mobile), chestLeft)).toBeGreaterThan(180);
    const touchFlat = await take(mobile);
    await touchSwipe(tip, root);
    await expect.poll(async () => changed(touchFlat, await take(mobile), chestLeft)).toBeGreaterThan(70);
    report.touch = true;

    const second = await browser.newPage({ viewport: { width: 390, height: 844 } });
    const image = await sharp({ create: { width: 300, height: 400, channels: 3, background: '#cb995a' } }).png().toBuffer();
    await second.route('**/sample-fur.json', route => route.fulfill({ json: {
        ...settings, id: 'sample-fur', name: '示例形象',
        portrait: { image: './fixture.png', width: 300, height: 400, crop: { x: 15, y: 20, width: 270, height: 360 } },
        regions: [configure({ x: 180, y: 150, columns: 2 })],
    } }));
    await second.route('**/fixture.png', route => route.fulfill({ body: image, contentType: 'image/png' }));
    await second.goto(`${url}?profile=sample-fur`);
    await expect(second.locator('main')).toHaveAttribute('data-ready', 'ready');
    await expect(second.getByRole('heading', { name: '示例形象' })).toBeVisible();
    await expect(canvas(second)).toHaveAccessibleName('梳理示例形象');
    await button(second, '毛发参数').click();
    await expect(second.getByLabel('总片数')).toHaveText(`2 / ${maxPatches}`);
    const secondBounds = await box(second);
    assert.ok(Math.abs(secondBounds.width / secondBounds.height - .75) < .002);
    report.secondProfile = true;
} finally {
    await browser.close();
}

const fallback = await launch(['--disable-webgl']);
try {
    const page = await fallback.newPage();
    await open(page, 'unavailable');
    await expect(page.getByRole('status')).toBeVisible();
    await expect(button(page, '梳理')).toBeDisabled();
    assert.ok(await page.locator('.portrait img').evaluate(el => el.naturalWidth === 512));
} finally {
    await fallback.close();
}

assert.deepEqual(bad, []);
assert.deepEqual(await fingerprint(), before);
await save('report.json', JSON.stringify({ url, ...report, inputs: before }, null, 2));
console.log('Continuous petting trail, profiles, layers, editor/export, directional light, reversible grooming, draw calls, input, responsive layout and fallback passed.');
