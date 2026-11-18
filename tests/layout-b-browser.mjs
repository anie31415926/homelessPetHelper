import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';
import { HOLD_DURATION_MS } from '../src/layout-b/useHoldConfirm.js';

const base = process.env.LAYOUT_BASE_URL || 'http://localhost:5188';
const output = 'test-results/layout-b/compact-hold';
await fs.mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const errors = [];
const failed = [];
const measurements = [];
const checks = [];
const context = await browser.newContext({ hasTouch: true, isMobile: true, reducedMotion: 'reduce' });
const page = await context.newPage();
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
const row = id => page.locator(`[data-action="${id}"]`);
const action = id => row(id).locator('.select-action');
const pin = id => row(id).locator('.retain-action');
const count = async () => Number(await page.locator('main').getAttribute('data-confirmations'));
const center = async element => {
    const box = await element.boundingBox();
    return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
};
const settle = () => page.waitForTimeout(HOLD_DURATION_MS + 90);

async function geometry(width, height, label = 'left', suffix = '') {
    await page.setViewportSize({ width, height });
    const result = await page.evaluate(async () => {
        await Promise.all([...document.images].map(image => image.decode()));
        const rect = element => {
            const r = element.getBoundingClientRect();
            return { x: r.x, y: r.y, width: r.width, height: r.height, bottom: r.bottom, right: r.right };
        };
        const buttons = [...document.querySelectorAll('.scene-header button,.action-dock button')];
        const outside = buttons.filter(button => {
            const r = rect(button);
            return r.x < -1 || r.y < -1 || r.right > innerWidth + 1 || r.bottom > innerHeight + 1;
        }).map(button => button.getAttribute('aria-label') || button.textContent);
        const clipped = [...document.querySelectorAll('button,strong,h1,h2,p')].filter(element =>
            element.clientWidth && (element.scrollWidth > element.clientWidth + 2 || element.scrollHeight > element.clientHeight + 2))
            .map(element => element.textContent);
        const smallTargets = buttons.filter(button => rect(button).width < 44 || rect(button).height < 44)
            .map(button => button.getAttribute('aria-label') || button.textContent);
        const images = [...document.images].map(image => ({ source: image.className, width: image.naturalWidth, height: image.naturalHeight }));
        return { outside, clipped, smallTargets, images, overflow: document.documentElement.scrollWidth > innerWidth,
            safeBottom: parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--safe-bottom')) || 0,
            shell: rect(document.querySelector('.baseline-shell')), scene: rect(document.querySelector('.observation')),
            dock: rect(document.querySelector('.action-dock')), list: rect(document.querySelector('.action-list')),
            first: rect(document.querySelector('.action-row')), last: rect([...document.querySelectorAll('.action-row')].at(-1)),
            pin: rect(document.querySelector('.retain-action')), action: rect(document.querySelector('.select-action')) };
    });
    assert.deepEqual(result.outside, [], `${width}x${height}: controls stay in viewport`);
    assert.deepEqual(result.clipped, [], `${width}x${height}: text fits`);
    assert.deepEqual(result.smallTargets, [], `${width}x${height}: 44px touch targets`);
    assert.equal(result.overflow, false, 'no horizontal overflow');
    assert.ok(result.images.every(image => image.width > 0 && image.height > 0), 'images decoded');
    assert.ok(result.scene.height >= result.shell.height * .54, 'scene owns most of the screen');
    assert.ok(result.dock.height - result.safeBottom <= 216, 'controls remain compact, excluding system gesture inset');
    if (!suffix.includes('long-text')) assert.ok(result.first.height <= 48, 'single-line action bars');
    assert.ok(result.first.y >= result.shell.y + result.shell.height * .65, 'actions close to bottom');
    assert.ok(result.last.bottom <= result.shell.bottom - 10, 'bottom gesture clearance');
    assert.ok(Math.abs(result.list.x + result.list.width / 2 - result.shell.x - result.shell.width / 2) < 1, 'centered bars');
    assert.ok(label === 'right' ? result.pin.x > result.action.x + result.action.width / 2 : result.pin.x < result.action.x + result.action.width / 2, 'independent retention on thumb side');
    measurements.push({ width, height, label, ...result });
    await page.screenshot({ path: `${output}/${label}-${width}x${height}${suffix}.png`, fullPage: true });
}

