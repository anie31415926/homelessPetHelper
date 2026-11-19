import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { chromium } from '@playwright/test';
import { createDuel, animal } from '../src/duel/engine.js';

const base = process.env.DUEL_BASE_URL || 'http://localhost:5190';
const output = 'test-results/duel';
await fs.mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
    if (!error.message.includes("Executable doesn't exist")) throw error;
    browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const errors = [], failed = [], measurements = [], checks = [];
const context = await browser.newContext({ hasTouch: true, isMobile: true, reducedMotion: 'reduce' });
const page = await context.newPage();
page.on('pageerror', error => errors.push(error.message));
page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
const main = page.locator('main');
const round = async () => Number(await main.getAttribute('data-round'));
const settle = () => page.waitForFunction(() => document.querySelector('.duel-board')?.getAttribute('aria-busy') === 'false');
async function geometry(width, height, mode = 'chain') {
    await page.setViewportSize({ width, height });
    await page.goto(`${base}/duel.html?seed=20261119&mode=${mode}`, { waitUntil: 'networkidle' });
    const measurement = await page.evaluate(async () => {
        for (const image of document.images) await image.decode();
        const rect = el => { const r = el.getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height, bottom: r.bottom, right: r.right }; };
        const clipped = [...document.querySelectorAll('button,strong,h1,p')].filter(el =>
            !el.classList.contains('response-text') && el.clientWidth &&
            (el.scrollWidth > el.clientWidth + 2 || el.scrollHeight > el.clientHeight + 2)).map(el => el.textContent);
        const tiles = [...document.querySelectorAll('.duel-tile')].map(rect);
        const intersections = tiles.flatMap((a, i) => tiles.slice(i + 1).filter(b =>
            a.x < b.right - 1 && a.right > b.x + 1 && a.y < b.bottom - 1 && a.bottom > b.y + 1));
        return { clipped, overflow: document.documentElement.scrollWidth > innerWidth,
            scene: rect(document.querySelector('.duel-scene')), board: rect(document.querySelector('.duel-board')),
            actions: rect(document.querySelector('.duel-actions')), intersections: intersections.length,
            images: [...document.images].map(el => ({ width: el.naturalWidth, height: el.naturalHeight })),
            shell: rect(document.querySelector('main')), tiles,
            scroll: document.querySelector('main').scrollHeight > document.querySelector('main').clientHeight + 1 };
    });
    assert.deepEqual(measurement.clipped, [], `${width}x${height} text fits`);
    assert.equal(measurement.overflow, false);
    assert.equal(measurement.intersections, 0);
    assert.ok(measurement.scene.height >= 60);
    assert.ok(measurement.images.every(el => el.width > 0 && el.height > 0));
    assert.equal(measurement.tiles.length, 20);
    assert.ok(measurement.actions.y >= measurement.board.bottom);
    assert.ok(measurement.actions.bottom <= measurement.shell.bottom, 'actions stay inside shell');
    assert.equal(measurement.scroll, false, 'content fits, not only outer shell');
    if (height >= 568) assert.ok(measurement.shell.height <= height + 1, 'normal viewport fits without scrolling');
    const landscape = width >= 600 && height <= 559;
    assert.equal(measurement.shell.width, Math.min(width, landscape ? 667 : 375), 'phone preview width is capped');
    assert.equal(measurement.shell.height, Math.min(height, landscape ? 375 : 667), 'phone preview height is capped');
    assert.ok(Math.abs(measurement.shell.x - (width - measurement.shell.width) / 2) < 1, 'preview is horizontally centered');
    assert.ok(Math.abs(measurement.shell.y - (height - measurement.shell.height) / 2) < 1, 'preview is vertically centered');
    measurements.push({ width, height, mode, ...measurement });
    await page.screenshot({ path: `${output}/${mode}-${width}x${height}.png`, fullPage: true });
}
async function hold(locator, duration = 470) {
    const box = await locator.boundingBox();
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.down(); await page.waitForTimeout(duration); await page.mouse.up();
}
async function switchMode(name) {
    await page.getByRole('button', { name: /切换玩法，当前/ }).click();
    await page.getByRole('button', { name: new RegExp(`^${name}`) }).click();
    await page.getByRole('button', { name: '换个玩法重开' }).click();
}
async function safeLayout(width, height, insets) {
    await geometry(width, height, 'change');
    await main.evaluate((el, values) => {
        for (const [side, value] of Object.entries(values)) el.style.setProperty(`--safe-${side}`, `${value}px`);
    }, insets);
    const actual = await page.evaluate(() => {
        const shell = document.querySelector('main');
        const bounds = shell.getBoundingClientRect();
        const css = getComputedStyle(shell);
        const top = parseFloat(css.paddingTop), bottom = parseFloat(css.paddingBottom);
        const left = parseFloat(css.paddingLeft), right = parseFloat(css.paddingRight);
        const clipped = [...document.querySelectorAll('.duel-topbar button,.duel-actions button,.duel-tile')]
            .filter(el => el.getClientRects().length && getComputedStyle(el).visibility !== 'hidden').filter(el => {
                const r = el.getBoundingClientRect();
                return r.top < bounds.top + top - 1 || r.bottom > bounds.bottom - bottom + 1 ||
                    r.left < bounds.left + left - 1 || r.right > bounds.right - right + 1;
            }).map(el => el.getAttribute('aria-label') || el.textContent);
        return { clipped, scroll: shell.scrollHeight > shell.clientHeight + 1 };
    });
    assert.deepEqual(actual.clipped, []);
    assert.equal(actual.scroll, false, `${width}x${height} with insets still fits`);
    await page.screenshot({ path: `${output}/insets-${width}x${height}.png`, fullPage: true });
}
try {
    for (const [w, h] of [[320, 568], [360, 500], [360, 640], [375, 667], [390, 670], [390, 844], [430, 932], [768, 1024], [1440, 900]]) await geometry(w, h);
    await geometry(320, 568, 'change');
    await page.getByRole('button', { name: /现场行动.*已保留/ }).click();
    const compactRound = await round();
    await hold(page.locator('dialog .card-action').first()); await settle();
    assert.equal(await round(), compactRound + 1, 'short viewport hand sheet submits real move');
    assert.equal(await page.getByRole('dialog').count(), 0);
    checks.push('320x568 hand sheet is playable without scrolling');
    await safeLayout(320, 568, { top: 47, bottom: 34 });
    await safeLayout(375, 667, { top: 24, bottom: 20 });
    await safeLayout(844, 390, { left: 47, right: 47, bottom: 21 });
    await page.getByRole('button', { name: '查看规则', exact: true }).click();
    await page.locator('.modal-body').evaluate(el => { el.scrollTop = el.scrollHeight; });
    const closeBounds = await page.getByRole('button', { name: '关闭', exact: true }).boundingBox();
    assert.ok(closeBounds.y >= 0 && closeBounds.y + closeBounds.height <= 390);
    await page.getByRole('button', { name: '关闭', exact: true }).click();
    checks.push('small portrait and landscape safe areas; long dialog scroll keeps close visible');
    await geometry(1440, 900);
    await page.getByRole('button', { name: '查看规则', exact: true }).click();
    const modalInPhone = await page.getByRole('dialog').evaluate(el => {
        const dialog = el.getBoundingClientRect();
        const shell = document.querySelector('main').getBoundingClientRect();
        return dialog.left >= shell.left && dialog.right <= shell.right &&
            dialog.top >= shell.top && dialog.bottom <= shell.bottom;
    });
    assert.equal(modalInPhone, true, 'desktop dialog stays inside the phone preview');
    await page.screenshot({ path: `${output}/phone-preview-desktop-dialog.png`, fullPage: true });
    await page.getByRole('button', { name: '关闭', exact: true }).click();
    checks.push('375x667 centered desktop preview, responsive smaller viewports and bounded dialog');
    await geometry(390, 740, 'change');
    await geometry(390, 780, 'change');
    await geometry(390, 844, 'change');
    await page.getByRole('button', { name: '查看现场预告' }).click();
    assert.ok(await page.getByRole('dialog').getByText('接下来的现场').isVisible());
    await page.getByRole('button', { name: '关闭', exact: true }).click();
    checks.push('forecast details available by touch, not hover only');
    await page.locator('main').evaluate(el => { el.style.setProperty('--safe-top', '47px'); el.style.setProperty('--safe-bottom', '34px'); });
    const safe = await page.evaluate(() => ({ top: document.querySelector('.duel-topbar').getBoundingClientRect().top,
        bottom: document.querySelector('.duel-actions').getBoundingClientRect().bottom, height: innerHeight }));
    assert.ok(safe.top >= 47);
    assert.ok(safe.bottom <= safe.height - 34);
    await page.screenshot({ path: `${output}/safe-area-390x844.png`, fullPage: true });
    checks.push('simulated top 47 / bottom 34 safe area');
    let catSeed = 1;
    while (animal(createDuel({ seed: catSeed })).kind !== 'cat') catSeed++;
    await page.goto(`${base}/duel.html?seed=${catSeed}`, { waitUntil: 'networkidle' });
    assert.ok(await page.getByAltText('正在观察你的猫咪').isVisible());
    await page.screenshot({ path: `${output}/cat-390x844.png`, fullPage: true });
    await geometry(390, 844);
    const boardTypes = () => page.locator('.duel-tile').evaluateAll(els => els.map(el => el.dataset.type));
    const initial = await boardTypes();
    const action = page.locator('.card-action').first();
    const id = await action.locator('..').getAttribute('data-card');
    await action.click(); await page.waitForTimeout(500);
    assert.equal(await round(), 1, 'short tap cancels');
    await hold(action, 150); await page.waitForTimeout(350);
    assert.equal(await round(), 1, 'early release cancels');
    await page.locator('.pin-action').first().click();
    assert.equal(await round(), 1, 'pin does not use a turn');
    assert.equal(await page.locator('.pin-action').first().getAttribute('aria-pressed'), 'true');
    await page.setViewportSize({ width: 375, height: 567 });
    assert.equal(await round(), 1);
    await page.getByRole('button', { name: /现场行动.*已保留/ }).click();
    assert.equal(await page.locator('dialog .pin-action').first().getAttribute('aria-pressed'), 'true');
    await page.getByRole('button', { name: '关闭', exact: true }).click();
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await page.locator('.pin-action').first().getAttribute('aria-pressed'), 'true');
    checks.push('browser height changes keep round and retained card state');
    const card = page.locator(`[data-card="${id}"] .card-action`);
    await hold(card); await settle();
    assert.equal(await round(), 2, 'actual hold executes once');
    assert.ok(!await page.locator(`[data-card="${id}"] .pin-action[aria-pressed="true"]`).count(), 'used pin consumed');
    checks.push('short-tap/early-release cancel, independent pin, hold submits one action');

    await page.getByRole('button', { name: '暂停', exact: true }).click();
    const pausedRound = await round();
    await page.waitForTimeout(600);
    assert.equal(await round(), pausedRound);
    await page.getByRole('button', { name: '重玩本局', exact: true }).click();
    assert.equal(await round(), 1);
    assert.deepEqual(await boardTypes(), initial, 'seeded replay restores initial board');
    await page.getByRole('button', { name: '暂停', exact: true }).click();
    await page.locator('#duel-seed').fill('-1');
    await page.getByRole('button', { name: '重开指定编号' }).click();
    assert.ok(await page.getByRole('alert').isVisible());
    await page.getByRole('button', { name: '关闭', exact: true }).click();
    checks.push('pause has no progression, seeded replay, invalid seed rejected');

    const first = page.locator('[data-cell="0"]');
    await first.focus(); await first.press('ArrowRight');
    assert.equal(await page.evaluate(() => document.activeElement.dataset.cell), '1');
    const keyboardAction = page.locator('.card-action').first();
    await keyboardAction.focus(); await page.keyboard.down('Enter'); await page.waitForTimeout(470); await page.keyboard.up('Enter'); await settle();
    assert.equal(await round(), 2);
    checks.push('directional board focus and keyboard hold');

    await switchMode('现场变局');
    assert.equal(await main.getAttribute('data-mode'), 'change');
    assert.equal(await round(), 1);
    assert.ok(await page.locator('.event-forecast').isVisible());
    await switchMode('顺势连锁');
    checks.push('confirmed mode change restarts same seed, forecast displayed');

    // Play the real UI from the visible legal groups, without mutation or a debug win endpoint.
    for (let i = 0; i < 100 && await main.getAttribute('data-status') === 'playing'; i++) {
        await settle();
        const insight = page.locator('.focus-action');
        if (await insight.isEnabled()) { await insight.click(); continue; }
        const index = await page.evaluate(() => [...document.querySelectorAll('.duel-tile')]
            .filter(el => +el.dataset.size >= 2)
            .sort((a, b) => Number(b.dataset.relevant === 'true') * 30 + +b.dataset.size -
                (Number(a.dataset.relevant === 'true') * 30 + +a.dataset.size))[0].dataset.cell);
        await page.locator(`[data-cell="${index}"]`).click();
    }
    await settle();
    assert.equal(await main.getAttribute('data-status'), 'ready');
    assert.equal(await page.locator('.card-action').count(), 0, 'finish does not wait for a new hand');
    await page.getByRole('button', { name: '让它自己走进来' }).click();
    assert.equal(await main.getAttribute('data-status'), 'rescued');
    assert.ok(await page.getByRole('dialog').isVisible());
    await page.screenshot({ path: `${output}/success-390x844.png`, fullPage: true });
    await page.getByRole('button', { name: '同一局，试另一种走法' }).click();
    assert.equal(await round(), 1);
    assert.equal(await main.getAttribute('data-seed'), '20261119');
    checks.push('full actual encounter, explicit acceptance, result metrics, deterministic retry');

    for (const mode of ['read', 'change']) {
        await page.setViewportSize(mode === 'change' ? { width: 320, height: 568 } : { width: 375, height: 667 });
        await page.goto(`${base}/duel.html?seed=18&mode=${mode}`, { waitUntil: 'networkidle' });
        for (let i = 0; i < 100 && await main.getAttribute('data-status') === 'playing'; i++) {
            await settle();
            if (await page.locator('.focus-action').isEnabled()) { await page.locator('.focus-action').click(); continue; }
            const index = await page.evaluate(() => [...document.querySelectorAll('.duel-tile')]
                .filter(el => +el.dataset.size >= 2).sort((a, b) =>
                    Number(b.dataset.relevant === 'true') * 30 + +b.dataset.size -
                    (Number(a.dataset.relevant === 'true') * 30 + +a.dataset.size))[0].dataset.cell);
            await page.locator(`[data-cell="${index}"]`).click();
        }
        await settle();
        assert.equal(await main.getAttribute('data-status'), 'ready', `${mode} completes through real UI`);
        await page.getByRole('button', { name: '让它自己走进来' }).click();
        assert.equal(await main.getAttribute('data-status'), 'rescued');
        const dialogBounds = await page.getByRole('dialog').boundingBox();
        assert.ok(dialogBounds.y >= 0 && dialogBounds.y + dialogBounds.height <= page.viewportSize().height);
        await page.getByRole('button', { name: '同一局，试另一种走法' }).click();
    }
    checks.push('read and change modes also complete through real visible inputs');

    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`${base}/duel.html?seed=20261119&mode=chain`, { waitUntil: 'networkidle' });
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await page.locator('[data-cell][data-size="2"]').first().click();
    await page.waitForTimeout(110);
    assert.ok(await page.locator('.wave-cell').count() >= 2);
    await page.screenshot({ path: `${output}/actual-clear.png`, fullPage: true });
    await settle();
    checks.push('motion-on actual-wave overlay renders');
    assert.deepEqual(errors, []);
    assert.deepEqual(failed, []);
    console.log(JSON.stringify({ checks, viewports: measurements.length, errors, failed }, null, 2));
    await fs.writeFile(`${output}/report.json`, JSON.stringify({ checks, measurements, errors, failed }, null, 2));
} finally { await browser.close(); }
