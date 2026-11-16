import { chromium } from '@playwright/test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { mazePath } from '../src/rescue/game.js';

const base = process.env.RESCUE_BASE_URL || 'http://localhost:5173';
await fs.mkdir('test-results', { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) {
  if (!error.message.includes("Executable doesn't exist")) throw error;
  browser = await chromium.launch({ headless: true, channel: 'msedge' });
}
const errors = [];
const failed = [];
const watch = page => {
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
  page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
};
const data = page => page.locator('.game-scene').evaluate(svg => ({ ...svg.dataset }));
const phase = (page, value) => page.waitForFunction(value => document.querySelector('.game-scene')?.dataset.phase === value, value);
const screenshot = (page, name) => page.screenshot({ path: `test-results/rescue-${name}.png`, fullPage: true });

async function walkMaze(page, entrance = 'bottom', pointer = false) {
  const from = { col: 5, row: entrance === 'bottom' ? 10 : 0 };
  const path = mazePath(from, { col: 5, row: entrance === 'bottom' ? 0 : 10 });
  let current = from;
  const labels = { up: '向上移动', down: '向下移动', left: '向左移动', right: '向右移动' };
  const keys = { up: 'ArrowUp', down: 'ArrowDown', left: 'ArrowLeft', right: 'ArrowRight' };
  for (const [col, row] of path.slice(1)) {
    const direction = col > current.col ? 'right' : col < current.col ? 'left' : row > current.row ? 'down' : 'up';
    await page.waitForTimeout(160);
    if (pointer) await page.getByRole('button', { name: labels[direction], exact: true }).click();
    else await page.keyboard.press(keys[direction]);
    await page.waitForFunction(({ col, row }) => {
      const svg = document.querySelector('.game-scene');
      return Number(svg.dataset.col) === col && Number(svg.dataset.row) === row;
    }, { col, row });
    current = { col, row };
  }
  await phase(page, 'rescue');
}

async function checkGeometry(page) {
  const geometry = await page.evaluate(() => {
    const outside = [...document.querySelectorAll('.control-deck button,.rescue-header button,.journey span')].filter(element => {
      const box = element.getBoundingClientRect();
      return box.width && (box.left < -1 || box.right > innerWidth + 1 || box.bottom > innerHeight + 1);
    }).map(element => element.getAttribute('aria-label') || element.textContent);
    const clipped = [...document.querySelectorAll('button,h1,h2,.status-line')].filter(element => element.clientWidth && element.scrollWidth > element.clientWidth + 2).map(element => element.textContent);
    return { overflow: document.documentElement.scrollWidth > innerWidth, outside, clipped };
  });
  assert.equal(geometry.overflow, false, 'no horizontal overflow');
  assert.deepEqual(geometry.outside, [], 'controls fit viewport');
  assert.deepEqual(geometry.clipped, [], 'text fits controls');
}

async function scenePixels(page) {
  return page.locator('.game-scene').evaluate(async svg => {
    const serialized = new XMLSerializer().serializeToString(svg);
    const url = URL.createObjectURL(new Blob([serialized], { type: 'image/svg+xml' }));
    try {
      const image = new Image();
      image.src = url;
      await image.decode();
      const canvas = document.createElement('canvas');
      canvas.width = 195; canvas.height = 290;
      const context = canvas.getContext('2d');
      context.drawImage(image, 0, 0, 195, 290);
      const pixels = context.getImageData(0, 0, 195, 290).data;
      const colors = new Set();
      let dark = 0;
      let opaque = 0;
      for (let i = 0; i < pixels.length; i += 4) {
        colors.add(`${pixels[i] >> 3},${pixels[i + 1] >> 3},${pixels[i + 2] >> 3}`);
        if (pixels[i] < 180 && pixels[i + 1] < 180 && pixels[i + 2] < 180) dark++;
        if (pixels[i + 3] > 250) opaque++;
      }
      return { colors: colors.size, dark, opaque };
    } finally { URL.revokeObjectURL(url); }
  });
}

async function rescueAnimal(page, missFirst = true) {
  assert.equal(await page.getByRole('button', { name: '出网', exact: true }).isEnabled(), true);
  const before = await data(page);
  await page.waitForTimeout(400);
  const after = await data(page);
  assert.notEqual(before.angle, after.angle, 'aim moves');
  assert.notEqual(before.animalX, after.animalX, 'animal moves');
  if (missFirst) {
    await page.waitForFunction(() => Number(document.querySelector('.game-scene').dataset.angle) < -.96);
    await page.getByRole('button', { name: '出网', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.game-scene').dataset.net !== 'idle');
    assert.equal(await page.getByRole('button', { name: '向上移动' }).isDisabled(), true);
    await screenshot(page, 'empty-net');
    await page.waitForFunction(() => document.querySelector('.game-scene').dataset.net === 'idle');
    assert.equal((await data(page)).phase, 'rescue', 'miss is retryable');
  }
  // Move through actual controls; the test never injects or overrides game state.
  await page.getByRole('button', { name: '向上移动' }).focus();
  await page.keyboard.down('ArrowUp');
  await page.waitForFunction(() => Number(document.querySelector('.game-scene').dataset.y) <= 373);
  await page.keyboard.up('ArrowUp');
  await page.waitForFunction(() => Math.abs(Number(document.querySelector('.game-scene').dataset.angle)) < .025);
  await page.getByRole('button', { name: /投食，剩余 3 份/ }).click();
  await page.waitForFunction(() => Number(document.querySelector('.game-scene').dataset.eating) > 0);
  assert.equal(await page.getByRole('button', { name: /投食，剩余 2 份/ }).isDisabled(), true, 'one active food at a time');
  await screenshot(page, 'food-and-aim');
  await page.waitForFunction(() => {
    const s = document.querySelector('.game-scene').dataset;
    const desired = Math.atan2(Number(s.animalX) - Number(s.x), Number(s.y) - 20 - Number(s.animalY));
    return Math.abs(Number(s.angle) - desired) < .05;
  }, undefined, { timeout: 8000 });
  await page.getByRole('button', { name: '出网', exact: true }).click();
  await phase(page, 'success');
  await page.getByRole('dialog', { name: '接住你啦。' }).waitFor();
  await screenshot(page, 'success');
  await page.getByRole('button', { name: '带它回站', exact: true }).click();
  await phase(page, 'station');
  const bounds = await page.locator('.game-scene').boundingBox();
  await page.mouse.click(bounds.x + bounds.width / 2, bounds.y + bounds.height * .65);
  await page.getByRole('status').filter({ hasText: '蹭了蹭' }).waitFor();
  await screenshot(page, 'station');
}

try {
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  watch(page);
  await page.goto(`${base}/rescue.html`, { waitUntil: 'networkidle' });
  await screenshot(page, 'desktop-maze');
  assert.equal(await page.getByRole('button', { name: '出网', exact: true }).isDisabled(), true);
  const initialCell = await data(page);
  await page.keyboard.press('ArrowLeft');
  assert.equal((await data(page)).col, initialCell.col, 'wall collision works in browser');
  await page.getByRole('button', { name: '暂停', exact: true }).click();
  const paused = await data(page);
  const time = await page.locator('.elapsed').textContent();
  await page.keyboard.press('ArrowUp');
  await page.waitForTimeout(600);
  assert.equal((await data(page)).row, paused.row);
  assert.equal(await page.locator('.elapsed').textContent(), time);
  await page.keyboard.press('Escape');
  await walkMaze(page);
  await screenshot(page, 'desktop-rescue');
  await page.getByRole('button', { name: '暂停', exact: true }).click();
  const frozen = await data(page);
  await page.waitForTimeout(400);
  assert.deepEqual(await data(page), frozen, 'pause freezes aim and animal as well as input');
  await page.getByRole('button', { name: '继续救助', exact: true }).click();
  const pixels = await scenePixels(page);
  assert.ok(pixels.colors > 80 && pixels.dark > 200 && pixels.opaque > 50000, `rendered scene pixels: ${JSON.stringify(pixels)}`);
  await rescueAnimal(page);
  await page.getByRole('button', { name: '再去救助', exact: true }).click();
  await phase(page, 'maze');
  assert.equal(await page.locator('.game-scene .scene-animal').count(), 1);

  const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true });
  const phone = await mobile.newPage();
  watch(phone);
  await phone.goto(`${base}/rescue.html`, { waitUntil: 'networkidle' });
  for (const [width, height] of [[320, 640], [390, 844], [430, 932], [768, 1024], [1440, 1000]]) {
    await phone.setViewportSize({ width, height });
    await checkGeometry(phone);
    await screenshot(phone, `maze-${width}`);
  }
  await phone.setViewportSize({ width: 390, height: 844 });
  await phone.getByRole('button', { name: '试玩设置' }).click();
  assert.equal(await phone.getByRole('button', { name: '关闭设置' }).evaluate(button => button === document.activeElement), true);
  await phone.keyboard.press('Shift+Tab');
  assert.equal(await phone.getByRole('button', { name: '开始新一轮' }).evaluate(button => button === document.activeElement), true, 'modal traps focus');
  await phone.getByRole('button', { name: '北门 · 上方' }).click();
  await phone.getByRole('button', { name: '小狗', exact: true }).click();
  await screenshot(phone, 'settings');
  await phone.getByRole('button', { name: '开始新一轮' }).click();
  assert.equal((await data(phone)).row, '0');
  await phone.emulateMedia({ reducedMotion: 'reduce' });
  await walkMaze(phone, 'top', true);
  await screenshot(phone, 'mobile-dog-rescue');
  await rescueAnimal(phone, false);
  for (const [width, height] of [[320, 640], [390, 844], [1440, 1000]]) {
    await phone.setViewportSize({ width, height });
    await checkGeometry(phone);
    await screenshot(phone, `station-${width}`);
  }
  assert.equal(await phone.evaluate(() => localStorage.getItem('fluffy-days-v1')), null, 'old save untouched');
  await phone.reload({ waitUntil: 'networkidle' });
  await phase(phone, 'maze');
  assert.deepEqual(errors, [], 'no browser errors');
  assert.deepEqual(failed, [], 'no failed assets');
  console.log('PASS: both entrances, keyboard/pointer movement, wall collision, pause, moving aim/animal, range miss/retry, food, catch/retract, return, feedback, replay, settings, focus trap, reduced motion, five viewport sizes, rendered pixels and isolated saves.');
  console.log('Scene pixel check:', JSON.stringify(pixels));
} finally { await browser.close(); }
