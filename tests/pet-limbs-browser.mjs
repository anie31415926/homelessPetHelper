import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';
const base = process.env.PET_LAYERS_URL || 'http://127.0.0.1:5193', output = 'test-results/pet-limbs-copy';
await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) { if (!error.message.includes("Executable doesn't exist")) throw error; browser = await chromium.launch({ channel: 'msedge', headless: true }); }
const page = await browser.newPage({ reducedMotion: 'reduce' }), errors = [], failures = [], checks = [];
page.on('pageerror', error => errors.push(error.message)); page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
const sample = () => page.locator('canvas').evaluate(canvas => {
    const copy = document.createElement('canvas'); copy.width = canvas.width; copy.height = canvas.height;
    const ctx = copy.getContext('2d'); ctx.drawImage(canvas, 0, 0);
    const data = ctx.getImageData(0, 0, copy.width, copy.height).data, old = window.__limbPixels;
    let pixels = 0, difference = 0, upperDifference = 0;
    for (let p = 0; p < data.length; p++) {
        if (p % 4 === 3 && data[p] > 64) pixels++;
        if (old?.length === data.length && Math.abs(data[p] - old[p]) > 4) { difference++; if (p < data.length * .58) upperDifference++; }
    }
    window.__limbPixels = data; return { pixels, difference, upperDifference };
});
const checkAssets = () => page.evaluate(async () => {
        const load = async path => {
            const image = new Image(); image.src = path; await image.decode();
            const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
            if (image.width !== 452 || image.height !== 512) throw new Error('Full limb coordinates changed');
            const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0); return ctx.getImageData(0, 0, 452, 512).data;
        };
        const source = await load('/pet-mesh/cat-side.png'), front = await load('/pet-limbs/foreground.png');
        const cuts = await Promise.all(['front', 'hind'].map(kind => load(`/art-source/pet-limbs/${kind}-cut.png`)));
        let foregroundViolations = 0;
        for (let p = 0; p < source.length; p += 4) {
            if (cuts.some(cut => cut[p + 3])) continue;
            for (let c = 0; c < 4; c++) if (source[p + c] !== front[p + c]) foregroundViolations++;
        }
        const limbs = [];
        for (const kind of ['front', 'hind']) {
            const [data, mask, protectedMask] = await Promise.all([
                load(`/pet-limbs/far-${kind}.png`), load(`/art-source/pet-limbs/${kind}-silhouette.png`), load(`/art-source/pet-limbs/${kind}-protected.png`),
            ]);
            let rootY = 512, pawY = 0, protectedPixels = 0, violations = 0;
            const seen = new Uint8Array(452 * 512), queue = []; let components = 0;
            for (let p = 0; p < data.length; p += 4) {
                if (data[p + 3] !== mask[p + 3]) violations++;
                if (data[p + 3] > 64) { rootY = Math.min(rootY, Math.floor(p / 4 / 452)); pawY = Math.max(pawY, Math.floor(p / 4 / 452)); }
                if (!protectedMask[p + 3]) continue;
                protectedPixels++;
                for (let c = 0; c < 4; c++) if (data[p + c] !== source[p + c]) violations++;
            }
            for (let i = 0; i < seen.length; i++) {
                if (seen[i] || data[i * 4 + 3] <= 64) continue;
                components++; queue.length = 0; queue.push(i); seen[i] = 1;
                for (let read = 0; read < queue.length; read++) {
                    const j = queue[read], x = j % 452, y = Math.floor(j / 452);
                    for (const next of [x ? j - 1 : -1, x < 451 ? j + 1 : -1, y ? j - 452 : -1, y < 511 ? j + 452 : -1]) {
                        if (next < 0 || seen[next] || data[next * 4 + 3] <= 64) continue;
                        seen[next] = 1; queue.push(next);
                    }
                }
            }
            limbs.push({ kind, rootY, pawY, protectedPixels, violations, components });
        }
        return { foregroundViolations, limbs };
    });
