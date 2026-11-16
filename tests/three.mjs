import { chromium } from '@playwright/test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

await fs.mkdir('test-results', { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 1024 }, deviceScaleFactor: 1, reducedMotion: 'reduce' });
const page = await context.newPage();
const errors = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
const failed = [];
page.on('response', response => { if (response.status() >= 400) failed.push(response.url()); });
try {
  await page.goto('http://localhost:5173/?view=3d', { waitUntil: 'networkidle' });
  const canvas = page.locator('.pet-world canvas');
  await canvas.waitFor({ state: 'visible' });
  await page.waitForFunction(() => Number(document.querySelector('.pet-world canvas')?.dataset.frames) >= 1);
  const save = () => page.evaluate(() => JSON.parse(localStorage.getItem('fluffy-days-v1')));
  const snapshot = () => page.evaluate(() => {
    const webgl = document.querySelector('.pet-world canvas');
    const output = document.createElement('canvas'); output.width = 160; output.height = 140;
    const ctx = output.getContext('2d'); ctx.drawImage(webgl, 0, 0, 160, 140);
    const data = ctx.getImageData(0, 0, 160, 140).data;
    const colors = new Set(); let light = 0; let dark = 0;
    for (let i = 0; i < data.length; i += 4) {
      colors.add(`${data[i] >> 3},${data[i + 1] >> 3},${data[i + 2] >> 3}`);
      if (data[i] > 200 && data[i + 1] > 185 && data[i + 2] > 160) light++;
      if (data[i] < 160 && data[i + 1] < 160 && data[i + 2] < 160) dark++;
    }
    let hash = 0; for (let i = 0; i < data.length; i += 12) hash = (hash * 31 + data[i]) >>> 0;
    return { colors: colors.size, light, dark, hash, ...webgl.dataset };
  });
  async function renderTick() { await page.waitForTimeout(160); }
  async function perform(label, count) { for (let i = 0; i < count; i++) await page.getByRole('button', { name: label, exact: true }).last().click(); await renderTick(); }
  async function closeMilestone() { const modal = page.getByRole('dialog', { name: '你看，它长大啦' }); if (await modal.isVisible()) await modal.getByRole('button', { name: '关闭', exact: true }).click(); }
  await page.screenshot({ path: 'test-results/3d-desktop-initial.png', fullPage: true });
  const initial = await snapshot();
  assert.ok(initial.colors > 200 && initial.light > 1000 && initial.dark > 100, `blank 3D canvas: ${JSON.stringify(initial)}`);
  assert.equal(initial.kind, 'cat'); assert.equal(initial.mud, '8');
  console.log('3D initial pixel check:', JSON.stringify(initial));
  const sceneBounds = await canvas.boundingBox();
  const mood = (await save()).pets.cat.mood;
  await page.mouse.click(sceneBounds.x + sceneBounds.width * .5, sceneBounds.y + sceneBounds.height * .56);
  assert.equal((await save()).pets.cat.mood, mood + 2, 'raycast pet click');
  await page.getByRole('button', { name: '转动视角', exact: true }).click(); await renderTick();
  assert.notEqual((await snapshot()).hash, initial.hash, 'camera should turn');
  await page.screenshot({ path: 'test-results/3d-camera-turn.png', fullPage: true });
  await page.getByRole('button', { name: '恢复正面', exact: true }).click(); await renderTick();
  await page.getByRole('button', { name: '洗澡', exact: true }).click();
  await perform('打泡泡', 1);
  const bounds = await canvas.boundingBox();
  await page.mouse.move(bounds.x + bounds.width * .46, bounds.y + bounds.height * .62);
  await page.mouse.down();
  for (let i = 0; i < 2; i++) {
    await page.waitForTimeout(220);
    await page.mouse.move(bounds.x + bounds.width * (i % 2 ? .46 : .53), bounds.y + bounds.height * .62);
  }
  await page.mouse.up(); await renderTick();
  assert.equal((await snapshot()).mud, '5');
  assert.equal((await snapshot()).room, 'bathroom');
  await page.screenshot({ path: 'test-results/3d-bath-bubbles.png', fullPage: true });
  await perform('打泡泡', 2); await perform('冲干净', 5); await perform('吹蓬松', 3);
  assert.equal((await save()).pets.cat.clean, 100); assert.equal((await snapshot()).mud, '0');
  await page.getByRole('button', { name: '护理', exact: true }).click();
  await perform('梳毛毛', 6); await perform('修整毛毛', 3);
  assert.equal((await snapshot()).messy, 'false');
  await page.getByRole('button', { name: '就医', exact: true }).click();
  await perform('测体温', 1);
  await page.screenshot({ path: 'test-results/3d-clinic.png', fullPage: true });
  assert.equal((await snapshot()).room, 'clinic');
  await perform('听心跳', 1); await perform('吃药药', 1); await closeMilestone();
  await page.getByRole('button', { name: '喂食', exact: true }).click();
  await page.getByRole('dialog').locator('.food-row').nth(1).getByRole('button', { name: '喂食' }).click();
  await renderTick();
  assert.equal((await save()).pets.cat.hunger, 81);
  await page.getByRole('button', { name: '小背包', exact: true }).click();
  await page.getByRole('tab', { name: '小衣橱' }).click();
  await page.getByRole('button', { name: /莓果蝴蝶结/ }).click();
  await page.getByRole('dialog').getByRole('button', { name: '关闭', exact: true }).click(); await renderTick();
  await page.screenshot({ path: 'test-results/3d-clean-bow.png', fullPage: true });
  assert.equal((await snapshot()).stage, '1');
  await page.getByRole('button', { name: '2D', exact: true }).click();
  assert.equal(await canvas.count(), 0);
  assert.equal(await page.locator('.scene .pet-art').count(), 1);
  await page.getByRole('button', { name: '3D', exact: true }).click();
  await canvas.waitFor({ state: 'visible' }); await renderTick();
  assert.equal((await save()).pets.cat.accessory, 'bow');
  await page.getByRole('button', { name: '陪玩', exact: true }).click(); await perform('追毛线球', 5);
  await page.getByRole('button', { name: '晚安，明天见', exact: true }).click();
  await page.getByRole('button', { name: '睡醒，迎接第 2 天', exact: true }).click();
  await page.getByRole('button', { name: '喂食', exact: true }).click();
  await page.getByRole('dialog').locator('.food-row').nth(1).getByRole('button', { name: '喂食' }).click();
  await page.getByRole('button', { name: '洗澡', exact: true }).click();
  await perform('打泡泡', 5); await perform('冲干净', 5); await perform('吹蓬松', 3);
  await page.getByRole('button', { name: '护理', exact: true }).click();
  await perform('梳毛毛', 6); await perform('修整毛毛', 3); await closeMilestone(); await renderTick();
  assert.equal((await snapshot()).stage, '2');
  await page.screenshot({ path: 'test-results/3d-adult-cat.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 }); await renderTick();
  await page.waitForTimeout(3100);
  await page.screenshot({ path: 'test-results/3d-mobile-adult.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 1024 }); await renderTick();
  await page.getByRole('button', { name: '收养布丁', exact: true }).first().click();
  await page.getByRole('button', { name: '带布丁回家', exact: true }).click(); await renderTick();
  assert.equal((await snapshot()).kind, 'dog');
  await page.screenshot({ path: 'test-results/3d-dog.png', fullPage: true });
  for (const [width, height] of [[390, 844], [320, 720], [768, 1024], [1280, 900]]) {
    await page.setViewportSize({ width, height }); await renderTick();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
    assert.equal(overflow, false, `horizontal overflow at ${width}`);
    const pixels = await snapshot(); assert.ok(pixels.colors > 200 && pixels.light > 1000 && pixels.dark > 100, `blank at ${width}`);
    await page.screenshot({ path: `test-results/3d-view-${width}.png`, fullPage: true });
  }
  await page.setViewportSize({ width: 390, height: 844 });
  await renderTick(); await page.waitForTimeout(3100);
  await page.screenshot({ path: 'test-results/3d-mobile-dog.png', fullPage: true });
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await page.waitForTimeout(350);
  const movingA = await snapshot(); await page.waitForTimeout(550); const movingB = await snapshot();
  assert.ok(Number(movingB.frames) > Number(movingA.frames) + 3, 'idle animation renders');
  assert.notEqual(movingB.hash, movingA.hash, 'pet animation changes visible pixels');
  await page.emulateMedia({ reducedMotion: 'reduce' }); await page.waitForTimeout(200);
  const staticFrames = Number((await snapshot()).frames); await page.waitForTimeout(250);
  assert.ok(Number((await snapshot()).frames) <= staticFrames + 1, 'reduced motion stops idle rendering');
  assert.deepEqual(errors, [], 'browser errors'); assert.deepEqual(failed, [], 'failed assets');
  const noWebGLContext = await browser.newContext();
  const fallbackPage = await noWebGLContext.newPage();
  await fallbackPage.addInitScript(() => {
    const getContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function(type, ...args) { return type.startsWith('webgl') ? null : getContext.call(this, type, ...args); };
  });
  await fallbackPage.goto('http://localhost:5173/?view=3d', { waitUntil: 'networkidle' });
  await fallbackPage.getByRole('button', { name: '回到绘本小窝', exact: true }).click();
  assert.equal(await fallbackPage.locator('.scene .pet-art').count(), 1, 'no-WebGL fallback keeps the game playable');
  await noWebGLContext.close();
  console.log('PASS: WebGL pixels, raycast pet interaction, camera, care scenes, mud, neat fur, growth, wardrobe, cat/dog, 2D fallback, responsive framing, animation and reduced motion');
} finally { await browser.close(); }
