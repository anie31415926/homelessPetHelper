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
const errors = []; const failed = [];
function watch(page) {
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
}
const shot = (page, name) => page.screenshot({ path: `test-results/outing-${name}.png`, fullPage: true });
const stage = (page, value) => page.waitForFunction(value => document.querySelector('.outing-shell')?.dataset.stage === value, value);
const state = page => page.locator('.encounter-scene').evaluate(svg => ({ ...svg.dataset }));
async function clickCell(page, col, row, touch = false) {
  const point = await page.locator('.lab-scene').evaluate((svg, point) => {
    const transformed = new DOMPoint(point.x, point.y).matrixTransform(svg.getScreenCTM());
    return { x: transformed.x, y: transformed.y };
  }, center(col, row));
  if (touch) await page.touchscreen.tap(point.x, point.y);
  else await page.mouse.click(point.x, point.y);
}
const tool = (page, name) => page.locator('.approach-tools').getByRole('button', { name, exact: true });
async function action(page, id, touch = false) {
  const labels = { food: '放下食物', wait: /退开等待|蹲下等待/, toy: '轻晃玩具', approach: '慢慢靠近', invite: '邀请进箱' };
  const button = page.getByRole('button', { name: labels[id], exact: typeof labels[id] === 'string' });
  if (touch) await button.tap(); else await button.click();
  await page.waitForFunction(() => document.querySelector('.encounter-scene').dataset.phase === 'react');
  assert.equal(await page.locator('.turn-actions button:not(:disabled)').count(), 0, 'input locked while reaction resolves');
  await page.waitForFunction(() => document.querySelector('.outing-shell').dataset.stage === 'success' || document.querySelector('.encounter-scene').dataset.phase === 'choose');
}
async function geometry(page) {
  const result = await page.evaluate(() => {
    const outside = [...document.querySelectorAll('.outing-controls button,.lab-header button,.outing-steps span')].filter(element => {
      const r = element.getBoundingClientRect();
      return r.width && (r.left < -1 || r.right > innerWidth + 1 || r.bottom > innerHeight + 1);
    }).map(element => element.textContent || element.getAttribute('aria-label'));
    const clipped = [...document.querySelectorAll('button,h1,h2,.outing-narrative')].filter(element => element.clientWidth && (element.scrollWidth > element.clientWidth + 2 || element.scrollHeight > element.clientHeight + 2)).map(element => element.textContent);
    return { outside, clipped, overflow: document.documentElement.scrollWidth > innerWidth };
  });
  assert.deepEqual(result.outside, [], 'controls within viewport');
  assert.deepEqual(result.clipped, [], 'text fits its container');
  assert.equal(result.overflow, false);
}
async function pixels(page) {
  return page.locator('.encounter-scene').evaluate(async svg => {
    const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(svg)], { type: 'image/svg+xml' }));
    try {
      const image = new Image(); image.src = url; await image.decode();
      const canvas = document.createElement('canvas'); canvas.width = 195; canvas.height = 250;
      const ctx = canvas.getContext('2d'); ctx.drawImage(image, 0, 0, 195, 250);
      const data = ctx.getImageData(0, 0, 195, 250).data;
      const colors = new Set(); let dark = 0;
      for (let i = 0; i < data.length; i += 4) {
        colors.add(`${data[i] >> 3},${data[i + 1] >> 3},${data[i + 2] >> 3}`);
        if (data[i] < 180 && data[i + 1] < 180 && data[i + 2] < 180) dark++;
      }
      return { colors: colors.size, dark };
    } finally { URL.revokeObjectURL(url); }
  });
}

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1024 }, reducedMotion: 'reduce' });
  const page = await context.newPage(); watch(page);
  await page.goto(`${base}/outing.html?seed=7&route=puzzle&profile=hungry`, { waitUntil: 'networkidle' });
  await shot(page, 'puzzle-start');
  assert.equal(await page.getByRole('button', { name: '走近它', exact: true }).isDisabled(), true);
  await page.getByRole('button', { name: '搬动木箱 A', exact: true }).click();
  await page.getByRole('button', { name: '木箱向上', exact: true }).click();
  await page.getByRole('button', { name: '木箱向左', exact: true }).click();
  await tool(page, '木板').click(); await clickCell(page, 1, 4);
  await page.getByRole('button', { name: '走近它', exact: true }).waitFor({ state: 'visible' });
  await page.waitForFunction(() => ![...document.querySelectorAll('button')].find(button => button.textContent.includes('走近它')).disabled);
  await page.getByRole('button', { name: '撤销一步', exact: true }).click();
  await page.waitForFunction(() => [...document.querySelectorAll('button')].find(button => button.textContent.includes('走近它')).disabled);
  await clickCell(page, 1, 4);
  await page.getByRole('button', { name: '走近它', exact: true }).click();
  await stage(page, 'negotiate');
  assert.equal(await page.getByRole('button', { name: '放下食物', exact: true }).evaluate(button => button === document.activeElement), true, 'phase transition focuses action controls');
  await shot(page, 'talk-desktop');
  const initialPixels = await pixels(page);
  assert.ok(initialPixels.colors > 100 && initialPixels.dark > 400);
  await action(page, 'approach');
  assert.equal((await state(page)).alert, '86');
  assert.match(await page.getByRole('status').textContent(), /靠近得有些早/);
  await page.getByRole('button', { name: '暂停', exact: true }).click();
  const frozen = await state(page);
  const time = await page.locator('.outing-mission strong').textContent();
  await page.waitForTimeout(450);
  assert.deepEqual(await state(page), frozen);
  assert.equal(await page.locator('.outing-mission strong').textContent(), time);
  await page.keyboard.press('Escape');
  for (const id of ['wait', 'food', 'wait', 'toy', 'wait', 'approach', 'approach', 'invite']) await action(page, id);
  await stage(page, 'success');
  await shot(page, 'success');
  await page.getByRole('button', { name: '带它回站', exact: true }).click();
  await stage(page, 'station');
  await page.getByRole('button', { name: '轻轻抚摸', exact: true }).last().click();
  await page.getByRole('status').filter({ hasText: '蹭着' }).waitFor();
  await shot(page, 'station');
  assert.equal(await page.evaluate(() => localStorage.getItem('fluffy-days-v1')), null);
  await page.getByRole('button', { name: '试玩设置' }).click();
  await page.getByRole('button', { name: '清理开路', exact: true }).click();
  await page.getByRole('button', { name: '很谨慎', exact: true }).click();
  await page.getByRole('button', { name: '开始新的外出', exact: true }).click();
  await stage(page, 'approach');
  assert.equal(await page.locator('.outing-shell').getAttribute('data-route'), 'cleanup');
  await shot(page, 'cleanup-start');
  for (let i = 0; i < 40 && await page.getByRole('button', { name: '走近它', exact: true }).isDisabled(); i++) {
    const item = page.locator('[data-junk][data-accessible="true"]').first();
    assert.ok(await item.count());
    const type = await item.getAttribute('data-type');
    await tool(page, { litter: '捡拾', weeds: '修剪', mud: '冲洗' }[type]).click();
    await item.press('Enter');
    await page.waitForTimeout(50);
  }
  await page.getByRole('button', { name: '走近它', exact: true }).click();
  await stage(page, 'negotiate');
  assert.equal((await state(page)).alert, '82');
  for (const id of ['wait', 'toy', 'wait', 'approach', 'wait', 'approach', 'invite']) await action(page, id);
  await stage(page, 'success');

  const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const phone = await mobile.newPage(); watch(phone);
  await phone.goto(`${base}/outing.html?stage=talk&profile=curious&seed=15`, { waitUntil: 'networkidle' });
  await action(phone, 'toy', true);
  for (const [width, height] of [[320, 640], [390, 844], [430, 932], [768, 1024], [1440, 1024]]) {
    await phone.setViewportSize({ width, height }); await geometry(phone); await shot(phone, `talk-${width}`);
  }
  await phone.setViewportSize({ width: 390, height: 844 });
  await phone.getByRole('button', { name: '试玩设置' }).tap();
  await phone.keyboard.press('Shift+Tab');
  assert.equal(await phone.getByRole('button', { name: '重玩同一次相遇', exact: true }).evaluate(button => button === document.activeElement), true, 'settings focus trap');
  await phone.getByRole('button', { name: '很谨慎', exact: true }).tap();
  await phone.getByLabel('直接体验回合交涉').check();
  await phone.getByRole('button', { name: '开始新的外出', exact: true }).tap();
  await stage(phone, 'negotiate');
  for (const id of ['wait', 'toy', 'wait', 'approach', 'wait', 'approach', 'invite']) await action(phone, id, true);
  await stage(phone, 'success');
  await phone.getByRole('button', { name: '带它回站', exact: true }).tap();
  await shot(phone, 'mobile-station');
  for (const route of ['puzzle', 'cleanup']) {
    await phone.goto(`${base}/outing.html?route=${route}&seed=5`, { waitUntil: 'networkidle' });
    for (const [width, height] of [[320, 640], [390, 844], [1440, 1024]]) {
      await phone.setViewportSize({ width, height }); await geometry(phone); await shot(phone, `${route}-${width}`);
    }
  }
  assert.deepEqual(errors, [], 'no browser errors'); assert.deepEqual(failed, [], 'no failed assets');
  console.log('PASS: both approach branches -> distinct negotiation profiles -> voluntary rescue -> station, undo readiness, reaction locking, clear feedback, pause, settings, direct mode, keyboard focus, touch input, five viewport sizes, nonblank pixels and untouched legacy saves.');
  console.log('Encounter pixels:', JSON.stringify(initialPixels));
} finally { await browser.close(); }
