import assert from 'node:assert/strict';
import {mkdir} from 'node:fs/promises';
import {chromium} from '@playwright/test';
import {createChibiRig} from '../src/pet-sit-chibi/rig.js';
import {foreWeights, hindWeights, liftForepaw} from '../src/pet-mesh-sit/join.js';
import {contourStrength} from '../src/pet-mesh-sit/contour.js';
import {rumpBand, sampleRump} from '../src/pet-mesh-sit/rump.js';
import {sidePose} from '../src/pet-mesh-sit/side.js';
import {deinkAmount} from '../src/pet-mesh-sit/deink.js';
import {tailBoundary} from '../src/pet-mesh-sit/partition.js';
import {pawContact} from '../src/pet-mesh-sit/paw-contact.js';
import {readFile} from 'node:fs/promises';

const {layers} = JSON.parse(await readFile('public/pet-sit-chibi/atlas.json', 'utf8'));
const pawSupport = layers.find(layer => layer.id === 'nearHindPaw').support
    .map(([x, y]) => [(291 - x) / 100, (477 - y) / 100]);
for (let i = 0; i <= 100; i++) {
    const {angle, height} = pawContact(i / 100, pawSupport);
    const lower = Math.min(...pawSupport.map(([x, y]) => x * Math.sin(angle) + y * Math.cos(angle) + height));
    assert.ok(Math.abs(lower - .004) < 1e-8);
}
assert.equal(pawContact(.53, pawSupport).angle, 0);
assert.ok(pawContact(1, pawSupport).angle > .6);
import {contactAmount, contactY} from '../src/pet-mesh-sit/seat-contact.js';

for (const [x, y] of [[346, 445], [342, 453], [337, 463], [331, 475]]) {
    const weight = hindWeights('near', y, x)[1][1];
    assert.equal(weight, 1, 'The original rear outline follows only the paw');
    assert.equal(contactY((226 - x) / 100, .08, 1, weight), .08);
}
assert.ok(hindWeights('near', 429, 340)[1][1] > 0 && hindWeights('near', 429, 340)[1][1] < 1);
assert.equal(hindWeights('far', 445, 346)[1][1], hindWeights('far', 445, 302)[1][1]);
assert.equal(contactAmount(.55), 0);
assert.equal(contactY(-.8, .18, 1), .18);
assert.equal(contactY(.1, .04, 1), .04);
assert.ok(contactY(-1, .06, 1) < .03);
assert.ok(contactY(-1.55, .24, 1) < .02, 'Rump underside at tail root reaches the floor');
assert.equal(contactY(-2.08, .08, 1), .08, 'Tail away from root keeps its arc');
assert.equal(contactY(-1.55, .50, 1), .50, 'Upper rump is unchanged');
for (let i = 0; i < 140; i++) {
    assert.ok(contactY(-1, i / 1000, 1) < contactY(-1, (i + 1) / 1000, 1));
}

assert.ok(tailBoundary(372) > tailBoundary(425));
assert.ok(Math.abs(tailBoundary(390 - .001) - tailBoundary(390 + .001)) < .01);
assert.equal(deinkAmount(.31), 0);
assert.equal(deinkAmount(.64), 1);
assert.deepEqual(sampleRump(.3), [0, 0, 0]);
assert.deepEqual(sampleRump(.55), [3.5, -2.5, .72]);
assert.deepEqual(sampleRump(1), [6, -4, 1]);
assert.equal(rumpBand(349, 435), 0, 'Leave the ground contact outside the rim adjustment');
assert.ok(sidePose(0).contact.every(id => id.startsWith('leg-')));
assert.ok(sidePose(1).contact.includes('hip') && sidePose(1).contact.includes('chest'));
for (let i = 0; i <= 100; i++) {
    const {body, limbs, floor, contact} = sidePose(i / 100);
    assert.ok(contact.length);
    for (const p of body) assert.ok(p.xy[1] - p.extent >= floor - 1e-8);
    for (const leg of limbs) leg.lengths.forEach((length, j) => {
        const a = leg.points[j], b = leg.points[j + 1];
        assert.ok(Math.abs(Math.hypot(...a.map((v, k) => v - b[k])) - length) < 1e-8);
        assert.ok(b[1] - (j ? 12 : 8) >= floor - 1e-8);
    });
}

