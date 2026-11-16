import { chromium } from '@playwright/test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { center } from '../src/playlab/engine.js';

const base = process.env.RESCUE_BASE_URL || 'http://localhost:5173';
await fs.mkdir('test-results', { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
  if (!error.message.includes("Executable doesn't exist")) throw error;
  browser = await chromium.launch({ channel: 'msedge', headless: true });
}
const errors = [];
const failed = [];
const watch = page => {
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
};
const shot = (page, name) => page.screenshot({ path: `test-results/lab-${name}.png`, fullPage: true });
const read = page => page.locator('.lab-scene').evaluate(svg => ({ ...svg.dataset }));
const tool = (page, name) => page.locator('.lab-controls').getByRole('button', { name: new RegExp(name) });
async function screenPoint(page, col, row) {
  return page.locator('.lab-scene').evaluate((svg, point) => {
    const transformed = new DOMPoint(point.x, point.y).matrixTransform(svg.getScreenCTM());
    return { x: transformed.x, y: transformed.y };
  }, center(col, row));
}
async function cell(page, col, row, touch = false) {
  const point = await screenPoint(page, col, row);
  if (touch) await page.touchscreen.tap(point.x, point.y);
  else await page.mouse.click(point.x, point.y);
}
async function done(page) {
  await page.waitForFunction(() => document.querySelector('.lab-scene')?.dataset.done === 'true', undefined, { timeout: 25000 });
  await page.getByRole('dialog').waitFor();
}
async function checkLayout(page) {
  const result = await page.evaluate(() => {
    const outside = [...document.querySelectorAll('.lab-header button,.mode-tabs button,.lab-controls button')].filter(element => {
      const rect = element.getBoundingClientRect();
      return rect.width && (rect.left < -1 || rect.right > innerWidth + 1 || rect.bottom > innerHeight + 1);
    }).map(element => element.getAttribute('aria-label') || element.textContent);
    const clipped = [...document.querySelectorAll('button,h1,h2,.lab-status')].filter(element => element.clientWidth > 0 && element.scrollWidth > element.clientWidth + 2).map(element => element.textContent);
    return { overflow: document.documentElement.scrollWidth > innerWidth, outside, clipped };
  });
  assert.equal(result.overflow, false);
  assert.deepEqual(result.outside, [], 'controls stay visible');
  assert.deepEqual(result.clipped, [], 'no clipped text');
}
async function scenePixels(page) {
  return page.locator('.lab-scene').evaluate(async svg => {
    const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(svg)], { type: 'image/svg+xml' }));
    try {
      const image = new Image(); image.src = url; await image.decode();
      const canvas = document.createElement('canvas'); canvas.width = 195; canvas.height = 270;
      const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0, 195, 270);
      const pixels = ctx.getImageData(0, 0, 195, 270).data;
      const colors = new Set(); let opaque = 0; let dark = 0;
      for (let i = 0; i < pixels.length; i += 4) {
        colors.add(`${pixels[i] >> 3},${pixels[i + 1] >> 3},${pixels[i + 2] >> 3}`);
        if (pixels[i + 3] > 250) opaque++;
        if (pixels[i] < 180 && pixels[i + 1] < 180 && pixels[i + 2] < 180) dark++;
      }
      return { colors: colors.size, opaque, dark };
    } finally { URL.revokeObjectURL(url); }
  });
}

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1024 }, reducedMotion: 'reduce' });
  const page = await context.newPage(); watch(page);
  await page.goto(`${base}/playlab.html`, { waitUntil: 'networkidle' });
  const pixels = {};
  pixels.puzzle = await scenePixels(page);
  await shot(page, 'puzzle-desktop');
  await page.getByRole('button', { name: '搬动木箱 A', exact: true }).click();
  await page.getByRole('button', { name: '木箱向下', exact: true }).click();
  assert.equal(await page.locator('[data-box="a"]').getAttribute('data-row'), '3');
  await page.getByRole('button', { name: '撤销一步', exact: true }).click();
  assert.equal(await page.locator('[data-box="a"]').getAttribute('data-row'), '2');
  // Drag a real box with pointer capture, then use its accessible arrow control.
  const a = await screenPoint(page, 3, 2);
  const b = await screenPoint(page, 3, 3);
  await page.mouse.move(a.x, a.y); await page.mouse.down(); await page.mouse.move(b.x, b.y, { steps: 8 }); await page.mouse.up();
  assert.equal(await page.locator('[data-box="a"]').getAttribute('data-row'), '3');
  await page.getByRole('button', { name: '木箱向右', exact: true }).click();
  await tool(page, '食物').click(); await cell(page, 3, 7);
  await page.getByRole('status').filter({ hasText: '路还不通' }).waitFor();
  await tool(page, '木板').click(); await cell(page, 1, 4);
  await page.waitForTimeout(450);
  await shot(page, 'puzzle-cat-walking');
  await page.getByRole('button', { name: '暂停', exact: true }).click();
  const frozen = await read(page);
  await page.waitForTimeout(400);
  assert.deepEqual(await read(page), frozen, 'pause freezes movement and timer');
  await page.keyboard.press('Shift+Tab');
  assert.equal(await page.getByRole('button', { name: '重新开始这一关', exact: true }).evaluate(element => element === document.activeElement), true);
  await page.keyboard.press('Escape');
  await done(page);
  await shot(page, 'puzzle-success');
  await page.getByRole('button', { name: '想再玩', exact: true }).click();
  await page.getByRole('button', { name: '试试下一种', exact: true }).click();

  assert.equal((await read(page)).mode, 'cleanup');
  pixels.cleanup = await scenePixels(page);
  await shot(page, 'cleanup-before');
  const far = page.locator('[data-junk][data-accessible="false"]').first();
  const farId = await far.getAttribute('data-junk');
  const farHp = await far.getAttribute('data-hp');
  await far.press('Enter');
  assert.equal(await page.locator(`[data-junk="${farId}"]`).getAttribute('data-hp'), farHp);
  const weed = page.locator('[data-junk][data-accessible="true"][data-type="weeds"]').first();
  const weedId = await weed.getAttribute('data-junk');
  await tool(page, '修剪').click();
  const weedBox = await weed.boundingBox();
  await page.mouse.move(weedBox.x + weedBox.width / 2, weedBox.y + weedBox.height / 2);
  await page.mouse.down(); await page.waitForTimeout(250);
  const partial = await page.locator(`[data-junk="${weedId}"]`).getAttribute('data-hp');
  assert.ok(Number(partial) > 0 && Number(partial) < .65, 'continuous tool gives partial progress');
  await shot(page, 'cleanup-working');
  await page.waitForTimeout(600); await page.mouse.up();
  assert.equal(await page.locator(`[data-junk="${weedId}"]`).count(), 0);
  const toolNames = { litter: '捡拾', weeds: '修剪', mud: '冲洗' };
  for (let attempt = 0; attempt < 50 && await page.locator('[data-junk]').count(); attempt++) {
    const item = page.locator('[data-junk][data-accessible="true"]').first();
    assert.ok(await item.count(), 'there must be an accessible next item');
    const type = await item.getAttribute('data-type');
    await tool(page, toolNames[type]).click();
    await item.press('Enter');
    await page.waitForTimeout(40);
  }
  assert.equal(await page.locator('[data-junk]').count(), 0);
  await shot(page, 'cleanup-after');
  await page.getByRole('button', { name: '接回动物', exact: true }).click();
  await page.getByRole('button', { name: '接回动物', exact: true }).click();
  await done(page);
  assert.equal((await read(page)).safe, '2');
  await shot(page, 'cleanup-success');
  await page.getByRole('button', { name: '还行', exact: true }).click();
  await page.getByRole('button', { name: '试试下一种', exact: true }).click();

  assert.equal((await read(page)).mode, 'guide');
  pixels.guide = await scenePixels(page);
  await shot(page, 'guide-desktop');
  await tool(page, '玩具').click();
  await cell(page, 5, 2);
  const target = center(5, 2);
  await page.waitForFunction(target => {
    const s = document.querySelector('.lab-scene').dataset;
    return Math.hypot(Number(s.playerX) - target.x, Number(s.playerY) - target.y) < 2 && Number(s.trust) > .55;
  }, target, { timeout: 16000 });
  await shot(page, 'guide-following');
  await cell(page, 3, 7);
  await done(page);
  assert.equal((await read(page)).attention, '安心进箱');
  await shot(page, 'guide-success');
  await page.getByRole('button', { name: '想再玩', exact: true }).click();
  await page.getByRole('button', { name: '关闭弹窗', exact: true }).click();
  assert.match(await page.locator('.lab-footer').textContent(), /3 \/ 3/);
  const preferences = await page.evaluate(() => JSON.parse(localStorage.getItem('fluffy-rescue-lab-v1')));
  assert.equal(preferences.puzzle.vote, 3);
  assert.equal(preferences.cleanup.vote, 2);
  assert.equal(preferences.guide.vote, 3);
  assert.equal(await page.evaluate(() => localStorage.getItem('fluffy-days-v1')), null, 'legacy save untouched');
  await page.getByRole('button', { name: '重新开始当前玩法', exact: true }).click();
  assert.match(await page.locator('.lab-footer').textContent(), /3 \/ 3/, 'replay does not erase experienced count');

  const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const phone = await mobile.newPage(); watch(phone);
  for (const mode of ['puzzle', 'cleanup', 'guide']) {
    await phone.goto(`${base}/playlab.html?mode=${mode}`, { waitUntil: 'networkidle' });
    for (const [width, height] of [[320, 640], [390, 844], [768, 1024], [1440, 1024]]) {
      await phone.setViewportSize({ width, height });
      await checkLayout(phone);
      await shot(phone, `${mode}-${width}`);
    }
  }
  await phone.setViewportSize({ width: 390, height: 844 });
  const before = await read(phone);
  await phone.getByRole('button', { name: '向上移动', exact: true }).tap();
  await phone.waitForTimeout(200);
  const moved = await read(phone);
  assert.ok(Number(moved.playerY) < Number(before.playerY), 'touch dpad moves');
  await phone.waitForTimeout(200);
  assert.equal((await read(phone)).playerY, moved.playerY, 'touch release stops movement');
  await tool(phone, '玩具').tap();
  await cell(phone, 5, 6, true);
  await phone.waitForFunction(() => Number(document.querySelector('.lab-scene').dataset.playerX) > 230);
  await phone.getByRole('button', { name: /场景解谜/ }).tap();
  await phone.getByRole('button', { name: '搬动木箱 A', exact: true }).tap();
  await phone.getByRole('button', { name: '木箱向下', exact: true }).tap();
  assert.equal(await phone.locator('[data-box="a"]').getAttribute('data-row'), '3');
  await phone.getByRole('button', { name: /引导回家/ }).tap();
  assert.ok(Number((await read(phone)).playerX) > 230, 'tabs preserve current experiment state');
  for (const [mode, values] of Object.entries(pixels)) assert.ok(values.colors > 90 && values.dark > 100 && values.opaque > 50000, `${mode} nonblank pixels: ${JSON.stringify(values)}`);
  assert.deepEqual(errors, [], 'no browser errors');
  assert.deepEqual(failed, [], 'no failed resources');
  console.log('PASS: all three levels completed through real UI, puzzle drag/undo/route, continuous cleanup/frontier/tools/two animals, guide pathfinding/follow/home, pause, feedback persistence, replay, tab isolation, touch controls, four viewport sizes per mode, reduced motion and nonblank rendered scenes.');
  console.log('Pixel checks:', JSON.stringify(pixels));
} finally { await browser.close(); }
