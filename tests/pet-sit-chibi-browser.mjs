import assert from 'node:assert/strict';
import {mkdir, writeFile} from 'node:fs/promises';
import {chromium} from '@playwright/test';
import sharp from 'sharp';

const output = 'test-results/pet-sit-chibi'; await mkdir(output, {recursive: true});
const browser = await chromium.launch({channel: 'msedge', headless: true}), errors = [], results = [], comparisons = [];
const base = process.env.PET_SIT_CHIBI_URL || 'http://127.0.0.1:5193';
try {
    const page = await browser.newPage({reducedMotion: 'reduce'});
    page.on('pageerror', error => errors.push(error.message));
    page.on('response', response => {if (response.status() >= 400) errors.push(`${response.status()} ${response.url()}`);});
    const seek = async value => {
        if (await page.locator('canvas').getAttribute('data-mode') === 'source') await page.getByRole('button', {name: '贴图', exact: true}).click();
        value = Math.round(value * 1000) / 1000; await page.getByLabel('动作进度', {exact: true}).fill(String(value));
        await page.waitForFunction(value => Number(document.querySelector('canvas')?.dataset.progress) === value && document.querySelector('canvas')?.dataset.mode !== 'source', value);
    };
    const frame = async () => Buffer.from((await page.locator('canvas').evaluate(canvas => canvas.toDataURL('image/png'))).split(',')[1], 'base64');
    function bounds(data, info) {
        let count = 0, left = info.width, right = 0, top = info.height, bottom = 0;
        for (let i = 0; i < data.length; i += 4) {
            const r = data[i], g = data[i + 1], b = data[i + 2];
            if (Math.max(r, g, b) - Math.min(r, g, b) > 18 && Math.min(r, g, b) < 230) {const x = i / 4 % info.width, y = Math.floor(i / 4 / info.width); count++; left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);}
        }
        return {count, left, right, top, bottom, width: info.width, height: info.height};
    }
    const raw = async () => sharp(await frame()).ensureAlpha().raw().toBuffer({resolveWithObject: true});
    for (const [width, height] of [[1280, 900], [390, 844], [375, 667], [320, 568], [844, 390], [768, 1024]]) {
        await page.setViewportSize({width, height}); await page.goto(`${base}/pet-sit-chibi.html`); await page.waitForFunction(() => !document.querySelector('button[aria-label="播放"]')?.disabled);
        await seek(0); await page.waitForTimeout(100); assert.equal(await page.locator('canvas').getAttribute('data-progress'), '0'); const standing = await raw();
        await page.getByRole('button', {name: '原图', exact: true}).click(); await page.waitForFunction(() => document.querySelector('canvas')?.dataset.mode === 'source'); const source = await raw();
        const a = bounds(standing.data, standing.info), b = bounds(source.data, source.info); let union = 0, intersection = 0, difference = 0;
        for (let i = 0; i < standing.data.length; i += 4) {
            const occupied = data => Math.max(data[i], data[i + 1], data[i + 2]) - Math.min(data[i], data[i + 1], data[i + 2]) > 18 && Math.min(data[i], data[i + 1], data[i + 2]) < 230;
            const fa = occupied(standing.data), fb = occupied(source.data);
            if (fa || fb) {union++; for (let j = 0; j < 3; j++) difference += Math.abs(standing.data[i + j] - source.data[i + j]);} if (fa && fb) intersection++;
        }
        const comparison = {viewport: [width, height], iou: intersection / union, meanChannelDifference: difference / (union * 3), standing: a, source: b}; comparisons.push(comparison);
        assert.ok(comparison.iou > .94, JSON.stringify(comparison));
        for (const edge of ['left', 'right', 'top', 'bottom']) assert.ok(Math.abs(a[edge] - b[edge]) <= 2, JSON.stringify(comparison));
        if (width === 1280 || width === 390) {await page.getByRole('heading').click(); await page.screenshot({path: `${output}/${width}-source.png`, fullPage: true});}
        await page.getByRole('button', {name: '贴图', exact: true}).click();
        const signatures = new Set();
        for (const at of [0, .1, .2, .375, .55, .7, .86, .95, 1]) {
            await seek(at); const pixels = await raw(), box = bounds(pixels.data, pixels.info);
            assert.ok(box.count > 1800 && box.left > 2 && box.right < box.width - 2 && box.top > 2 && box.bottom < box.height - 2, JSON.stringify({width, at, box})); results.push({viewport: [width, height], at, box}); signatures.add((await frame()).toString('base64'));
            if ((width === 1280 || width === 390) && [0, .2, .55, .86, 1].includes(at)) {await page.getByRole('heading').click(); await page.screenshot({path: `${output}/${width}-${at}.png`, fullPage: true});}
        }
        assert.equal(signatures.size, 9); assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        assert.deepEqual(await page.evaluate(() => [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent)), []);
        for (const target of await page.locator('button,select,.options label').evaluateAll(elements => elements.map(el => ({name: el.textContent || el.getAttribute('aria-label'), width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height})))) assert.ok(target.width >= 48 && target.height >= 48, JSON.stringify(target));
        await seek(.55); const held = await frame(); await page.waitForTimeout(140); assert.ok((await frame()).equals(held));
        await page.getByRole('button', {name: '叠骨', exact: true}).click(); const overlay = await raw(); let green = 0;
        for (let i = 0; i < overlay.data.length; i += 4) if (overlay.data[i] < overlay.data[i + 1] * .7 && overlay.data[i + 1] > overlay.data[i + 2] * 1.05) green++;
        assert.ok(green > 40); await page.screenshot({path: `${output}/${width}-overlay.png`, fullPage: true});
        await page.getByRole('button', {name: '骨架', exact: true}).click(); assert.ok(!(await frame()).equals(held));
        await page.getByRole('button', {name: '贴图', exact: true}).click(); await page.getByLabel('深底', {exact: true}).check(); await page.screenshot({path: `${output}/${width}-dark.png`, fullPage: true}); await page.getByLabel('深底', {exact: true}).uncheck();
        await page.getByRole('button', {name: '下一帧', exact: true}).click(); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) > .55); await page.getByRole('button', {name: '上一帧', exact: true}).click(); assert.ok(Math.abs(Number(await page.getByLabel('动作进度').inputValue()) - .55) < .001);
        await page.getByLabel('播放速度', {exact: true}).selectOption('1'); await seek(0); await page.getByRole('button', {name: '播放', exact: true}).click(); await page.waitForTimeout(160); await page.getByRole('button', {name: '暂停', exact: true}).click(); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) > 0);
        await page.getByRole('button', {name: '原图', exact: true}).click(); await page.getByRole('button', {name: '播放', exact: true}).click(); await page.waitForTimeout(120); await page.getByRole('button', {name: '暂停', exact: true}).click(); assert.equal(await page.locator('canvas').getAttribute('data-mode'), 'skin'); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) < .2);
        await seek(.99); await page.getByRole('button', {name: '播放', exact: true}).click(); await page.getByRole('button', {name: '播放', exact: true}).waitFor(); assert.equal(Number(await page.getByLabel('动作进度').inputValue()), 1);
        await page.getByRole('button', {name: '回到站立', exact: true}).click(); assert.equal(Number(await page.getByLabel('动作进度').inputValue()), 0);
    }
    await page.setViewportSize({width: 620, height: 900}); const contact = [];
    for (const at of [0, .2, .55, .86, 1]) {await seek(at); contact.push(await sharp(await frame()).resize(300, 340, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());}
    await sharp({create: {width: 1500, height: 340, channels: 4, background: '#f5f7f6'}}).composite(contact.map((input, i) => ({input, left: i * 300, top: 0}))).png().toFile(`${output}/contact.png`);
    await seek(0); const standing = await sharp(await frame()).resize(420, 480, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer(); await page.getByRole('button', {name: '原图', exact: true}).click();
    const original = await sharp(await frame()).resize(420, 480, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer();
    await sharp({create: {width: 840, height: 480, channels: 4, background: '#f5f7f6'}}).composite([{input: original, left: 0, top: 0}, {input: standing, left: 420, top: 0}]).png().toFile(`${output}/source-standing.png`);
    if (process.argv.includes('--animation')) {
        const normal = [], overlays = [];
        for (let i = 0; i <= 60; i++) {
            await seek(i / 60); await page.getByRole('button', {name: '贴图', exact: true}).click(); normal.push(await sharp(await frame()).resize(480, 530, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());
            await page.getByRole('button', {name: '叠骨', exact: true}).click(); overlays.push(await sharp(await frame()).resize(480, 530, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());
        }
        for (const [name, frames] of [['sit-chibi', normal], ['sit-chibi-overlay', overlays]]) {await sharp(frames, {join: {animated: true}}).gif({delay: frames.map((_, i) => i === 0 || i === 60 ? 700 : 100), loop: 0, colours: 192}).toFile(`${output}/${name}.gif`); assert.equal((await sharp(`${output}/${name}.gif`, {animated: true}).metadata()).delay.reduce((sum, value) => sum + value, 0), 7300);}
    }
    assert.deepEqual(errors, []); await writeFile(`${output}/browser.json`, JSON.stringify({errors, results, comparisons}, null, 2)); console.log(`PASS ${results.length} poses/6 viewports, native source comparison, modes/controls, pixels/text/touch bounds`); console.log(comparisons.map(({viewport, iou, meanChannelDifference}) => ({viewport, iou, meanChannelDifference})));
} finally {await browser.close();}