assert.equal(contourStrength(.6, .2), 0);
assert.equal(contourStrength(1, .5), 0);
assert.equal(contourStrength(1, .01), 0);
assert.ok(contourStrength(.9, .15) > .5);

for (const fn of [foreWeights, hindWeights]) {
    for (let y = 315; y <= 501; y++) {
        const weights = fn('near', y, 330);
        assert.ok(weights.every(([, w]) => w >= 0 && w <= 1));
        assert.ok(Math.abs(weights.reduce((total, [, w]) => total + w, 0) - 1) < 1e-9);
    }
    assert.equal(fn('near', 481, 330).at(-1)[1], 1, 'Paw core must have a single rigid influence');
}
const rig = createChibiRig();
rig.apply(0);
const planted = rig.world('farFrontToe'), toe = rig.world('nearFrontToe');
let last = toe;
for (let i = 0; i <= 100; i++) {
    rig.apply(i / 100);
    for (const {from, to, length} of rig.segments) {
        assert.ok(Math.abs(rig.world(from).distanceTo(rig.world(to)) - length) < 1e-7);
    }
    rig.apply(0);
    liftForepaw(rig, i / 100);
    for (const {from, to, length} of rig.segments) {
        assert.ok(Math.abs(rig.world(from).distanceTo(rig.world(to)) - length) < 1e-7);
    }
    const moved = rig.world('nearFrontToe');
    assert.ok(moved.distanceTo(last) < .04);
    assert.ok(rig.world('farFrontToe').distanceTo(planted) < 1e-7);
    if (i === 50) assert.ok(moved.y - toe.y > .28);
    if (i === 100) assert.ok(moved.distanceTo(toe) < 1e-6);
    last = moved;
}
rig.dispose();