const checkCopies = () => page.evaluate(async () => {
    const { records } = await (await fetch('/art-source/pet-limbs/donor/derivation.json')).json();
    const load = async path => {
        const image = new Image(); image.src = path; await image.decode();
        const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
        if (image.width !== 452 || image.height !== 512) throw new Error('Copy canvas coordinates changed');
        const context = canvas.getContext('2d'); context.drawImage(image, 0, 0); return context.getImageData(0, 0, 452, 512).data;
    };
    const original = await load('/pet-mesh/cat-side.png'), results = [];
    for (const kind of ['front', 'hind']) {
        const donor = await load(`/art-source/pet-limbs/donor/${kind}-source.png`), mask = await load(`/art-source/pet-limbs/donor/${kind}-mask.png`);
        const copy = await load(`/pet-limbs/far-${kind}-copy.png`), record = records[kind], [dx, dy] = record.offset;
        let alphaViolations = 0, colourViolations = 0, originalViolations = 0, sourcePixels = 0, copiedPixels = 0;
        let left = 452, right = -1, top = 512, bottom = -1;
        for (let y = 0; y < 512; y++) for (let x = 0; x < 452; x++) {
            const p = (y * 452 + x) * 4, tx = x + dx, ty = y + dy;
            if (copy[p + 3] > 64) { copiedPixels++; left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y); }
            if (mask[p + 3] === 255 && original[p + 3] === 255) for (let c = 0; c < 4; c++) if (original[p + c] !== donor[p + c]) originalViolations++;
            if (donor[p + 3] > 64) sourcePixels++;
            if (tx < 0 || ty < 0 || tx >= 452 || ty >= 512) { if (donor[p + 3]) alphaViolations++; continue; }
            const target = (ty * 452 + tx) * 4;
            if (copy[target + 3] !== donor[p + 3]) alphaViolations++;
            if (donor[p + 3] <= 64) continue;
            const shade = .94 - .04 * Math.max(0, 1 - (y - record.rootY) / 150);
            for (let c = 0; c < 3; c++) if (Math.abs(copy[target + c] - Math.round(donor[p + c] * shade)) > 3) colourViolations++;
        }
        results.push({ kind, alphaViolations, colourViolations, originalViolations, sourcePixels, copiedPixels, width: right - left + 1, height: bottom - top + 1, record });
    }
    return results;
});
try {
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height }); await page.goto(`${base}/pet-layers.html`, { waitUntil: 'networkidle' });
        await page.waitForSelector('main[data-status="ready"]'); await page.waitForTimeout(100);
        const geometry = await page.evaluate(() => ({ overflow: document.documentElement.scrollWidth > innerWidth,
            clipped: [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent) }));
        assert.equal(geometry.overflow, false); assert.deepEqual(geometry.clipped, []); assert.ok((await sample()).pixels > 5000);
        await page.screenshot({ path: `${output}/preview-${width}x${height}.png`, fullPage: true }); checks.push({ width, height, ...geometry });
    }
    const assets = await checkAssets();
    assert.equal(assets.foregroundViolations, 0);
    for (const limb of assets.limbs) {
        assert.equal(limb.violations, 0); assert.equal(limb.components, 1);
        assert.ok(limb.rootY < 325 && limb.pawY > 480 && limb.pawY - limb.rootY > 160, 'complete shoulder/hip to paw, not a short patch');
        assert.ok(limb.protectedPixels > 200);
    }
    const copies = await checkCopies();
    for (const copy of copies) {
        assert.equal(copy.alphaViolations, 0); assert.equal(copy.colourViolations, 0); assert.equal(copy.originalViolations, 0);
        assert.equal(copy.sourcePixels, copy.copiedPixels); assert.ok(copy.copiedPixels > 11000);
        assert.equal(copy.width, copy.record.sourceExtent.width); assert.equal(copy.height, copy.record.sourceExtent.height);
        assert.deepEqual(copy.record.scale, [1, 1]);
    }
    await page.setViewportSize({ width: 390, height: 780 }); await page.getByLabel('动作', { exact: true }).selectOption('rest');
    assert.equal(await page.getByLabel('完整腿来源', { exact: true }).inputValue(), 'copy');
    await page.waitForTimeout(100); await sample(); await page.getByRole('button', { name: '原图', exact: true }).click(); await page.waitForTimeout(100);
    const rest = await sample(); assert.equal(rest.upperDifference, 0); assert.ok(rest.difference < 25000);
    await page.getByRole('button', { name: '分层', exact: true }).click();
    const limbs = [];
    for (const version of ['copy', 'ai']) for (const kind of ['front', 'hind', 'both']) {
        await page.getByLabel('完整腿来源', { exact: true }).selectOption(version);
        await page.getByLabel('控制部位', { exact: true }).selectOption(kind); await page.getByLabel('动作', { exact: true }).selectOption('rest');
        await page.waitForTimeout(100); await sample(); await page.getByLabel('动作', { exact: true }).selectOption('forward'); await page.waitForTimeout(100);
        const motion = await sample(); assert.ok(motion.difference > 100); assert.equal(motion.upperDifference, 0);
        await page.screenshot({ path: `${output}/${version}-${kind}-forward.png` });
        await page.getByRole('button', { name: '显示前景', exact: true }).click(); await page.getByLabel('动作', { exact: true }).selectOption('rest'); await page.waitForTimeout(100);
        const isolated = await sample(); assert.ok(isolated.pixels > 1500 && isolated.pixels < 18000);
        await page.screenshot({ path: `${output}/${version}-${kind}-complete.png` });
        await page.getByLabel('动作', { exact: true }).selectOption('tuck'); await page.waitForTimeout(100); await page.screenshot({ path: `${output}/${version}-${kind}-tuck.png` });
        await page.getByRole('button', { name: '显示前景', exact: true }).click(); limbs.push({ version, kind, motion, isolated });
    }
    await page.getByLabel('完整腿来源', { exact: true }).selectOption('copy');
    await page.getByLabel('动作', { exact: true }).selectOption('rest'); await page.waitForTimeout(100); await sample();
    await page.getByLabel('完整腿来源', { exact: true }).selectOption('ai'); await page.waitForTimeout(100);
    const comparison = await sample(); assert.ok(comparison.difference > 1000); assert.equal(comparison.upperDifference, 0);
    await page.getByLabel('完整腿来源', { exact: true }).selectOption('copy');
    await sample(); await page.getByRole('button', { name: '显示网格', exact: true }).click(); await page.waitForTimeout(100);
    assert.ok((await sample()).difference > 100);
    await page.getByRole('button', { name: '显示骨骼', exact: true }).click();
    await page.waitForTimeout(100); await page.screenshot({ path: `${output}/mesh-bones.png` });
    await page.getByRole('button', { name: '复位', exact: true }).click(); await page.getByRole('button', { name: '播放', exact: true }).click();
    await page.waitForTimeout(150); await sample(); await page.waitForTimeout(300); const playing = await sample(); assert.ok(playing.difference > 100);
    await page.getByRole('button', { name: '暂停', exact: true }).click(); await page.waitForTimeout(100); await sample(); await page.waitForTimeout(150); assert.equal((await sample()).difference, 0);
    await page.getByRole('slider', { name: '腿根', exact: true }).fill('-25');
    await page.getByRole('slider', { name: '屈膝', exact: true }).fill('40'); await page.waitForTimeout(100); assert.ok((await sample()).difference > 100);
    await page.screenshot({ path: `${output}/manual-stress.png` });
    await page.setViewportSize({ width: 375, height: 667 }); await page.waitForTimeout(100); assert.ok((await sample()).pixels > 5000);
    assert.deepEqual(errors, []); assert.deepEqual(failures, []);
    await writeFile(`${output}/browser-report.json`, JSON.stringify({ checks, assets, copies, rest, comparison, limbs, playing, errors, failures }, null, 2));
    console.log('Same-source copied limbs passed: no size/alpha loss, exact near-leg source, shadow-only RGB, old AI comparison, five viewports, motion and occlusion, pause, mesh and bones.');
} finally { await browser.close(); }
