import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium, expect } from '@playwright/test';
import sharp from 'sharp';

const output = 'test-results/pet-affection-v7';
await mkdir(output, { recursive: true });
const url = (process.env.PET_AFFECTION_URL || 'http://127.0.0.1:5197') + '/pet-affection.html';
const flags = ['--enable-webgl', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'];
async function launch(args) {
    try { return await chromium.launch({ args }); }
    catch (error) {
        if (!error.message.includes("Executable doesn't exist")) throw error;
        return chromium.launch({ channel: 'msedge', args });
    }
}
const browser = await launch(flags);
const errors = [], invalidResponses = [], checkpoints = [];

async function open(page) {
    page.on('pageerror', error => errors.push(error.message));
    page.on('response', response => { if (response.status() >= 400) invalidResponses.push(response.url()); });
    await page.goto(url);
    await expect(page.locator('main')).toHaveAttribute('data-ready', 'ready');
}

async function startRub(page, zone = 'head') {
    const { x, y, width, height } = await page.locator(`.pet-${zone}`).boundingBox();
    const at = (ratio, row = .5) => ({ x: x + width * ratio, y: y + height * row });
    const move = async (point, steps = 1) => page.mouse.move(point.x, point.y, { steps });
    await move(at(.34));
    await page.mouse.down();
    let beat = 0;
    return async () => {
        await page.waitForTimeout(105);
        await move(at(++beat % 2 ? .63 : .34), 5);
    };
}

const button = (page, name) => page.getByRole('button', { name, exact: true });
const face = (page, value) => expect(page.locator('main')).toHaveAttribute('data-face', value);
const clip = page => page.locator('.pet').screenshot();
async function countChanged(before, after, y0, y1, x0 = 0, x1 = 1) {
    const { data: a, info } = await sharp(before).raw().toBuffer({ resolveWithObject: true });
    const b = await sharp(after).raw().toBuffer();
    assert.equal(a.length, b.length);
    let changed = 0;
    const row = info.width * info.channels;
    for (let y = Math.floor(info.height * y0); y < Math.floor(info.height * y1); y++) {
        for (let x = Math.floor(info.width * x0); x < Math.floor(info.width * x1); x++) {
            const i = y * row + x * info.channels;
            if (Math.abs(a[i] - b[i]) + Math.abs(a[i + 1] - b[i + 1]) + Math.abs(a[i + 2] - b[i + 2]) > 12) changed++;
        }
    }
    return changed;
}

try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
    await open(page);
    await expect(button(page, '表情＋微动')).toBeEnabled();
    const main = page.locator('main');
    for (const [width, height] of [[1280, 900], [390, 844], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        const layout = await page.evaluate(() => ({
            overflow: document.documentElement.scrollWidth > innerWidth,
            bench: document.querySelector('.bench').getBoundingClientRect().bottom <= innerHeight + 1,
            canvas: document.querySelector('canvas').getBoundingClientRect().width,
            clipped: [...document.querySelectorAll('.bench button')].some(el => el.scrollWidth > el.clientWidth + 1),
        }));
        assert.equal(layout.overflow, false);
        assert.equal(layout.bench, true);
        assert.equal(layout.clipped, false);
        assert.ok(layout.canvas >= 225);
        await page.screenshot({ path: `${output}/calm-${width}x${height}.png` });
        checkpoints.push({ width, height, ...layout });
    }

    await page.setViewportSize({ width: 390, height: 844 });
    const headRub = await startRub(page);
    await page.waitForTimeout(580);
    await face(page, 'calm');
    for (let i = 0; i < 3; i++) await headRub();
    await face(page, 'soft');
    await expect(page.locator('.flower-burst')).toHaveCount(0);
    for (let i = 0; i < 6 && await main.getAttribute('data-face') !== 'happy'; i++) await headRub();
    await face(page, 'happy');
    await expect(page.locator('.flower-burst')).toHaveCount(1);
    await page.waitForTimeout(300);
    await page.screenshot({ path: `${output}/happy-390x844.png` });
    await page.mouse.up();
    await face(page, 'soft');
    await expect(page.locator('.flower-burst')).toHaveCount(0);
    await page.waitForTimeout(850);
    await face(page, 'soft');
    await page.screenshot({ path: `${output}/after-390x844.png` });
    const rested = await clip(page);
    await page.waitForTimeout(130);
    assert.equal(await countChanged(rested, await clip(page), .25, .51, .67, .97), 0, 'tail rests while the smile stays');
    await face(page, 'calm');
    await page.waitForTimeout(220);
    await page.screenshot({ path: `${output}/settled-390x844.png` });

    await button(page, '原图').click();
    const bodyRub = await startRub(page, 'body');
    for (let i = 0; i < 7; i++) await bodyRub();
    await face(page, 'calm');
    await expect(page.locator('.flower-burst')).toHaveCount(1);
    await page.mouse.up();
    await button(page, '彩虹小花').click();
    const still = await clip(page);

    await button(page, '换表情').click();
    const faceRub = await startRub(page, 'body');
    for (let i = 0; i < 3; i++) await faceRub();
    await face(page, 'soft');
    for (let i = 0; i < 4; i++) await faceRub();
    await face(page, 'happy');
    await page.waitForTimeout(190);
    const patch = await clip(page);
    await page.mouse.up();
    assert.ok(await countChanged(still, patch, .20, .50) > 600, 'patch changes face pixels');
    assert.equal(await countChanged(still, patch, .57, .95), 0, 'patch leaves all body pixels unchanged');

    await button(page, '表情＋微动').click();
    const meshRub = await startRub(page);
    for (let i = 0; i < 7; i++) await meshRub();
    await face(page, 'happy');
    await page.waitForTimeout(190);
    const mesh = await clip(page);
    const swingFrames = [mesh];
    let tailChanged = 0;
    for (let i = 0; i < 4; i++) {
        await meshRub();
        const frame = await clip(page);
        swingFrames.push(frame);
        tailChanged = Math.max(tailChanged, await countChanged(mesh, frame, .25, .55, .65, .97));
        assert.equal(await countChanged(mesh, frame, 265 / 512, 271 / 512, 405 / 512, 414 / 512), 0,
            'visible root fur does not move even while the curl rotates');
        assert.equal(await countChanged(mesh, frame, .59, .95), 0, 'stationary body does not deform with the tail');
        assert.equal(await countChanged(mesh, frame, .63, .95), 0, 'tail wag does not move the paws');
    }
    await page.mouse.up();
    assert.ok(await countChanged(patch, mesh, .08, .48, .14, .62) > 200, 'mesh changes head pixels');
    assert.ok(tailChanged >= 80, 'tail wiggles between two frames in the same pose');
    checkpoints.push({ movingTailPixels: tailChanged, movingRootPixels: 0, movingPawPixels: 0 });
    assert.equal(await countChanged(patch, mesh, .62, .95), 0, 'mesh leaves feet anchored');
    const { width, height } = await sharp(still).metadata();
    const region = { left: Math.floor(width * .61), top: Math.floor(height * .26), width: Math.floor(width * .34), height: Math.floor(height * .36) };
    const tailCrops = await Promise.all(swingFrames.map(input => sharp(input).extract(region).png().toBuffer()));
    await sharp({ create: { width: region.width * tailCrops.length, height: region.height, channels: 3, background: '#fffdf7' } })
        .composite(tailCrops.map((input, i) => ({ input, left: i * region.width, top: 0 })))
        .png().toFile(`${output}/tail-frames.png`);
    await sharp({ create: { width: width * 3, height, channels: 3, background: '#fffdf7' } })
        .composite([still, patch, mesh].map((input, i) => ({ input, left: i * width, top: 0 })))
        .png().toFile(`${output}/compare.png`);

    await button(page, '复位').click();
    await face(page, 'calm');
    const figure = await page.locator('.pet').boundingBox();
    const atFraction = (x, y) => page.mouse.move(figure.x + x * figure.width, figure.y + y * figure.height, { steps: 8 });
    await atFraction(.38, .33);
    await page.mouse.down();
    for (let i = 0; i < 3; i++) {
        await page.waitForTimeout(105);
        await atFraction(i % 2 ? .36 : .46, .33);
    }
    await face(page, 'soft');
    await atFraction(.44, .61);
    for (let i = 0; i < 4; i++) {
        await page.waitForTimeout(105);
        await atFraction(i % 2 ? .43 : .58, .61);
    }
    await face(page, 'happy');
    await page.mouse.up();
    await face(page, 'soft');
    await button(page, '复位').click();
    await face(page, 'calm');
    const cancelled = await startRub(page);
    for (let i = 0; i < 3; i++) await cancelled();
    await face(page, 'soft');
    await page.locator('.pet-head').dispatchEvent('pointercancel', { pointerId: 1 });
    for (let i = 0; i < 5; i++) await cancelled();
    await face(page, 'soft');
    await page.mouse.up();
    await button(page, '摸摸').focus();
    await page.keyboard.press('Enter');
    await face(page, 'soft');
    await face(page, 'happy');
    await face(page, 'soft');
    await face(page, 'calm');
    await expect(main).toHaveAttribute('data-mode', 'mesh');
    await page.close();

    const mobile = await browser.newPage({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    await open(mobile);
    const cdp = await mobile.context().newCDPSession(mobile);
    const body = await mobile.locator('.pet-body').boundingBox();
    const at = ratio => ({ x: body.x + body.width * ratio, y: body.y + body.height / 2 });
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [at(.25)] });
    await face(mobile, 'calm');
    for (let i = 1; i <= 7; i++) {
        await mobile.waitForTimeout(110);
        await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [at(i % 2 ? .62 : .32)] });
        if (i === 3) await face(mobile, 'soft');
    }
    await face(mobile, 'happy');
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await face(mobile, 'soft');
    await face(mobile, 'calm');
    await mobile.close();
} finally { await browser.close(); }

const fallback = await launch(['--disable-webgl']);
try {
    const page = await fallback.newPage({ reducedMotion: 'reduce' });
    await open(page);
    await expect(button(page, '表情＋微动')).toBeDisabled();
    await expect(button(page, '换表情')).toHaveAttribute('aria-pressed', 'true');
    await button(page, '摸摸').click();
    await face(page, 'happy');
    await expect(page.locator('.flower-burst')).toHaveCount(1);
} finally { await fallback.close(); }

assert.deepEqual(errors, []);
assert.deepEqual(invalidResponses, []);
await writeFile(`${output}/report.json`, JSON.stringify({ url, checkpoints, errors, invalidResponses }, null, 2));
console.log('Desktop and mobile layout, head and body rubs, expression and tail pixels, release and WebGL fallback passed.');
