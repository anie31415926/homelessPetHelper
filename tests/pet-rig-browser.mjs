import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
const base = process.env.PET_RIG_URL || 'http://127.0.0.1:5193';
const output = 'test-results/pet-rig';
await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const page = await browser.newPage({ reducedMotion: 'reduce' });
const errors = [], failures = [], checks = [];
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const capturePixels = () => page.locator('canvas').evaluate(canvas => Array.from(canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data));
const difference = (a, b) => a.reduce((n, value, i) => n + (Math.abs(value - b[i]) > 6 ? 1 : 0), 0);
try {
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        await page.goto(`${base}/pet-rig.html`, { waitUntil: 'networkidle' });
        await page.waitForSelector('main[data-status="ready"]');
        const geometry = await page.evaluate(() => ({
            overflow: document.documentElement.scrollWidth > innerWidth,
            clipped: [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent),
            canvas: document.querySelector('canvas').getBoundingClientRect().toJSON(),
        }));
        assert.equal(geometry.overflow, false);
        assert.deepEqual(geometry.clipped, []);
        const still = await capturePixels();
        assert.ok(still.filter((value, i) => i % 4 === 3 && value > 64).length > 15000, 'animal is nonblank');
        await page.screenshot({ path: `${output}/side-${width}x${height}.png`, fullPage: true });
        await page.getByRole('button', { name: '正面', exact: true }).click();
        await page.waitForTimeout(100);
        assert.ok(difference(still, await capturePixels()) > 20000, 'view changes actual pixels');
        await page.screenshot({ path: `${output}/front-${width}x${height}.png`, fullPage: true });
        checks.push({ width, height, ...geometry });
    }
    await page.setViewportSize({ width: 390, height: 780 });
    const rendering = await page.evaluate(async () => {
        const { loadArt, renderRig } = await import('/src/pet-rig/renderer.js');
        const art = await loadArt();
        const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 540;
        const ctx = canvas.getContext('2d');
        const read = () => ctx.getImageData(0, 0, 640, 540).data;
        const changes = (a, b) => a.reduce((n, value, i) => n + (Math.abs(value - b[i]) > 6 ? 1 : 0), 0);
        const checks = [];
        for (const view of ['front', 'side']) {
            const state = { view, action: 'idle', proportion: 'standard', manual: {}, bones: false, exploded: false };
            renderRig(ctx, art, state, 0); const original = read();
            for (const joint of ['head', 'ears', 'tail', 'legs']) {
                renderRig(ctx, art, { ...state, manual: { [joint]: 18 } }, 0);
                checks.push({ view, joint, pixels: changes(original, read()) });
            }
            for (const proportion of ['standard', 'short', 'long']) for (const action of ['idle', 'walk', 'happy', 'listen', 'crouch']) {
                for (const time of [0, .4, 1.1]) {
                    renderRig(ctx, art, { ...state, proportion, action }, time);
                    const data = read();
                    for (let y = 0; y < 540; y++) for (let x = 0; x < 640; x++) {
                        if (x > 2 && x < 637 && y > 2 && y < 537) continue;
                        if (data[(y * 640 + x) * 4 + 3] > 32) throw new Error(`Clipped image: ${view}/${proportion}/${action}`);
                    }
                }
            }
        }
        return checks;
    });
    assert.ok(rendering.every(check => check.pixels > 300), 'every manual joint changes its actual raster geometry with time frozen');
    await page.getByRole('button', { name: '侧面', exact: true }).click();
    await page.getByRole('button', { name: '踏步', exact: true }).click();
    await page.getByRole('button', { name: '播放', exact: true }).click();
    await page.waitForTimeout(160);
    const a = await capturePixels();
    await page.waitForTimeout(250);
    const b = await capturePixels();
    assert.ok(difference(a, b) > 4000, 'limbs actually animate');
    await page.getByRole('button', { name: '暂停', exact: true }).click();
    await page.waitForTimeout(100);
    const paused = await capturePixels();
    await page.waitForTimeout(200);
    assert.equal(difference(paused, await capturePixels()), 0, 'pause freezes every pixel');
    await page.setViewportSize({ width: 375, height: 667 });
    await page.waitForTimeout(100);
    assert.equal(difference(paused, await capturePixels()), 0, 'resizing a paused preview preserves its pixels');
    await page.setViewportSize({ width: 390, height: 780 });
    await page.getByRole('slider', { name: '耳朵', exact: true }).fill('22');
    await page.getByRole('slider', { name: '尾巴', exact: true }).fill('35');
    await page.waitForTimeout(100);
    assert.ok(difference(paused, await capturePixels()) > 1000, 'manual joints control images');
    await page.getByLabel('比例预设').selectOption('short');
    await page.getByRole('button', { name: '显示骨骼', exact: true }).click();
    await page.screenshot({ path: `${output}/short-bones.png` });
    await page.getByRole('button', { name: '展开部件', exact: true }).click();
    await page.screenshot({ path: `${output}/exploded.png` });
    await page.getByRole('button', { name: '查看参考图', exact: true }).click();
    assert.equal(await page.getByRole('dialog').count(), 1);
    await page.keyboard.press('Escape');
    assert.equal(await page.getByRole('dialog').count(), 0);
    await page.getByRole('button', { name: '复位', exact: true }).click();
    assert.equal(await page.getByRole('slider', { name: '尾巴', exact: true }).inputValue(), '0');
    assert.equal(await page.getByLabel('比例预设').inputValue(), 'standard');
    for (const view of ['front', 'side']) {
        await page.getByRole('button', { name: view === 'front' ? '正面' : '侧面', exact: true }).click();
        for (const action of ['放松', '踏步', '开心', '听声音', '伏低']) {
            await page.getByRole('button', { name: action, exact: true }).click();
            await page.screenshot({ path: `${output}/${view}-${action}.png` });
        }
    }
    assert.deepEqual(errors, []); assert.deepEqual(failures, []);
    await writeFile(`${output}/browser-report.json`, JSON.stringify({ checks, rendering, errors, failures, motion: true, paused: true, joints: true, controls: true }, null, 2));
    console.log('Pet rig browser checks passed: 5 viewports, 90 pose framing samples, frozen-time raster joints, motion, pause/resize, proportions, overlays and modal.');
} finally { await browser.close(); }