const url = process.env.PET_MESH_SIT_URL || 'http://127.0.0.1:5193';
const output = 'test-results/pet-mesh-sit';
await mkdir(output, {recursive: true});
const browser = await chromium.launch().catch(error => {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    return chromium.launch({channel: 'msedge'});
});
try {
    const page = await browser.newPage();
    const errors = [], images = new Set();
    page.on('pageerror', error => errors.push(error.message));
    page.on('request', request => {
        if (request.resourceType() === 'image') images.add(new URL(request.url()).pathname);
    });
    const set = value => page.getByRole('slider', {name: '动作进度'}).evaluate((input, n) => {
        input.value = n; input.dispatchEvent(new Event('input', {bubbles: true}));
    }, value);
    const sample = () => page.locator('canvas:not(.contour)').evaluateAll(canvases => canvases.map(canvas => {
        const copy = document.createElement('canvas');
        copy.width = canvas.width; copy.height = canvas.height;
        const ctx = copy.getContext('2d');
        ctx.drawImage(canvas, 0, 0);
        const {data} = ctx.getImageData(0, 0, copy.width, copy.height);
        let area = 0, hash = 0, left = canvas.width, right = 0, top = canvas.height, bottom = 0;
        for (let i = 0; i < data.length; i += 4) {
            if (data[i + 3] < 80) continue;
            area++;
            const x = (i / 4) % canvas.width, y = Math.floor(i / 4 / canvas.width);
            left = Math.min(left, x); right = Math.max(right, x);
            top = Math.min(top, y); bottom = Math.max(bottom, y);
            hash = Math.imul(hash ^ (data[i] | data[i + 1] << 8 | x << 16 | y << 25), 16777619);
        }
        return {area, hash, top, left, right, bottom, w: canvas.width, h: canvas.height};
    }));
    const inkPixels = () => page.locator('.contour').evaluate(layer => {
        const {width, height} = layer;
        const layerData = layer.getContext('2d').getImageData(0, 0, width, height).data;
        const base = document.createElement('canvas');
        base.width = width; base.height = height;
        const ctx = base.getContext('2d');
        ctx.drawImage(layer.parentElement.querySelector('canvas:not(.contour)'), 0, 0);
        const baseData = ctx.getImageData(0, 0, width, height).data;
        let count = 0, leaks = 0;
        for (let i = 3; i < layerData.length; i += 4) if (layerData[i]) {
            count++;
            if (!baseData[i]) leaks++;
        }
        return {count, leaks};
    });
    const deinkPixels = mode => page.locator('.stage').nth(1).locator('canvas:not(.contour)').evaluate((base, save) => {
        const c = document.createElement('canvas'); c.width = base.width; c.height = base.height;
        const ctx = c.getContext('2d'); ctx.drawImage(base, 0, 0);
        const {data} = ctx.getImageData(0, 0, c.width, c.height);
        if (save) {base.deinkReference = data; return null;}
        let changes = 0, outside = 0, alpha = 0, minX = 1, maxX = 0, minY = 1;
        const original = base.deinkReference;
        for (let i = 0; i < data.length; i += 4) {
            if (original[i + 3] !== data[i + 3]) alpha++;
            if (original[i + 3] < 120 || Math.max(...[0, 1, 2].map(j => Math.abs(original[i + j] - data[i + j]))) < 8) continue;
            changes++;
            const x = i / 4 % c.width, y = Math.floor(i / 4 / c.width);
            minX = Math.min(minX, x / c.width); maxX = Math.max(maxX, x / c.width); minY = Math.min(minY, y / c.height);
            if (x < c.width * .64 || x > c.width * .86 || y < c.height * .63) outside++;
        }
        return {changes, outside, alpha, minX, maxX, minY};
    }, mode === 'save');
    const visible = frames => frames.forEach(f => {
        assert.ok(f.area > f.w * f.h * .17);
        assert.ok(f.left > 0 && f.top > 0 && f.right < f.w - 1 && f.bottom < f.h - 1, JSON.stringify(f));
    });
    for (const [width, height] of [[1280, 800], [390, 780], [320, 568]]) {
        await page.setViewportSize({width, height});
        await page.goto(url + '/pet-mesh-sit.html');
        await page.waitForSelector('main[data-status="ready"]');
        const rest = await sample(); visible(rest);
        for (const percent of [30, 55, 85, 100]) {
            await set(percent);
            const poses = await sample(); visible(poses);
            for (let i = 0; i < poses.length; i++) {
                assert.notEqual(poses[i].hash, rest[i].hash);
                assert.ok(poses[i].area > rest[i].area * .72);
                assert.ok(Math.abs(poses[i].bottom - rest[i].bottom) <= 6);
            }
            assert.notEqual(poses[0].hash, poses[1].hash);
            if (percent === 55 || percent === 100) await page.screenshot({path: `${output}/rig-sit-${width}-${percent}.png`});
        }
        const repartitioned = await sample();
        const pawButton = page.getByRole('button', {name: '后掌贴地'});
        await pawButton.click();
        const fixedPaw = await sample();
        assert.equal(fixedPaw[0].area, repartitioned[0].area);
        assert.notEqual(fixedPaw[1].hash, repartitioned[1].hash);
        assert.ok(Math.abs(fixedPaw[1].bottom - repartitioned[1].bottom) <= 2);
        await page.screenshot({path: `${output}/paw-contact-off-${width}.png`});
        await pawButton.click();
        await page.getByRole('button', {name: '接地形变'}).click();
        const rigid = await sample();
        assert.notEqual(rigid[1].hash, repartitioned[1].hash);
        assert.equal(rigid[0].bottom, repartitioned[0].bottom);
        assert.ok(Math.abs(rigid[1].area - repartitioned[1].area) < rigid[1].area * .012);
        assert.equal(rigid[1].top, repartitioned[1].top);
        await page.screenshot({path: `${output}/seat-rigid-${width}.png`});
        await page.getByRole('button', {name: '接地形变'}).click();
        await page.getByRole('button', {name: '重分层'}).click();
        const plain = await sample();
        assert.notEqual(plain[1].hash, repartitioned[1].hash);
        assert.ok(Math.abs(plain[1].bottom - repartitioned[1].bottom) <= 2);
        assert.ok(Math.abs(plain[1].area - repartitioned[1].area) < plain[1].area * .02);
        await page.getByRole('button', {name: '臀部修形'}).click();
        const corrected = await sample();
        assert.notEqual(plain[1].hash, corrected[1].hash);
        assert.equal(plain[0].bottom, corrected[0].bottom);
        assert.ok(Math.abs(plain[1].area - corrected[1].area) < plain[1].area * .015);
        await page.getByRole('button', {name: '臀部修形'}).click();
        await set(55);
        await deinkPixels('save');
        await page.getByRole('button', {name: '去内描边'}).click();
        const changed = await deinkPixels('compare');
        assert.ok(changed.changes > 4 && changed.outside === 0 && changed.alpha === 0, JSON.stringify(changed));
        await page.getByRole('button', {name: '去内描边'}).click();
        await set(100);
        assert.equal((await inkPixels()).count, 0);
        const beforeInk = await sample();
        await page.getByRole('button', {name: '补线'}).click();
        const ink = await inkPixels();
        assert.ok(ink.count > 1 && ink.count < beforeInk[1].area * .01 && ink.leaks === 0, JSON.stringify(ink));
        assert.deepEqual(await sample(), beforeInk, 'The line must not change the underlying rig pixels');
        await page.screenshot({path: `${output}/rig-ink-${width}.png`});
        await set(55);
        assert.equal((await inkPixels()).count, 0);
        await page.selectOption('#action', 'paw');
        await set(50);
        const lifted = await sample(); visible(lifted);
        assert.equal((await inkPixels()).count, 0);
        lifted.forEach((pose, i) => assert.notEqual(pose.hash, rest[i].hash));
        assert.notEqual(lifted[0].hash, lifted[1].hash);
        await page.getByRole('button', {name: '深底'}).click();
        await page.screenshot({path: `${output}/rig-paw-${width}.png`});
        await page.getByRole('button', {name: '骨架'}).click();
        assert.notEqual((await sample())[1].hash, lifted[1].hash);
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth === innerWidth));
    }
    await page.getByRole('button', {name: '播放'}).click();
    await page.waitForFunction(() => Number(document.querySelector('input').value) > 1);
    await page.getByRole('button', {name: '暂停'}).click();
    assert.deepEqual([...images].sort(), ['/pet-mesh/cat-side.png', '/pet-sit-chibi/atlas.png']);
    await page.getByText('侧躺 · 翻侧接地').click();
    const study = page.locator('#side-study'), diagram = study.getByRole('img', {name: '侧躺骨架与接地点'});
    const standing = await diagram.innerHTML();
    for (const v of [60, 100]) {
        await study.getByRole('slider').evaluate((slider, n) => {
            slider.value = n; slider.dispatchEvent(new Event('input', {bubbles: true}));
        }, v);
        assert.notEqual(await diagram.innerHTML(), standing);
        await study.screenshot({path: `${output}/side-block-${v}.png`});
    }
    assert.ok(await study.locator('img').evaluate(img => img.complete && img.naturalWidth > 0));
    assert.deepEqual(errors, []);
    console.log('PASS 101 poses per action; editable partition in 3 viewports, local cleanup regression and side roll.');
} finally {
    await browser.close();
}
