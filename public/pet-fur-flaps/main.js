import { advance, createPatches, move, settle } from './model.js';
import { makeRenderer } from './shader.js';
import { attachEditor } from './editor.js';
import { maxPatches, validateProfile } from './profile.js';

const main = document.querySelector('main');
const canvas = document.querySelector('canvas');
const portrait = document.querySelector('.portrait img');
const buttons = document.querySelectorAll('[data-mode]');
const reset = document.querySelector('.reset');
const tune = document.querySelector('.tune');
const status = document.querySelector('[role="status"]');
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
let profile = null;
let patches = [];
let mode = 'brush';
let renderer = null;
let frame = 0;
let lastFrame = 0;
let pointer = null;
let previous = null;

function draw(time) {
    frame = 0;
    if (!renderer) return;
    const elapsed = lastFrame ? Math.min(48, time - lastFrame) : 16;
    const moving = advance(patches, mode, elapsed, reducedMotion.matches);
    renderer.draw(patches);
    if (moving) {
        lastFrame = time;
        requestDraw();
    } else lastFrame = 0;
}

function requestDraw() {
    if (renderer && !frame) frame = requestAnimationFrame(draw);
}

function end(event) {
    if (event && event.pointerId !== pointer) return;
    const current = pointer;
    pointer = null;
    previous = null;
    if (current !== null) {
        if (canvas.hasPointerCapture(current)) canvas.releasePointerCapture(current);
    }
}

function point(event, rect = canvas.getBoundingClientRect()) {
    const crop = profile.portrait.crop;
    return { x: crop.x + (event.clientX - rect.left) * crop.width / rect.width,
        y: crop.y + (event.clientY - rect.top) * crop.height / rect.height };
}

canvas.addEventListener('pointerdown', event => {
    if (!renderer || pointer !== null || event.button !== 0) return;
    pointer = event.pointerId;
    previous = point(event);
    canvas.setPointerCapture(pointer);
});
function moveTo(next) {
    if (Math.hypot(next.x - previous.x, next.y - previous.y) < 1) return;
    if (move(patches, mode, previous.x, previous.y, next.x, next.y)) requestDraw();
    previous = next;
}

canvas.addEventListener('pointermove', event => {
    if (!previous || event.pointerId !== pointer) return;
    const samples = mode === 'pet' ? event.getCoalescedEvents?.() : undefined;
    if (samples?.length) {
        const rect = canvas.getBoundingClientRect();
        for (const sample of samples) moveTo(point(sample, rect));
    } else moveTo(point(event));
});
for (const type of ['pointerup', 'pointercancel', 'lostpointercapture']) canvas.addEventListener(type, end);
window.addEventListener('blur', () => end());
document.addEventListener('visibilitychange', () => { if (document.hidden) end(); });

canvas.addEventListener('keydown', event => {
    if (!renderer || (event.key !== ' ' && event.key !== 'Enter')) return;
    event.preventDefault();
    if (event.repeat || pointer !== null) return;
    if (mode === 'pet') {
        const region = patches[0]?.region;
        for (const patch of patches) {
            if (patch.region !== region) continue;
            patch.flick = .91;
            patch.linger = 75;
        }
    } else {
        for (const patch of patches) {
            const from = event.shiftKey ? patch.length : 4;
            const to = event.shiftKey ? 4 : patch.length;
            move([patch], mode, patch.x + patch.gx * from, patch.y + patch.gy * from,
                patch.x + patch.gx * to, patch.y + patch.gy * to);
        }
    }
    requestDraw();
});

function resetPose(restore) {
    end();
    settle(patches, mode, restore);
    requestDraw();
}

for (const button of buttons) {
    button.addEventListener('click', () => {
        if (mode === button.dataset.mode) return;
        mode = button.dataset.mode;
        for (const option of buttons) option.setAttribute('aria-pressed', String(option.dataset.mode === mode));
        updateCanvasLabel();
        resetPose(false);
    });
}
reset.addEventListener('click', () => resetPose(true));

function resize() {
    const ratio = Math.min(2, devicePixelRatio);
    const rect = canvas.getBoundingClientRect();
    const width = Math.max(1, Math.round(rect.width * ratio));
    const height = Math.max(1, Math.round(rect.height * ratio));
    if (canvas.width === width && canvas.height === height) return;
    canvas.width = width;
    canvas.height = height;
    requestDraw();
}

function unavailable() {
    end();
    cancelAnimationFrame(frame);
    frame = lastFrame = 0;
    renderer = null;
    canvas.hidden = true;
    for (const button of buttons) button.disabled = true;
    reset.disabled = true;
    tune.disabled = true;
    status.hidden = false;
    main.dataset.ready = 'unavailable';
}

async function start() {
    try {
        if (!profile) await loadProfile();
        renderer = makeRenderer(canvas, portrait, profile.portrait.crop, maxPatches);
        if (!renderer) {
            unavailable();
            return;
        }
        canvas.hidden = false;
        for (const button of buttons) button.disabled = false;
        reset.disabled = false;
        tune.disabled = false;
        status.hidden = true;
        renderer.setLight(profile.light);
        resize();
        requestDraw();
        main.dataset.ready = 'ready';
    } catch (error) {
        console.warn('Interactive fur unavailable:', error);
        unavailable();
    }
}

async function loadProfile() {
    const name = new URL(location.href).searchParams.get('profile');
    if (name !== null && !/^[a-z0-9][a-z0-9-]*$/.test(name)) throw new Error('Invalid fur profile name');
    const url = new URL(name ? `./${name}.json` : main.dataset.profile, document.baseURI);
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Cannot read fur profile: ${response.status}`);
    const settings = validateProfile(await response.json());
    const { crop, width, height } = settings.portrait;
    portrait.src = new URL(settings.portrait.image, url).href;
    await portrait.decode();
    if (portrait.naturalWidth !== width || portrait.naturalHeight !== height) throw new Error('Fur profile image dimensions do not match');
    portrait.width = width;
    portrait.height = height;
    portrait.alt = settings.name;
    main.querySelector('h1').textContent = settings.name;
    document.title = `${settings.name} · 抚摸与梳理`;
    const styles = portrait.parentElement.style;
    styles.setProperty('--portrait-ratio', crop.width / crop.height);
    styles.setProperty('--image-width', `${width / crop.width * 100}%`);
    styles.setProperty('--image-left', `${-crop.x / crop.width * 100}%`);
    styles.setProperty('--image-top', `${-crop.y / crop.height * 100}%`);
    profile = settings;
    updateCanvasLabel();
    patches = createPatches(profile.regions);
    settle(patches, mode);
    attachEditor(main, profile, change => {
        if (change !== 'light') {
            patches = createPatches(profile.regions, change === 'restore' ? [] : patches);
            resetPose(false);
        }
        renderer?.setLight(profile.light);
        requestDraw();
    });
}

function updateCanvasLabel() {
    canvas.setAttribute('aria-label', `${mode === 'brush' ? '梳理' : '抚摸'}${profile.name}`);
}

new ResizeObserver(resize).observe(canvas);
canvas.addEventListener('webglcontextlost', event => { event.preventDefault(); unavailable(); });
canvas.addEventListener('webglcontextrestored', start);
start();
