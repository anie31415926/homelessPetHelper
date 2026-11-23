import assert from 'node:assert/strict';
import {mkdir, writeFile} from 'node:fs/promises';
import {chromium} from '@playwright/test';
import sharp from 'sharp';

const output = 'test-results/pet-sit-study'; await mkdir(output, {recursive: true});
const browser = await chromium.launch({channel: 'msedge', headless: true});
const errors = [], results = [], base = process.env.PET_SIT_URL || 'http://127.0.0.1:5193';
try {
    const page = await browser.newPage({reducedMotion: 'reduce'});
    page.on('pageerror', error => errors.push(error.message));
    page.on('response', response => {if (response.status() >= 400) errors.push(`${response.status()} ${response.url()}`);});
    const seek = async value => {
        value = Math.round(value * 1000) / 1000;
        await page.getByLabel('动作进度', {exact: true}).fill(String(value));
        await page.waitForFunction(value => Number(document.querySelector('canvas')?.dataset.progress) === value, value);
    };
    const pixels = async () => page.locator('canvas').evaluate(canvas => {
        const copy = document.createElement('canvas'); copy.width = canvas.width; copy.height = canvas.height;
        const context = copy.getContext('2d'); context.drawImage(canvas, 0, 0);
        const data = context.getImageData(0, 0, copy.width, copy.height).data;
        let count = 0, left = copy.width, right = 0, top = copy.height, bottom = 0;
        for (let p = 0; p < data.length; p += 4) {
            if (data[p] < 180 && data[p + 1] < 190 && data[p + 2] < 190) {
                const x = p / 4 % copy.width, y = Math.floor(p / 4 / copy.width);
                count++; left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
            }
        }
        return {count, left, right, top, bottom, width: copy.width, height: copy.height};
    });
    for (const [width, height] of [[1280, 900], [390, 844], [375, 667], [320, 568], [844, 390], [768, 1024]]) {
        await page.setViewportSize({width, height}); await page.goto(`${base}/pet-sit-study.html`);
        await page.locator('canvas').waitFor(); await seek(0);
        await page.waitForTimeout(120); assert.equal(await page.locator('canvas').getAttribute('data-progress'), '0');
        for (const at of [0, .2, .55, .86, 1]) {
            await seek(at); const bounds = await pixels();
            assert.ok(bounds.count > 600 && bounds.left > 2 && bounds.right < bounds.width - 2 && bounds.top > 2 && bounds.bottom < bounds.height - 2, JSON.stringify({width, at, bounds}));
            results.push({viewport: [width, height], at, bounds});
            if (width === 1280 || width === 390) {await page.getByRole('heading').click(); await page.screenshot({path: `${output}/${width}-${at}.png`, fullPage: true});}
        }
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
        assert.deepEqual(await page.evaluate(() => [...document.querySelectorAll('button,h1,label,output')].filter(el => el.clientWidth && (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent)), []);
        const targets = await page.locator('button,select,.options label').evaluateAll(elements => elements.map(el => ({label: el.textContent || el.getAttribute('aria-label'), width: el.getBoundingClientRect().width, height: el.getBoundingClientRect().height})));
        for (const target of targets) assert.ok(target.width >= 48 && target.height >= 48, JSON.stringify(target));
        await page.getByRole('button', {name: '站立', exact: false}).first().click();
        await page.getByRole('button', {name: '下一帧', exact: true}).click(); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) > 0);
        await page.getByRole('button', {name: '上一帧', exact: true}).click(); assert.equal(Number(await page.getByLabel('动作进度').inputValue()), 0);
        await page.getByLabel('关节', {exact: true}).uncheck(); await page.getByLabel('首尾叠影', {exact: true}).check();
        await page.getByLabel('播放速度', {exact: true}).selectOption('1');
        await page.getByRole('button', {name: '播放', exact: true}).click(); await page.waitForTimeout(150);
        await page.getByRole('button', {name: '暂停', exact: true}).click(); assert.ok(Number(await page.getByLabel('动作进度').inputValue()) > 0);
        await seek(1); await page.getByRole('button', {name: '播放', exact: true}).click(); await page.waitForTimeout(120);
        assert.ok(Number(await page.getByLabel('动作进度').inputValue()) < .2); await page.getByRole('button', {name: '暂停', exact: true}).click();
        await seek(.99); await page.getByRole('button', {name: '播放', exact: true}).click();
        await page.getByRole('button', {name: '播放', exact: true}).waitFor(); assert.equal(Number(await page.getByLabel('动作进度').inputValue()), 1);
        await page.screenshot({path: `${output}/${width}-controls.png`, fullPage: true});
    }
    await page.setViewportSize({width: 620, height: 700}); await page.getByLabel('关节', {exact: true}).check(); await page.getByLabel('首尾叠影', {exact: true}).uncheck();
    await page.getByRole('heading').click();
    const frames = [];
    for (const at of [0, .2, .55, .86, 1]) {await seek(at); frames.push(await sharp(await page.locator('.stage').screenshot()).resize(320, 230, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());}
    await sharp({create: {width: 1600, height: 230, channels: 4, background: '#f5f7f6'}}).composite(frames.map((input, i) => ({input, left: i * 320, top: 0}))).png().toFile(`${output}/contact.png`);
    if (process.argv.includes('--animation')) {
        const frames = [];
        for (let i = 0; i <= 60; i++) {await seek(i / 60); frames.push(await sharp(await page.locator('.stage').screenshot()).resize(620, 390, {fit: 'contain', background: '#f5f7f6'}).png().toBuffer());}
        const delay = frames.map((_, i) => i === 0 || i === 60 ? 700 : 100);
        await sharp(frames, {join: {animated: true}}).gif({delay, loop: 0, colours: 128}).toFile(`${output}/sit-skeleton.gif`);
        assert.equal((await sharp(`${output}/sit-skeleton.gif`, {animated: true}).metadata()).delay.reduce((sum, value) => sum + value, 0), 7300);
    }
    assert.deepEqual(errors, []); await writeFile(`${output}/browser.json`, JSON.stringify({errors, results}, null, 2));
    console.log(`PASS ${results.length} poses in 6 viewports; pixels, controls, ground framing, touch targets and text bounds`);
} finally {await browser.close();}
