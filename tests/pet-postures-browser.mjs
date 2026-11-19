import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
import { MOTIONS } from '../src/pet-layers/motion.js';

const base = process.env.PET_LAYERS_URL || 'http://127.0.0.1:5193', output = 'test-results/pet-postures';
await mkdir(output, { recursive: true });
assert.equal(createHash('sha256').update(await readFile('public/pet-mesh/cat-side.png')).digest('hex'), 'b1889c797e975a625a746ab530a4c7d9231a068ca387871b5306b2fedfd9b727');
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) { if (!error.message.includes("Executable doesn't exist")) throw error; browser = await chromium.launch({ channel: 'msedge', headless: true }); }
const page = await browser.newPage({ reducedMotion: 'reduce', viewport: { width: 390, height: 780 } });
const errors = [], failures = [], checks = [];
page.on('pageerror', error => errors.push(error.message)); page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const hash = async () => createHash('sha256').update(await page.locator('canvas').evaluate(canvas => canvas.toDataURL())).digest('hex');
const seek = async time => {
    const action = await page.getByLabel('整身动作', { exact: true }).inputValue();
    await page.getByLabel('动作进度', { exact: true }).fill(String(Math.round(time / MOTIONS[action] * 1000)));
    await page.waitForTimeout(85);
};
const frame = () => page.locator('canvas').evaluate(canvas => {
    const copy = document.createElement('canvas'); copy.width = canvas.width; copy.height = canvas.height;
    const ctx = copy.getContext('2d'); ctx.drawImage(canvas, 0, 0); const data = ctx.getImageData(0, 0, copy.width, copy.height).data;
    let pixels = 0, left = copy.width, right = -1, top = copy.height, bottom = -1;
    for (let p = 0; p < data.length; p += 4) if (data[p + 3] > 64 && data[p] > data[p + 1] + 3 && data[p + 1] > data[p + 2] + 2) {
        const x = p / 4 % copy.width, y = Math.floor(p / 4 / copy.width); pixels++;
        left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
    }
    return { pixels, left, right, top, bottom, width: copy.width, height: copy.height };
});
try {
    await page.goto(`${base}/pet-layers.html?motion=1&action=sit`, { waitUntil: 'networkidle' });
    await page.waitForSelector('main[data-status="ready"]');
    assert.equal(await page.getByLabel('整身动作', { exact: true }).inputValue(), 'sit');
    assert.equal(await page.locator('option[value="lie"]').count(), 0);
    assert.equal(await page.locator('option[value="sitMesh"]').count(), 0);
    assert.equal(await page.getByRole('button', { name: '播放', exact: true }).count(), 1);
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        for (const action of ['sit', 'side']) {
            await page.getByLabel('整身动作', { exact: true }).selectOption(action);
            await page.getByLabel('动作幅度', { exact: true }).fill('100');
            const hashes = new Set();
            for (const time of [0, .4, 1, 2.5, MOTIONS[action] - 1.3, MOTIONS[action] - .2]) {
                await seek(time); const f = await frame(); hashes.add(await hash());
                assert.ok(f.pixels > 1500 && f.left > 5 && f.right < f.width - 5 && f.top > 5 && f.bottom < f.height - 5, JSON.stringify({ action, time, f }));
                checks.push({ action, time, viewport: [width, height], ...f });
                if (width === 390 || width === 1280 && time === 2.5) await page.screenshot({ path: `${output}/${action}-${width}-${time}.png` });
            }
            assert.ok(hashes.size >= 4, 'actual rendered posture changes over time');
        }
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        const clipped = await page.evaluate(() => [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent));
        assert.deepEqual(clipped, []);
    }
    await page.setViewportSize({ width: 390, height: 780 });
    for (const action of ['sit', 'side']) {
        await page.getByLabel('整身动作', { exact: true }).selectOption(action); await seek(2.5);
        const held = await hash(); await page.waitForTimeout(220); assert.equal(await hash(), held, 'pause is stable');
        await page.getByRole('button', { name: '播放', exact: true }).click(); await page.waitForTimeout(280);
        await page.getByRole('button', { name: '暂停', exact: true }).click(); await page.waitForTimeout(60); assert.notEqual(await hash(), held);
        await seek(2.5); assert.equal(await hash(), held, 'arbitrary seek returns exact canvas');
        await page.getByRole('button', { name: '显示网格', exact: true }).click(); await page.getByRole('button', { name: '显示骨骼', exact: true }).click();
        await page.waitForTimeout(90); assert.notEqual(await hash(), held);
        await page.screenshot({ path: `${output}/${action}-grid.png` });
        await page.getByRole('button', { name: '显示身体', exact: true }).click(); await page.waitForTimeout(90);
        await page.screenshot({ path: `${output}/${action}-isolated.png` });
        await page.getByRole('button', { name: '显示身体', exact: true }).click();
        await page.getByRole('button', { name: '显示网格', exact: true }).click(); await page.getByRole('button', { name: '显示骨骼', exact: true }).click();
        await page.getByLabel('动作幅度', { exact: true }).fill('0'); await seek(0); const zero = await hash();
        await seek(2.5); assert.equal(await hash(), zero);
        await page.getByRole('button', { name: '原图', exact: true }).click(); await page.waitForTimeout(90);
        assert.equal(await hash(), zero, `${action} zero amount exactly matches its own complete-pose source image`);
        await page.getByRole('button', { name: '动作', exact: true }).click();
        await page.getByLabel('动作幅度', { exact: true }).fill('100');
    }
    await page.goto(`${base}/pet-layers.html?motion=1&action=side`, { waitUntil: 'networkidle' }); await page.waitForSelector('main[data-status="ready"]');
    assert.equal(await page.getByLabel('整身动作', { exact: true }).inputValue(), 'side');
    const surfaces = await page.evaluate(async () => {
        const { MeshBasicMaterial, Vector3 } = await import('/node_modules/three/build/three.module.js');
        const { createSitRest, applySitRest } = await import('/src/pet-layers/sit-rest.js');
        const { createSideRest, applySideRest } = await import('/src/pet-layers/side-rest.js');
        const { sampleMotion, MOTIONS } = await import('/src/pet-layers/motion.js');
        const results = [];
        for (const [action, path, create, apply] of [['sit', '/pet-motion/sit-rest.png', createSitRest, applySitRest], ['side', '/pet-motion/side-rest.png', createSideRest, applySideRest]]) {
            const image = new Image(); image.src = path; await image.decode();
            const canvas = document.createElement('canvas'); canvas.width = 452; canvas.height = 512;
            const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0); const rgba = ctx.getImageData(0, 0, 452, 512).data;
            const skin = create(new MeshBasicMaterial()), uv = skin.geometry.getAttribute('uv'), index = skin.geometry.index;
            const alpha = (x, y) => rgba[(Math.min(511, Math.max(0, Math.round(y))) * 452 + Math.min(451, Math.max(0, Math.round(x)))) * 4 + 3];
            const triangles = [];
            for (let i = 0; i < index.count; i += 3) {
                const ids = [index.getX(i), index.getX(i + 1), index.getX(i + 2)];
                const points = ids.map(id => [uv.getX(id) * 452, (1 - uv.getY(id)) * 512]);
                if (points.every(p => alpha(...p) > 220)) triangles.push(ids);
            }
            let minimum = Infinity, flipped = 0;
            for (let i = 0; i < 100; i++) {
                apply(skin, sampleMotion(i / 99 * MOTIONS[action], action, 1));
                for (const ids of triangles) {
                    const [a, b, c] = ids.map(id => skin.mesh.getVertexPosition(id, new Vector3()));
                    const area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    minimum = Math.min(minimum, area); if (area <= 0) flipped++;
                }
            }
            skin.geometry.dispose(); skin.skeleton.dispose(); skin.mesh.material.dispose();
            results.push({ action, triangles: triangles.length, frames: 100, minimum, flipped });
        }
        return results;
    });
    for (const surface of surfaces) { assert.ok(surface.triangles > 200); assert.equal(surface.flipped, 0, JSON.stringify(surface)); }
    assert.deepEqual(errors, []); assert.deepEqual(failures, []);
    await writeFile(`${output}/report.json`, JSON.stringify({ checks, surfaces, errors, failures }, null, 2));
    console.log('Postures passed: 60 real pixel/framing frames at five viewports; motion, pause/seek, overlays, side source identity, menu and links.');
} finally { await browser.close(); }
