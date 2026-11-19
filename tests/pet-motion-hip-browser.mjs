import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
import { MOTIONS } from '../src/pet-layers/motion.js';

const base = process.env.PET_LAYERS_URL || 'http://127.0.0.1:5193', output = 'test-results/pet-motion-hip';
// Supplemental local before/after check: capture --baseline BEFORE edits, never to repair a failed comparison.
// The ignored baseline is browser/GPU-specific; portable checks live in test:pet-motion and its browser suite.
await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) { if (!error.message.includes("Executable doesn't exist")) throw error; browser = await chromium.launch({ channel: 'msedge', headless: true }); }
const page = await browser.newPage({ reducedMotion: 'reduce', viewport: { width: 390, height: 780 } });
const errors = [], failures = [];
page.on('pageerror', error => errors.push(error.message)); page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const seek = async (action, time) => {
    await page.getByLabel('整身动作', { exact: true }).selectOption(action);
    await page.getByLabel('动作进度', { exact: true }).fill(String(Math.round(time / MOTIONS[action] * 1000)));
    await page.waitForTimeout(100);
};
const hash = async () => createHash('sha256').update(await page.locator('canvas').evaluate(canvas => canvas.toDataURL())).digest('hex');
try {
    await page.goto(`${base}/pet-layers.html?motion=1&legacyPose=1`, { waitUntil: 'networkidle' }); await page.waitForSelector('main[data-status="ready"]');
    await page.getByLabel('动作幅度', { exact: true }).fill('100');
    const unchanged = [];
    for (const [action, time] of [['idle', .7], ['look', 1.1], ['tread', .4], ['walk', .9], ['walk', 3.2]]) {
        await seek(action, time); unchanged.push({ action, time, hash: await hash() });
    }
    if (process.argv.includes('--baseline')) {
        await writeFile(`${output}/approved-baseline.json`, JSON.stringify(unchanged, null, 2)); console.log('Captured approved non-lying motion pixels.');
    } else {
        const baseline = JSON.parse(await readFile(`${output}/approved-baseline.json`, 'utf8'));
        assert.deepEqual(unchanged, baseline, 'approved breathing/look/step/walk pixels must stay identical');
        const frames = [];
        for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
            await page.setViewportSize({ width, height });
            for (const time of [.4, 1, 1.6, 2.2, 3.8, 4.7]) {
                await seek('lie', time);
                const frame = await page.locator('canvas').evaluate(canvas => {
                    const copy = document.createElement('canvas'); copy.width = canvas.width; copy.height = canvas.height;
                    const context = copy.getContext('2d'); context.drawImage(canvas, 0, 0); const data = context.getImageData(0, 0, copy.width, copy.height).data;
                    let pixels = 0, left = copy.width, right = -1, top = copy.height, bottom = -1;
                    for (let p = 0; p < data.length; p += 4) if (data[p + 3] > 64 && data[p] > data[p + 1] + 3 && data[p + 1] > data[p + 2] + 2) {
                        const x = p / 4 % copy.width, y = Math.floor(p / 4 / copy.width); pixels++;
                        left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
                    }
                    return { pixels, left, right, top, bottom, width: copy.width, height: copy.height };
                });
                assert.ok(frame.pixels > 1500 && frame.left > 5 && frame.right < frame.width - 5 && frame.top > 5 && frame.bottom < frame.height - 5);
                frames.push({ width, height, time, ...frame });
                if (width === 390) await page.screenshot({ path: `${output}/lie-${time}.png` });
            }
        }
        await page.setViewportSize({ width: 390, height: 780 }); await seek('lie', 2.2);
        await page.getByRole('button', { name: '显示网格', exact: true }).click(); await page.getByRole('button', { name: '显示骨骼', exact: true }).click();
        await page.waitForTimeout(100); await page.screenshot({ path: `${output}/connected-grid.png` });
        await page.getByRole('button', { name: '显示身体', exact: true }).click(); await page.waitForTimeout(100);
        await page.screenshot({ path: `${output}/isolated-connected-hind.png` });
        assert.deepEqual(errors, []); assert.deepEqual(failures, []);
        await writeFile(`${output}/browser-report.json`, JSON.stringify({ unchanged, frames, errors, failures }, null, 2));
        console.log('Connected lying hip passed: approved non-lying pixels identical; five viewports and six lie/rise stages, shared mesh/bones, isolated hind.');
    }
} finally { await browser.close(); }
