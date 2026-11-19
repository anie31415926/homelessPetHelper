import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
import { MOTIONS } from '../src/pet-layers/motion.js';

const base = process.env.PET_LAYERS_URL || 'http://127.0.0.1:5193', output = 'test-results/pet-motion';
await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) { if (!error.message.includes("Executable doesn't exist")) throw error; browser = await chromium.launch({ channel: 'msedge', headless: true }); }
const page = await browser.newPage({ reducedMotion: 'reduce' }), errors = [], failures = [], checks = [];
page.on('pageerror', error => errors.push(error.message)); page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const sample = () => page.locator('canvas').evaluate(canvas => {
    const copy = document.createElement('canvas'); copy.width = canvas.width; copy.height = canvas.height;
    const context = copy.getContext('2d'); context.drawImage(canvas, 0, 0);
    const data = context.getImageData(0, 0, copy.width, copy.height).data, old = window.__motionPixels;
    let pixels = 0, difference = 0, left = copy.width, right = -1, top = copy.height, bottom = -1;
    for (let p = 0; p < data.length; p += 4) {
        if (data[p + 3] > 64 && data[p] > data[p + 1] + 3 && data[p + 1] > data[p + 2] + 2) {
            const x = p / 4 % copy.width, y = Math.floor(p / 4 / copy.width); pixels++;
            left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
        }
        if (old?.length === data.length) for (let c = 0; c < 4; c++) if (Math.abs(data[p + c] - old[p + c]) > 4) difference++;
    }
    window.__motionPixels = data; return { pixels, difference, left, right, top, bottom, width: copy.width, height: copy.height };
});
const seek = async (action, time) => {
    await page.getByLabel('整身动作', { exact: true }).selectOption(action);
    await page.getByLabel('动作进度', { exact: true }).fill(String(Math.round(time / MOTIONS[action] * 1000)));
    await page.waitForTimeout(100); return sample();
};
const assertFramed = frame => {
    assert.ok(frame.pixels > 1500, 'real, nonblank animal texture');
    assert.ok(frame.left > 5 && frame.right < frame.width - 5 && frame.top > 5 && frame.bottom < frame.height - 5, 'complete animal stays inside camera');
};
const checkAssets = () => page.evaluate(async () => {
    const load = async path => {
        const image = new Image(); image.src = path; await image.decode();
        if (image.width !== 452 || image.height !== 512) throw new Error('Source layer size changed');
        const canvas = document.createElement('canvas'); canvas.width = 452; canvas.height = 512;
        const context = canvas.getContext('2d'); context.drawImage(image, 0, 0); return context.getImageData(0, 0, 452, 512).data;
    };
    const [original, body, mask, patch] = await Promise.all(['/pet-mesh/cat-side.png', '/pet-motion/body.png', '/art-source/pet-motion/body-mask.png', '/art-source/pet-motion/patch-mask.png'].map(load));
    let untouchedPixels = 0, bodyViolations = 0;
    for (let p = 0; p < original.length; p += 4) {
        if (mask[p + 3] !== 255 || patch[p + 3]) continue;
        untouchedPixels++;
        for (let c = 0; c < 4; c++) if (body[p + c] !== original[p + c]) bodyViolations++;
    }
    const near = [];
    for (const kind of ['front', 'hind']) {
        const data = await load(`/pet-motion/near-${kind}.png`), donor = await load(`/art-source/pet-limbs/donor/${kind}-source.png`);
        let opaquePixels = 0, violations = 0;
        for (let p = 0; p < data.length; p += 4) {
            if (data[p + 3] === 255) { opaquePixels++; for (let c = 0; c < 4; c++) if (data[p + c] !== donor[p + c]) violations++; }
            if (!donor[p + 3] && data[p + 3]) violations++;
        }
        near.push({ kind, opaquePixels, violations });
    }
    const [connected, connectedMask, hindMask] = await Promise.all(['/pet-motion/lie-connected.png', '/art-source/pet-motion/connected-mask.png', '/art-source/pet-limbs/donor/hind-mask.png'].map(load));
    let connectedViolations = 0, preservedHind = 0;
    for (let p = 0; p < original.length; p += 4) {
        const x = p / 4 % 452, y = Math.floor(p / 4 / 452), alpha = Math.max(mask[p + 3], hindMask[p + 3]);
        if (connectedMask[p + 3] !== alpha || Math.abs(connected[p + 3] - Math.round(original[p + 3] * alpha / 255)) > 1) connectedViolations++;
        if (original[p + 3] !== 255 || alpha !== 255) continue;
        if (!(x >= 171 && x <= 197 && y >= 372 && y <= 434)) {
            for (let c = 0; c < 3; c++) if (connected[p + c] !== original[p + c]) connectedViolations++;
        }
        if (x > 230 && y > 380 && hindMask[p + 3] === 255) preservedHind++;
    }
    const seen = new Uint8Array(452 * 512), queue = [340 * 452 + 310]; seen[queue[0]] = 1;
    for (let i = 0; i < queue.length; i++) {
        const p = queue[i], x = p % 452;
        for (const n of [x ? p - 1 : -1, x < 451 ? p + 1 : -1, p - 452, p + 452]) {
            if (n < 0 || n >= seen.length || seen[n] || connected[n * 4 + 3] < 128) continue;
            seen[n] = 1; queue.push(n);
        }
    }
    return { untouchedPixels, bodyViolations, near, connectedViolations, preservedHind, hipToPawConnected: !!seen[478 * 452 + 290] };
});
try {
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height }); await page.goto(`${base}/pet-layers.html?motion=1&legacyPose=1`, { waitUntil: 'networkidle' });
        await page.waitForSelector('main[data-status="ready"]'); await page.waitForTimeout(100);
        const geometry = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth > innerWidth,
            clipped: [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent) }));
        assert.equal(geometry.overflow, false); assert.deepEqual(geometry.clipped, []); assertFramed(await sample());
        await page.screenshot({ path: `${output}/preview-${width}x${height}.png`, fullPage: true }); checks.push({ width, height, ...geometry });
        for (const [action, time] of [['walk', 1], ['walk', 3.5], ['lie', 2.2], ['look', 1.1]]) assertFramed(await seek(action, time));
    }
    const assets = await checkAssets(); assert.equal(assets.bodyViolations, 0); assert.ok(assets.untouchedPixels > 100000);
    for (const near of assets.near) { assert.equal(near.violations, 0); assert.ok(near.opaquePixels > 6000); }
    assert.equal(assets.connectedViolations, 0); assert.ok(assets.preservedHind > 5000); assert.equal(assets.hipToPawConnected, true);
    await page.setViewportSize({ width: 390, height: 780 }); await page.getByLabel('动作幅度', { exact: true }).fill('100');
    const poses = [];
    for (const [action, times] of Object.entries({ idle: [.7, 2.5], look: [1.1, 3.5], tread: [.4, .8], walk: [.9, 1.9, 3.2, 4.2], lie: [0, 1, 2.2, 3.8, 4.8] })) {
        for (const time of times) {
            const frame = await seek(action, time); assertFramed(frame);
            await page.screenshot({ path: `${output}/${action}-${time}.png` }); poses.push({ action, time, ...frame });
        }
        assert.ok(poses.filter(pose => pose.action === action).some(pose => pose.difference > 100));
    }
    await seek('tread', .4); await page.getByRole('button', { name: '显示身体', exact: true }).click(); await page.waitForTimeout(100);
    const isolated = await sample(); assert.ok(isolated.pixels > 3000 && isolated.pixels < 15000);
    await page.screenshot({ path: `${output}/four-legs.png` });
    await page.getByRole('button', { name: '显示网格', exact: true }).click(); await page.getByRole('button', { name: '显示骨骼', exact: true }).click(); await page.waitForTimeout(100);
    assert.ok((await sample()).difference > 100); await page.screenshot({ path: `${output}/mesh-bones.png` });
    await page.getByRole('button', { name: '复位', exact: true }).click(); await page.getByLabel('播放速度', { exact: true }).fill('125');
    assert.equal(await page.locator('output').textContent(), '1.25x');
    await page.getByRole('button', { name: '播放', exact: true }).click(); await page.waitForTimeout(150); await sample(); await page.waitForTimeout(300);
    const playing = await sample(); assert.ok(playing.difference > 100);
    await page.getByRole('button', { name: '暂停', exact: true }).click(); await page.waitForTimeout(100); await sample(); await page.waitForTimeout(200); assert.equal((await sample()).difference, 0);
    const pausedPhase = await page.getByLabel('动作进度', { exact: true }).inputValue();
    await page.setViewportSize({ width: 375, height: 667 }); await page.waitForTimeout(100); assertFramed(await sample());
    assert.equal(await page.getByLabel('动作进度', { exact: true }).inputValue(), pausedPhase);
    await page.getByRole('button', { name: '原图', exact: true }).click(); await page.waitForTimeout(100); assert.ok((await sample()).difference > 100);
    assert.equal(await page.getByLabel('整身动作', { exact: true }).isDisabled(), true);
    await page.getByRole('button', { name: '动作', exact: true }).click();
    await page.goto(`${base}/pet-layers.html?motion=1&legacyPose=1&action=lie`, { waitUntil: 'networkidle' });
    await page.waitForSelector('main[data-status="ready"]');
    assert.equal(await page.getByLabel('整身动作', { exact: true }).inputValue(), 'lie');
    await seek('lie', 2.2); const connectedHash = await page.locator('canvas').evaluate(canvas => canvas.toDataURL());
    await page.getByRole('button', { name: '显示身体', exact: true }).click(); await page.waitForTimeout(100);
    const lyingLegs = await sample(); assert.ok(lyingLegs.pixels > 3000 && lyingLegs.pixels < 15000);
    await page.getByRole('button', { name: '显示身体', exact: true }).click(); await page.waitForTimeout(100);
    assert.equal(await page.locator('canvas').evaluate(canvas => canvas.toDataURL()), connectedHash, 'connected skin texture restores exactly after isolation');
    assert.deepEqual(errors, []); assert.deepEqual(failures, []);
    await writeFile(`${output}/browser-report.json`, JSON.stringify({ checks, assets, poses, isolated, playing, errors, failures }, null, 2));
    console.log('Four-leg motion passed: five viewports, real textures, stepping, bounded two-direction travel, lie/rise, head/tail, isolation, scrub, pause and resize.');
} finally { await browser.close(); }
