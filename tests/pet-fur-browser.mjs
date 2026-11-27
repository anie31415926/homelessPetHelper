import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { chromium, expect } from '@playwright/test';
import sharp from 'sharp';

const output = new URL('../test-results/pet-fur/', import.meta.url);
const url = new URL('../public/pet-fur/index.html', import.meta.url);
const files = ['public/pet-fur/index.html', 'public/pet-fur/style.css', 'public/pet-fur/fur.js',
    'public/pet-affection/shiba.webp', 'tests/pet-fur-browser.mjs'];
async function inputs() {
    return Object.fromEntries(await Promise.all(files.map(async file => [file,
        createHash('sha256').update(await readFile(new URL(`../${file}`, import.meta.url))).digest('hex'),
    ])));
}
const beforeInputs = await inputs();
await mkdir(output, { recursive: true });
const errors = [];
const report = {};

async function launch() {
    try { return await chromium.launch(); }
    catch (error) {
        if (!error.message.includes("Executable doesn't exist")) throw error;
        return chromium.launch({ channel: 'msedge' });
    }
}

async function open(page) {
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(url.href);
    await expect(page.locator('main')).toHaveAttribute('data-ready', 'ready');
}

const button = (page, name) => page.getByRole('button', { name, exact: true });
const bounds = page => page.locator('canvas').boundingBox();
const at = (box, x, y) => ({ x: box.x + (x - 70) / 400 * box.width, y: box.y + (y - 33) / 470 * box.height });
const photo = async page => page.screenshot({ clip: await bounds(page) });
const save = (name, png) => writeFile(new URL(name, output), png);

async function pixels(before, after, startY = 0, endY = 1, threshold = 20) {
    const { data: a, info } = await sharp(before).removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const b = await sharp(after).removeAlpha().raw().toBuffer();
    assert.equal(a.length, b.length);
    let changed = 0;
    const stride = info.width * info.channels;
    for (let i = Math.floor(info.height * startY) * stride; i < Math.floor(info.height * endY) * stride; i += info.channels) {
        if (Math.abs(a[i] - b[i]) + Math.abs(a[i + 1] - b[i + 1]) + Math.abs(a[i + 2] - b[i + 2]) > threshold) changed++;
    }
    return changed;
}

async function stroke(page, fromY, toY, x = 175) {
    const box = await bounds(page);
    const start = at(box, x, fromY);
    const end = at(box, x, toY);
    await page.mouse.move(start.x, start.y);
    await page.mouse.down();
    await page.mouse.move(end.x, end.y, { steps: 4 });
    await page.waitForTimeout(30);
    return photo(page);
}

const browser = await launch();
try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await open(page);
    for (const [width, height] of [[1280, 900], [390, 844], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        const layout = await page.evaluate(() => {
            const box = document.querySelector('canvas').getBoundingClientRect();
            return { width: box.width, height: box.height, bad: document.documentElement.scrollWidth > innerWidth ||
                document.querySelector('footer').getBoundingClientRect().bottom > innerHeight + 1 ||
                [...document.querySelectorAll('button')].some(el => el.scrollWidth > el.clientWidth + 1) };
        });
        assert.equal(layout.bad, false);
        assert.ok(layout.width >= 206 && Math.abs(layout.height / layout.width - 470 / 400) < .01);
        await save(`idle-${width}x${height}.png`, await page.screenshot());
    }

    await page.setViewportSize({ width: 390, height: 844 });
    const idle = await photo(page);
    const { entropy } = await sharp(idle).stats();
    assert.ok(entropy > 2, 'the portrait is visible');
    const down = await stroke(page, 269, 298);
    await page.mouse.up();
    await expect.poll(async () => pixels(idle, await photo(page), 0, 1, 0)).toBe(0);
    const up = await stroke(page, 327, 298);
    const changed = await pixels(idle, up, .46, .69);
    const directional = await pixels(down, up, .51, .63);
    assert.ok(changed > 100, `reverse stroke affects chest fur, got ${changed}`);
    assert.ok(directional > 50, `matching endpoints react differently by direction, got ${directional}`);
    assert.equal(await pixels(idle, up, 0, .40, 0), 0, 'chest does not animate head');
    assert.equal(await pixels(idle, up, .76, 1, 0), 0, 'legs and shadow stay put');
    await save('reverse-390x844.png', await page.screenshot());
    await page.locator('canvas').dispatchEvent('pointercancel', { pointerId: 1 });
    await expect.poll(async () => pixels(idle, await photo(page), 0, 1, 0)).toBe(0);
    await page.mouse.up();

    const forehead = await stroke(page, 93, 113, 201);
    assert.ok(await pixels(idle, forehead, .08, .22) > 35, 'forehead has its own fur');
    assert.equal(await pixels(idle, forehead, .43, 1, 0), 0, 'head contact leaves chest and feet alone');
    await page.mouse.up();

    await button(page, '固定毛发').click();
    const fixed = await stroke(page, 327, 298);
    assert.equal(await pixels(idle, fixed, 0, 1, 0), 0);
    await page.mouse.up();
    await button(page, '动态毛发').click();
    await page.locator('canvas').focus();
    const focused = await photo(page);
    await page.keyboard.press('Enter');
    assert.ok(await pixels(focused, await photo(page), .45, .68) > 25, 'keyboard triggers a stroke');
    const { width, height } = await sharp(idle).metadata();
    await save('comparison.png', await sharp({ create: { width: width * 3, height, channels: 3, background: '#d6e8da' } })
        .composite([idle, down, up].map((input, i) => ({ input, left: i * width, top: 0 })))
        .png().toBuffer());
    report.desktop = { changed, directional };

    const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    await open(mobile);
    const cdp = await mobile.context().newCDPSession(mobile);
    const box = await bounds(mobile);
    const calm = await photo(mobile);
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [at(box, 175, 327)] });
    for (const y of [320, 313, 305, 298]) {
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [at(box, 175, y)] });
    }
    report.touch = await pixels(calm, await photo(mobile), .46, .69);
    assert.ok(report.touch > 85, 'touch ruffles fur');
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await expect.poll(async () => pixels(calm, await photo(mobile), .46, .69)).toBe(0);

    const reduced = await browser.newPage({ viewport: { width: 390, height: 844 }, reducedMotion: 'reduce' });
    await open(reduced);
    const reducedIdle = await photo(reduced);
    await stroke(reduced, 327, 298);
    await reduced.mouse.up();
    await expect.poll(async () => pixels(reducedIdle, await photo(reduced), 0, 1, 0), { timeout: 750 }).toBe(0);
} finally {
    await browser.close();
}

assert.deepEqual(errors, []);
assert.deepEqual(await inputs(), beforeInputs);
await save('report.json', JSON.stringify({ ...report, beforeInputs }, null, 2));
console.log('Local HTML: layout, mouse, touch, direction, locality, recovery, static baseline, keyboard and reduced motion passed.');