try {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`${base}/layout-b.html`, { waitUntil: 'networkidle' });
    assert.equal(await page.locator('main').getAttribute('data-hand'), 'left');
    assert.equal(await page.locator('.execute-action').count(), 0, 'no separate execute button');
    assert.match(await page.locator('#hold-hint').textContent(), /按住选项/);
    await action('food').tap(); await settle();
    assert.equal(await count(), 0, 'short tap never confirms');
    checks.push('short tap cancels');

    const touch = await context.newCDPSession(page);
    const foodPoint = await center(action('food'));
    await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...foodPoint, id: 1 }] });
    await page.waitForFunction(() => document.querySelector('[data-action="food"]').dataset.holding === 'true');
    await page.waitForFunction(() => document.querySelector('main').dataset.confirmations === '1');
    await settle();
    assert.equal(await count(), 1, 'continued touch does not auto-repeat');
    await touch.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    assert.match(await page.locator('.animal-feedback').textContent(), /已执行：放下食物/);
    assert.equal(await pin('wait').getAttribute('aria-pressed'), 'true');
    assert.equal(await pin('food').getAttribute('aria-pressed'), 'false', 'confirmation never retains');
    checks.push('touch hold confirms once, keeps retention independent');

    await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...foodPoint, id: 1 }] });
    await touch.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: foodPoint.x + 28, y: foodPoint.y, id: 1 }] });
    await settle();
    await touch.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    assert.equal(await count(), 1, 'drag cancels');
    await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...foodPoint, id: 1 }] });
    await touch.send('Input.dispatchTouchEvent', { type: 'touchCancel', touchPoints: [] });
    await settle();
    assert.equal(await count(), 1, 'system pointer cancellation cancels');
    await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...foodPoint, id: 1 }] });
    await touch.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...foodPoint, id: 1 }, { x: foodPoint.x + 55, y: foodPoint.y - 55, id: 2 }] });
    await settle();
    await touch.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    assert.equal(await count(), 1, 'second finger cancels pending hold');
    checks.push('drag, system cancellation and second pointer cancel');

    await pin('food').tap(); await settle();
    assert.equal(await count(), 1, 'pin does not confirm');
    await pin('space').tap();
    assert.match(await page.locator('.animal-feedback').textContent(), /最多保留两个/);
    assert.match(await page.locator('#hold-hint').textContent(), /按住选项/, 'retention errors never replace the confirmation hint');
    assert.equal(await pin('space').getAttribute('aria-pressed'), 'false');
    await pin('food').tap();
    checks.push('retention/cap independent');

    await action('space').press('Enter'); await settle();
    assert.equal(await count(), 1, 'short keypress does not confirm');
    await action('space').focus();
    await page.keyboard.down('Space');
    await settle();
    assert.equal(await count(), 2, 'keyboard hold confirms');
    await page.keyboard.down('Space'); await settle();
    assert.equal(await count(), 2, 'key repeat does not repeat confirmation');
    await page.keyboard.up('Space'); await settle();
    assert.equal(await count(), 2, 'release does not confirm twice');
    checks.push('keyboard hold confirms once; short/repeated keys do not');

    const point = await center(action('wait'));
    await page.mouse.move(point.x, point.y); await page.mouse.down();
    await page.mouse.move(point.x, point.y - 90); await settle(); await page.mouse.up();
    assert.equal(await count(), 2, 'move off row cancels mouse hold');
    await page.mouse.move(point.x, point.y); await page.mouse.down();
    await page.evaluate(() => window.dispatchEvent(new Event('blur')));
    await settle(); await page.mouse.up();
    assert.equal(await count(), 2, 'window blur cancels pending hold');
    await page.mouse.down();
    await page.evaluate(() => window.dispatchEvent(new Event('scroll')));
    await settle(); await page.mouse.up();
    assert.equal(await count(), 2, 'scroll cancels');
    await page.mouse.down();
    await page.getByRole('button', { name: '暂停', exact: true }).focus();
    await page.keyboard.press('Enter');
    await settle(); await page.mouse.up();
    assert.equal(await count(), 2, 'pause cancels pending hold');
    assert.equal(await page.locator('dialog').evaluate(dialog => dialog.open), true);
    assert.equal(await action('food').isDisabled(), true, 'background disabled while paused');
    checks.push('off-row, focus loss, scroll and pause cancel');

    await page.getByRole('radio', { name: '左手' }).focus();
    await page.keyboard.press('ArrowRight');
    assert.equal(await page.getByRole('radio', { name: '右手' }).getAttribute('aria-checked'), 'true');
    await page.screenshot({ path: `${output}/pause-sheet.png`, fullPage: true });
    await page.getByRole('button', { name: '继续', exact: true }).tap();
    await geometry(390, 844, 'right');
    await page.getByRole('button', { name: '暂停', exact: true }).tap();
    await page.getByRole('radio', { name: '左手' }).tap();
    await page.keyboard.press('Escape');
    await page.waitForFunction(() => !document.querySelector('dialog').open);
    assert.equal(await page.getByRole('button', { name: '暂停', exact: true }).evaluate(button => button === document.activeElement), true);
    await page.getByRole('button', { name: '暂停', exact: true }).tap();
    await page.getByRole('button', { name: '重新开始', exact: true }).tap();
    assert.equal(await count(), 0);
    checks.push('handedness, keyboard mode switching, focus restore and reset');

    await action('space').evaluate(button => button.dispatchEvent(new MouseEvent('click', { bubbles: true, detail: 0 })));
    assert.equal(await count(), 1, 'assistive virtual activation remains available');
    checks.push('assistive virtual activation');
    await page.reload({ waitUntil: 'networkidle' });
    for (const [width, height] of [[320, 568], [360, 640], [390, 844], [430, 932], [768, 1024], [1440, 1000]]) {
        await geometry(width, height);
    }
    await page.setViewportSize({ width: 320, height: 568 });
    await action('space').locator('strong').evaluate(element => { element.textContent = '退开并留出足够的安全空间'; });
    await geometry(320, 568, 'left', '-long-text');
    await page.reload({ waitUntil: 'networkidle' });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.addStyleTag({ content: ':root { --safe-top: 36px; --safe-bottom: 34px; }' });
    await geometry(390, 844, 'left', '-safe');
    const safe = measurements.at(-1);
    assert.ok(safe.last.bottom <= safe.shell.bottom - 48, 'simulated gesture inset applied');
    const pixelCheck = await page.locator('.scene-backdrop').evaluate(image => {
        const canvas = document.createElement('canvas'); canvas.width = 64; canvas.height = 64;
        const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0, 64, 64);
        const data = ctx.getImageData(0, 0, 64, 64).data;
        const colors = new Set();
        for (let i = 0; i < data.length; i += 4) colors.add(`${data[i] >> 4},${data[i + 1] >> 4},${data[i + 2] >> 4}`);
        return colors.size;
    });
    assert.ok(pixelCheck > 20, 'backdrop contains rendered art');
    await page.reload({ waitUntil: 'networkidle' });
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    const progressPoint = await center(action('food'));
    await page.mouse.move(progressPoint.x, progressPoint.y); await page.mouse.down();
    await page.waitForTimeout(150);
    const progress = await page.locator('.hold-progress').evaluate(element => new DOMMatrix(getComputedStyle(element).transform).a);
    assert.ok(progress > 0 && progress < 1, 'hold progress visibly fills');
    await page.screenshot({ path: `${output}/holding.png`, fullPage: true });
    await page.mouse.up();
    assert.deepEqual(errors, [], 'no browser exceptions');
    assert.deepEqual(failed, [], 'no failed responses');
    await fs.writeFile(`${output}/report.json`, JSON.stringify({ status: 'passed', holdDurationMs: HOLD_DURATION_MS, checks, measurements,
        pixelColors: pixelCheck, errors, failed, limitations: 'Layout-only web prototype, not Unity gameplay or physical-device reach validation.' }, null, 2));
    console.log('Compact hold layout passed: hold/cancellation, no repeat, retention/cap, pause, keyboard/assistive input, six viewports, long text, safe area, images.');
} finally {
    await browser.close();
}
