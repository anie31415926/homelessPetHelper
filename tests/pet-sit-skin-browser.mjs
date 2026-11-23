import assert from 'node:assert/strict';
import {mkdir, writeFile} from 'node:fs/promises';
import {chromium} from '@playwright/test';
import sharp from 'sharp';

const output = 'test-results/pet-sit-skin'; await mkdir(output, {recursive: true});
const browser = await chromium.launch({channel: 'msedge', headless: true}), errors = [], results = [];
const base = process.env.PET_SIT_SKIN_URL || 'http://127.0.0.1:5193';
try {
    const page = await browser.newPage({reducedMotion: 'reduce'});
    page.on('pageerror', error => errors.push(error.message));
    page.on('response', response => {if (response.status() >= 400) errors.push(`${response.status()} ${response.url()}`);});
    const seek = async value => {
        value = Math.round(value * 1000) / 1000; await page.getByLabel('动作进度', {exact: true}).fill(String(value));
        await page.waitForFunction(value => Number(document.querySelector('canvas')?.dataset.progress) === value, value);
    };
    const pixels = async () => page.locator('canvas').evaluate(canvas => {
        const copy = document.createElement('canvas'); copy.width = canvas.width; copy.height = canvas.height;
        const context = copy.getContext('2d'); context.drawImage(canvas, 0, 0); const data = context.getImageData(0, 0, copy.width, copy.height).data;
        let count = 0, left = copy.width, right = 0, top = copy.height, bottom = 0;
        for (let p = 0; p < data.length; p += 4) {
            const r = data[p], g = data[p + 1], b = data[p + 2];
            if (Math.max(r, g, b) - Math.min(r, g, b) > 18 && Math.min(r, g, b) < 230) {
                const x = p / 4 % copy.width, y = Math.floor(p / 4 / copy.width); count++; left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
            }
        }
        return {count, left, right, top, bottom, width: copy.width, height: copy.height};
    });
    for (const [width, height] of [[1280, 900], [390, 844], [375, 667], [320, 568], [844, 390], [768, 1024]]) {
        await page.setViewportSize({width, height}); await page.goto(`${base}/pet-sit-skin.html`);
        await page.waitForFunction(() => !document.querySelector('button[aria-label="播放"]')?.disabled);
        await seek(0); await page.waitForTimeout(120); assert.equal(await page.locator('canvas').getAttribute('data-progress'), '0');
        const signatures = new Set();
        for (const at of [0, .1, .2, .375, .55, .7, .86, .95, 1]) {
            await seek(at); const bounds = await pixels();
            assert.ok(bounds.count > 1800 && bounds.left > 2 && bounds.right < bounds.width - 2 && bounds.top > 2 && bounds.bottom < bounds.height - 2, JSON.stringify({width, at, bounds}));
            results.push({viewport: [width, height], at, bounds}); signatures.add(await page.locator('canvas').evaluate(canvas => canvas.toDataURL()));
            if ((width === 1280 || width === 390) && [0, .2, .55, .86, 1].includes(at)) {await page.getByRole('heading').click(); await page.screenshot({path: `${output}/${width}-${at}.png`, fullPage: true});}
        }
        assert.equal(signatures.size, 9);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        assert.deepEqual(await page.evaluate(() => [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent)), []);
        for (const target of await page.locator('button,select,.options label').evaluateAll(elements => elements.map(el => ({name: el.textContent || el.getAttribute('aria-label'), width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height})))) assert.ok(target.width >= 48 && target.height >= 48, JSON.stringify(target));
        const image = () => page.locator('canvas').evaluate(canvas => canvas.toDataURL());
        await seek(.55); const skin = await image(); await page.waitForTimeout(150); assert.equal(await image(), skin);
        await page.getByRole('button', {name: '叠骨', exact: true}).click(); assert.notEqual(await image(), skin);
        const green = await page.locator('canvas').evaluate(canvas => {
            const c = document.createElement('canvas'); c.width = canvas.width; c.height = canvas.height; const ctx = c.getContext('2d'); ctx.drawImage(canvas, 0, 0);
            const data = ctx.getImageData(0, 0, c.width, c.height).data; let green = 0;
            for (let i = 0; i < data.length; i += 4) if (data[i] < data[i + 1] * .7 && data[i + 1] > data[i + 2] * 1.05) green++;
            return green;
        });
        assert.ok(green > 40, `Bone overlay hidden beneath translucent artwork: ${green}`);
        await page.screenshot({path: `${output}/${width}-overlay.png`, fullPage: true});
        await page.getByRole('button', {name: '骨架', exact: true}).click(); assert.notEqual(await image(), skin);
        const boneBounds = await pixels(); assert.ok(boneBounds.count > 50); assert.equal(await page.locator('canvas').getAttribute('data-mode'), 'bones');
        await page.getByRole('button', {name: '贴图', exact: true}).click(); await page.getByLabel('深底', {exact: true}).check(); await page.screenshot({path: `${output}/${width}-dark.png`, fullPage: true}); await page.getByLabel('深底', {exact: true}).uncheck();
        await page.getByRole('button', {name: '下一帧', exact: true}).click(); const next = Number(await page.getByLabel('动作进度').inputValue()); assert.ok(next > .55);
        await page.getByRole('button', {name: '上一帧', exact: true}).click(); assert.ok(Math.abs(Number(await page.getByLabel('动作进度').inputValue()) - .55) < .001);
        await page.getByLabel('播放速度', {exact: true}).selectOption('1'); await seek(0); await page.getByRole('button', {name: '播放', exact: true}).click(); await page.waitForTimeout(160); await page.getByRole('button', {name: '暂停', exact: true}).click(); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) > 0);
        await seek(.99); await page.getByRole('button', {name: '播放', exact: true}).click(); await page.getByRole('button', {name: '播放', exact: true}).waitFor(); assert.equal(Number(await page.getByLabel('动作进度').inputValue()), 1);
        await page.getByRole('button', {name: '播放', exact: true}).click(); await page.waitForTimeout(120); await page.getByRole('button', {name: '暂停', exact: true}).click(); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) < .2);
        await page.getByRole('button', {name: '回到站立', exact: true}).click(); assert.equal(Number(await page.getByLabel('动作进度').inputValue()), 0);
    }
    await page.setViewportSize({width: 620, height: 900}); const contact = [];
    const canvasFrame = async () => Buffer.from((await page.locator('canvas').evaluate(canvas => canvas.toDataURL('image/png'))).split(',')[1], 'base64');
    for (const at of [0, .2, .55, .86, 1]) {await seek(at); contact.push(await sharp(await canvasFrame()).resize(300, 340, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());}
    await sharp({create: {width: 1500, height: 340, channels: 4, background: '#f5f7f6'}}).composite(contact.map((input, i) => ({input, left: i * 300, top: 0}))).png().toFile(`${output}/contact.png`);
    if (process.argv.includes('--animation')) {
        const normal = [], overlays = [];
        for (let i = 0; i <= 60; i++) {
            await seek(i / 60); await page.getByRole('button', {name: '贴图', exact: true}).click(); normal.push(await sharp(await canvasFrame()).resize(480, 530, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());
            await page.getByRole('button', {name: '叠骨', exact: true}).click(); overlays.push(await sharp(await canvasFrame()).resize(480, 530, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());
        }
        for (const [name, frames] of [['sit-textured', normal], ['sit-overlay', overlays]]) {
            await sharp(frames, {join: {animated: true}}).gif({delay: frames.map((_, i) => i === 0 || i === 60 ? 700 : 100), loop: 0, colours: 192}).toFile(`${output}/${name}.gif`);
            assert.equal((await sharp(`${output}/${name}.gif`, {animated: true}).metadata()).delay.reduce((sum, value) => sum + value, 0), 7300);
        }
    }
    assert.deepEqual(errors, []); await writeFile(`${output}/browser.json`, JSON.stringify({errors, results}, null, 2)); console.log(`PASS ${results.length} skin poses, 6 viewports, modes/transport/background and pixels/text/touch bounds`);
} finally {await browser.close();}
