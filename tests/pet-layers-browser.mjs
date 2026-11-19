import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
const base = process.env.PET_LAYERS_URL || 'http://127.0.0.1:5193';
const output = 'test-results/pet-layers'; await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const page = await browser.newPage({ reducedMotion: 'reduce' }), errors = [], failures = [], checks = [];
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const sample = () => page.locator('canvas').evaluate(canvas => {
    const image = document.createElement('canvas'); image.width = canvas.width; image.height = canvas.height;
    const ctx = image.getContext('2d'); ctx.drawImage(canvas, 0, 0);
    const data = ctx.getImageData(0, 0, canvas.width, canvas.height).data, previous = window.__layerPixels;
    let difference = 0, upperDifference = 0, pixels = 0;
    for (let i = 0; i < data.length; i++) {
        if (i % 4 === 3 && data[i] > 64) pixels++;
        if (previous && previous.length === data.length && Math.abs(data[i] - previous[i]) > 4) {
            difference++; if (i < data.length * .6) upperDifference++;
        }
    }
    window.__layerPixels = data; return { difference, upperDifference, pixels };
});
try {
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height }); await page.goto(`${base}/pet-layers.html?legacy=1`, { waitUntil: 'networkidle' });
        await page.waitForSelector('main[data-status="ready"]'); await page.waitForTimeout(100);
        const geometry = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth > innerWidth,
            clipped: [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent) }));
        assert.equal(geometry.overflow, false); assert.deepEqual(geometry.clipped, []);
        assert.ok((await sample()).pixels > 5000);
        await page.screenshot({ path: `${output}/preview-${width}x${height}.png`, fullPage: true });
        checks.push({ width, height, ...geometry });
    }
    const assets = await page.evaluate(async () => {
        const load = async path => {
            const image = new Image(); image.src = path; await image.decode();
            const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
            const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0);
            return ctx.getImageData(0, 0, canvas.width, canvas.height).data;
        };
        const [original, front, direct, mask, ai, donor] = await Promise.all([
            '/pet-mesh/cat-side.png', '/pet-layers/foreground.png', '/art-source/pet-layers/visible-only.png',
            '/art-source/pet-layers/completion-mask.png', '/pet-layers/far-hind-ai.png', '/pet-layers/far-hind.png',
        ].map(load));
        let directPixels = 0, generatedPixels = 0, changed = 0, violations = 0;
        for (let p = 0; p < original.length; p += 4) {
            if (direct[p + 3]) {
                directPixels++;
                for (let c = 0; c < 4; c++) if (ai[p + c] !== original[p + c] || direct[p + c] !== original[p + c]) violations++;
                if (front[p + 3]) violations++;
            } else {
                for (let c = 0; c < 4; c++) if (front[p + c] !== original[p + c]) violations++;
                if (mask[p + 3]) {
                    generatedPixels++;
                    if (original[p + 3] !== 255 || ai[p + 3] !== mask[p + 3]) violations++;
                    if (Math.abs(ai[p] - donor[p]) + Math.abs(ai[p + 1] - donor[p + 1]) + Math.abs(ai[p + 2] - donor[p + 2]) > 6) changed++;
                } else if (ai[p + 3]) violations++;
            }
        }
        return { directPixels, generatedPixels, changed, violations };
    });
    assert.equal(assets.violations, 0, 'original paw locked, foreground unchanged, model pixels restricted to hidden mask');
    assert.ok(assets.directPixels > 1000 && assets.generatedPixels > 3000 && assets.changed > 1000);
    const cleanup = await page.evaluate(async () => {
        const load = async path => {
            const image = new Image(); image.src = path; await image.decode();
            const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
            const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0);
            return ctx.getImageData(0, 0, canvas.width, canvas.height).data;
        };
        const [source, before, clean, donor, front, silhouette, cut, protectedPaw] = await Promise.all([
            '/pet-mesh/cat-side.png', '/pet-layers/far-hind-ai.png', '/pet-layers/far-hind-ai-clean.png',
            '/pet-layers/far-hind-clean.png', '/pet-layers/foreground-clean.png',
            '/art-source/pet-layers/cleanup/silhouette.png', '/art-source/pet-layers/cleanup/foreground-cut-mask.png',
            '/art-source/pet-layers/cleanup/protected-paw.png',
        ].map(load));
        const width = 452, height = 512, index = (x, y) => (y * width + x) * 4;
        let violations = 0, protectedPixels = 0, altered = 0, foregroundDebris = 0;
        for (let p = 0; p < clean.length; p += 4) {
            if (clean[p + 3] !== silhouette[p + 3] || donor[p + 3] !== silhouette[p + 3]) violations++;
            if (!cut[p + 3]) for (let c = 0; c < 4; c++) if (front[p + c] !== source[p + c]) violations++;
            if (protectedPaw[p + 3]) {
                protectedPixels++;
                for (let c = 0; c < 4; c++) if (clean[p + c] !== source[p + c]) violations++;
            }
            if (Math.abs(clean[p] - before[p]) + Math.abs(clean[p + 1] - before[p + 1]) + Math.abs(clean[p + 2] - before[p + 2]) + Math.abs(clean[p + 3] - before[p + 3]) > 6) altered++;
        }
        for (const [x1, y1, x2, y2] of [[237, 438, 249, 449], [238, 483, 262, 488]]) {
            for (let y = y1; y <= y2; y++) for (let x = x1; x <= x2; x++) if (front[index(x, y) + 3] > 8) foregroundDebris++;
        }
        function topology(data) {
            const seen = new Uint8Array(width * height), sizes = [];
            let holes = 0, fringe = 0;
            const opaque = i => data[i * 4 + 3] >= 64;
            for (let start = 0; start < seen.length; start++) {
                if (seen[start]) continue;
                const filled = opaque(start), queue = [start]; seen[start] = 1;
                let touchesBorder = false;
                for (let read = 0; read < queue.length; read++) {
                    const i = queue[read], x = i % width, y = Math.floor(i / width);
                    if (x === 0 || y === 0 || x === width - 1 || y === height - 1) touchesBorder = true;
                    for (const next of [x ? i - 1 : -1, x + 1 < width ? i + 1 : -1, y ? i - width : -1, y + 1 < height ? i + width : -1]) {
                        if (next < 0 || seen[next] || opaque(next) !== filled) continue;
                        seen[next] = 1; queue.push(next);
                    }
                }
                if (filled) sizes.push(queue.length); else if (!touchesBorder) holes++;
            }
            for (let y = 1; y < height - 1; y++) for (let x = 1; x < width - 1; x++) {
                const p = index(x, y);
                if (data[p + 3] < 32 || Math.min(data[p], data[p + 1], data[p + 2]) < 226) continue;
                if ([-4, 4, -width * 4, width * 4].some(step => data[p + step + 3] < 64)) fringe++;
            }
            return { components: sizes.length, holes, whiteFringe: fringe };
        }
        return { violations, protectedPixels, altered, foregroundDebris, before: topology(before), after: topology(clean) };
    });
    assert.equal(cleanup.violations, 0, 'clean contour is shared, protected paw is unchanged, foreground changes only in the local cut');
    assert.ok(cleanup.protectedPixels > 800 && cleanup.altered > 400);
    assert.equal(cleanup.foregroundDebris, 0, 'old ankle spur and floor-line remnants are gone');
    assert.equal(cleanup.after.components, 1); assert.equal(cleanup.after.holes, 0);
    assert.ok(cleanup.after.whiteFringe < cleanup.before.whiteFringe, 'local decontamination reduces pale edge pixels');
    await page.setViewportSize({ width: 390, height: 780 });
    await page.getByLabel('动作', { exact: true }).selectOption('rest');
    await page.waitForTimeout(100); await sample();
    await page.getByRole('button', { name: '原图', exact: true }).click();
    await page.waitForTimeout(100); const rest = await sample();
    assert.ok(rest.difference < 1200, 'rest reassembly retains source silhouette and colour');
    await page.getByRole('button', { name: '分层', exact: true }).click();
    await page.waitForTimeout(100); await sample();
    await page.getByLabel('动作', { exact: true }).selectOption('forward');
    await page.waitForTimeout(100); const forward = await sample();
    assert.ok(forward.difference > 100, 'far leg moves behind foreground');
    assert.equal(forward.upperDifference, 0, 'upper body does not get pulled by the far leg');
    await page.screenshot({ path: `${output}/forward.png` });
    await page.getByLabel('动作', { exact: true }).selectOption('tuck'); await page.waitForTimeout(100);
    await page.screenshot({ path: `${output}/tuck.png` });
    await sample(); await page.getByRole('button', { name: '显示前景', exact: true }).click();
    await page.waitForTimeout(100); const isolated = await sample();
    assert.ok(isolated.pixels > 1000 && isolated.pixels < 10000, 'complete isolated far leg, not whole animal');
    await page.screenshot({ path: `${output}/isolated.png` });
    await sample(); await page.getByLabel('补全来源', { exact: true }).selectOption('before');
    await page.waitForTimeout(100); const beforeComparison = await sample();
    assert.ok(beforeComparison.difference > 200, 'cleanup comparison switches actual textures');
    await page.screenshot({ path: `${output}/isolated-before.png` });
    await page.locator('.stage').evaluate(stage => { stage.style.background = '#283e3b'; });
    await page.screenshot({ path: `${output}/isolated-before-dark.png` });
    await page.getByLabel('补全来源', { exact: true }).selectOption('ai'); await page.waitForTimeout(100);
    await page.screenshot({ path: `${output}/isolated-clean-dark.png` });
    await page.locator('.stage').evaluate(stage => { stage.style.background = ''; });
    await sample(); await page.getByLabel('补全来源', { exact: true }).selectOption('donor');
    await page.waitForTimeout(100); const comparison = await sample();
    assert.ok(comparison.difference > 200, 'model and same-image completion are independently selectable');
    await page.screenshot({ path: `${output}/isolated-donor.png` });
    await page.getByLabel('补全来源', { exact: true }).selectOption('ai');
    await page.waitForTimeout(100); await sample();
    await page.getByRole('button', { name: '显示网格', exact: true }).click(); await page.waitForTimeout(100);
    const wire = await sample(); assert.ok(wire.difference > 100, 'wire toggle draws real deformed triangle lines');
    await page.getByRole('button', { name: '显示骨骼', exact: true }).click(); await page.waitForTimeout(100);
    const bones = await sample(); assert.ok(bones.difference > 30, 'bone toggle draws real skeleton');
    await page.screenshot({ path: `${output}/isolated-mesh.png` });
    await page.getByRole('button', { name: '复位', exact: true }).click();
    await page.getByRole('button', { name: '播放', exact: true }).click();
    await page.waitForTimeout(200); await sample(); await page.waitForTimeout(250); const motion = await sample();
    assert.ok(motion.difference > 50); assert.equal(motion.upperDifference, 0);
    await page.getByRole('button', { name: '暂停', exact: true }).click();
    await page.waitForTimeout(100); await sample(); await page.waitForTimeout(150); assert.equal((await sample()).difference, 0);
    await page.getByRole('slider', { name: '腿根', exact: true }).fill('-25');
    await page.getByRole('slider', { name: '屈膝', exact: true }).fill('40');
    await page.waitForTimeout(100); assert.ok((await sample()).difference > 50);
    await page.setViewportSize({ width: 375, height: 667 }); await page.waitForTimeout(150); assert.ok((await sample()).pixels > 5000);
    assert.deepEqual(errors, []); assert.deepEqual(failures, []);
    await writeFile(`${output}/browser-report.json`, JSON.stringify({ checks, assets, cleanup, rest, forward, isolated, beforeComparison, comparison, wire, bones, motion, errors, failures }, null, 2));
    console.log('Far-hind layer passed: 5 viewports, source rest comparison, independent occluded motion, isolated leg, body invariance, manual controls and pause.');
} finally { await browser.close(); }
