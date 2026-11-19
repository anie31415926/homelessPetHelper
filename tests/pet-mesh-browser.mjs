import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
const base = process.env.PET_MESH_URL || 'http://127.0.0.1:5193';
const output = 'test-results/pet-mesh'; await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const page = await browser.newPage({ reducedMotion: 'reduce' });
const errors = [], failed = [], requests = [], checks = [];
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
page.on('request', request => { if (request.resourceType() === 'image') requests.push(request.url()); });
const sample = name => page.locator('canvas').evaluate((canvas, key) => {
    const helper = document.createElement('canvas'); helper.width = canvas.width; helper.height = canvas.height;
    const ctx = helper.getContext('2d'); ctx.drawImage(canvas, 0, 0);
    const data = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
    const previous = window.__meshPixels;
    const result = { pixels: 0, difference: 0, width: canvas.width, height: canvas.height };
    for (let i = 0; i < data.length; i++) {
        if (i % 4 === 3 && data[i] > 64) result.pixels++;
        if (previous && previous.length === data.length && Math.abs(previous[i] - data[i]) > 4) result.difference++;
    }
    window.__meshPixels = data; result.name = key; return result;
}, name);
try {
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        await page.goto(`${base}/pet-mesh.html`, { waitUntil: 'networkidle' });
        await page.waitForSelector('main[data-status="ready"]'); await page.waitForTimeout(120);
        const layout = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth > innerWidth,
            clipped: [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent) }));
        assert.equal(layout.overflow, false); assert.deepEqual(layout.clipped, []);
        assert.ok((await sample('baseline')).pixels > 5000, 'real WebGL image is nonblank');
        await page.screenshot({ path: `${output}/preview-${width}x${height}.png`, fullPage: true });
        checks.push({ width, height, ...layout });
    }
    await page.setViewportSize({ width: 390, height: 780 });
    await page.goto(`${base}/pet-mesh.html`, { waitUntil: 'networkidle' });
    await page.waitForSelector('main[data-status="ready"]');
    await page.getByRole('slider', { name: '动作幅度', exact: true }).fill('0');
    await page.waitForTimeout(100); await sample('zero');
    await page.getByRole('button', { name: '原图', exact: true }).click();
    await page.waitForTimeout(100);
    const original = await sample('original'); assert.ok(original.difference < 100, 'zero-pose mesh matches the original image');
    await page.getByRole('button', { name: '变形', exact: true }).click();
    const controls = [];
    for (const [joint, value] of [['头颈', '10'], ['耳根', '18'], ['尾部', '20'], ['抬腿', '100']]) {
        await page.getByRole('button', { name: '复位', exact: true }).click();
        await page.getByRole('slider', { name: '动作幅度', exact: true }).fill('0');
        await page.waitForTimeout(100); await sample('rest');
        await page.getByRole('slider', { name: joint, exact: true }).fill(value);
        await page.waitForTimeout(100);
        const pixels = await sample(joint); assert.ok(pixels.difference > 300, `${joint} changes real pixels`);
        controls.push(pixels);
        await page.screenshot({ path: `${output}/control-${joint}.png` });
    }
    await page.getByRole('button', { name: '显示网格', exact: true }).click();
    await page.getByRole('button', { name: '显示骨骼', exact: true }).click();
    await page.screenshot({ path: `${output}/paw-mesh-bones.png` });
    await page.getByRole('button', { name: '复位', exact: true }).click();
    await page.getByLabel('动作', { exact: true }).selectOption('tail');
    await page.getByRole('button', { name: '播放', exact: true }).click();
    await page.waitForTimeout(200); await sample('moving');
    await page.waitForTimeout(220); assert.ok((await sample('moving2')).difference > 500, 'actual animation');
    await page.getByRole('button', { name: '暂停', exact: true }).click();
    await page.waitForTimeout(100); await sample('paused');
    await page.waitForTimeout(200); assert.equal((await sample('paused2')).difference, 0, 'pause freezes actual pixels');
    await page.setViewportSize({ width: 375, height: 667 });
    await page.waitForTimeout(150); assert.ok((await sample('resize')).pixels > 5000, 'paused resize remains visible');
    assert.ok(requests.every(url => url.endsWith('/pet-mesh/cat-side.png')), 'only one whole-animal texture, no part atlases');
    assert.deepEqual(errors, []); assert.deepEqual(failed, []);
    await writeFile(`${output}/browser-report.json`, JSON.stringify({ checks, controls, original, errors, failed, images: [...new Set(requests)], motion: true, pause: true }, null, 2));
    console.log('Single-image mesh passed: 5 viewports, original/zero identity, 4 local raster controls, motion, pause/resize and one texture.');
} finally { await browser.close(); }
