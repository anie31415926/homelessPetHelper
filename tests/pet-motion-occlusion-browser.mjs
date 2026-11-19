import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from '@playwright/test';

const base = process.env.PET_LAYERS_URL || 'http://127.0.0.1:5193', output = 'test-results/pet-motion-belly';
await mkdir(output, { recursive: true });
let browser;
try { browser = await chromium.launch({ headless: true }); }
catch (error) { if (!error.message.includes("Executable doesn't exist")) throw error; browser = await chromium.launch({ channel: 'msedge', headless: true }); }
const page = await browser.newPage({ reducedMotion: 'reduce' }), errors = [], failures = [], frames = [];
page.on('pageerror', error => errors.push(error.message)); page.on('response', response => { if (response.status() >= 400) failures.push(response.url()); });
try {
    await page.goto(`${base}/pet-layers.html?motion=1&legacyPose=1&action=lie`, { waitUntil: 'networkidle' });
    await page.waitForSelector('main[data-status="ready"]');
    for (const [width, height] of [[1280, 900], [390, 780], [375, 667], [320, 568], [844, 390]]) {
        await page.setViewportSize({ width, height });
        const checks = await page.evaluate(async () => {
            const { createMotionPreview } = await import('/src/pet-layers/motion-renderer.js');
            const host = document.createElement('div'), rect = document.querySelector('.mesh-canvas').getBoundingClientRect();
            Object.assign(host.style, { position: 'fixed', left: '-10000px', width: `${rect.width}px`, height: `${rect.height}px` });
            document.body.appendChild(host);
            const preview = await createMotionPreview(host), { renderer, scene, camera } = preview;
            const far = scene.getObjectByName('farHind'), target = renderer.domElement;
            const connected = scene.getObjectByName('connected'), image = connected.material.map.image;
            const texture = document.createElement('canvas'); texture.width = image.width; texture.height = image.height;
            const textureContext = texture.getContext('2d'); textureContext.drawImage(image, 0, 0);
            const alpha = textureContext.getImageData(0, 0, texture.width, texture.height).data;
            const source = connected.geometry.getAttribute('position'), uv = connected.geometry.getAttribute('uv'), triangles = connected.geometry.index.array;
            const surfaces = [];
            for (let i = 0; i < triangles.length; i += 3) {
                const ids = [...triangles.slice(i, i + 3)];
                const x = ids.reduce((sum, index) => sum + uv.getX(index) * 452, 0) / 3;
                const y = ids.reduce((sum, index) => sum + (1 - uv.getY(index)) * 512, 0) / 3;
                if (x > 180 && x < 375 && y > 350 && y < 498 && alpha[(Math.floor(y) * 452 + Math.floor(x)) * 4 + 3] > 128) surfaces.push(ids);
            }
            const copy = document.createElement('canvas'); copy.width = target.width; copy.height = target.height;
            const context = copy.getContext('2d');
            const pixels = () => { context.clearRect(0, 0, copy.width, copy.height); context.drawImage(target, 0, 0); return context.getImageData(0, 0, copy.width, copy.height).data; };
            const result = [];
            try {
                for (const amount of [.8, 1]) for (const time of [.4, .7, 1, 1.3, 1.6, 2.2, 3.2, 3.8, 4.2, 4.7]) {
                    preview.draw({ mode: 'motion', action: 'lie', amount, body: true, wire: false, bones: false }, time);
                    let invertedSurface = 0;
                    for (const ids of surfaces) {
                        const [a, b, c] = ids.map(index => connected.applyBoneTransform(index, connected.position.clone().fromBufferAttribute(source, index)));
                        if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 0) invertedSurface++;
                    }
                    if (!far.visible || far.material.opacity !== 1) throw new Error('Far hind must remain rendered, not fade or disappear');
                    const complete = pixels();
                    far.visible = false; renderer.render(scene, camera); const without = pixels();
                    let exposed = 0, left = copy.width, right = -1, top = copy.height, bottom = -1;
                    for (let p = 0; p < complete.length; p += 4) {
                        if (complete[p + 3] > 64 && [0, 1, 2, 3].some(c => Math.abs(complete[p + c] - without[p + c]) > 8)) {
                            exposed++;
                            const x = p / 4 % copy.width, y = Math.floor(p / 4 / copy.width);
                            left = Math.min(left, x); right = Math.max(right, x); top = Math.min(top, y); bottom = Math.max(bottom, y);
                        }
                    }
                    const visibility = [];
                    scene.traverse(object => { if (object.isMesh) { visibility.push([object, object.visible]); object.visible = object === far; } });
                    renderer.render(scene, camera); const isolated = pixels();
                    let farPixels = 0;
                    for (let p = 0; p < isolated.length; p += 4) if (isolated[p + 3] > 128) farPixels++;
                    for (const [object, visible] of visibility) object.visible = visible;
                    far.visible = true;
                    result.push({ amount, time, exposed, farPixels, invertedSurface, checkedSurfaces: surfaces.length, left, right, top, bottom, width: copy.width, height: copy.height });
                }
            } finally { preview.dispose(); host.remove(); }
            return result;
        });
        frames.push(...checks.map(check => ({ viewport: [width, height], ...check })));
        await page.getByLabel('动作幅度', { exact: true }).fill('80');
        await page.getByLabel('动作进度', { exact: true }).fill('423'); await page.waitForTimeout(100);
        await page.screenshot({ path: `${output}/preview-${width}x${height}.png` });
    }
    await writeFile(`${output}/occlusion-report.json`, JSON.stringify({ frames, errors, failures }, null, 2));
    for (const frame of frames) {
        assert.ok(frame.farPixels > 1000, 'actual far hind remains visible when rendered alone');
        assert.ok(frame.checkedSurfaces > 100); assert.equal(frame.invertedSurface, 0, 'opaque abdomen/croup/hind triangles must not fold over');
        if (frame.time >= 1.6 && frame.time <= 3.2) assert.ok(frame.exposed / frame.farPixels < .004, `lying far hind must be covered, exposed ${frame.exposed}/${frame.farPixels}`);
    }
    assert.deepEqual(errors, []); assert.deepEqual(failures, []);
    console.log('Belly occlusion passed: 100 real-WebGL frames, opaque continuous surface without inverted triangles, far hind covered rather than hidden.');
} finally { await browser.close(); }
