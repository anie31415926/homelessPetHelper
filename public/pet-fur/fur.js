(() => {
    'use strict';

    const main = document.querySelector('main');
    const canvas = document.querySelector('canvas');
    const buttons = document.querySelectorAll('[data-mode]');
    const context = canvas.getContext('2d');
    const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
    const portrait = new Image();
    const width = 400;
    const height = 470;
    const offsetX = 70;
    const offsetY = 33;
    const tufts = [];
    const zones = [
        { x: 205, y: 111, rx: 72, ry: 22, spacing: 5.4, length: 12, angle: -Math.PI / 2,
            spread: 1, light: '#fff0ce', shade: '#ab6b34' },
        { x: 177, y: 297, rx: 61, ry: 43, spacing: 6.9, length: 21, angle: Math.PI / 2,
            spread: -.8, light: '#fffdf7', shade: '#be9d78' },
    ];

    let seed = 117;
    let mode = 'live';
    let pointerId = null;
    let last = null;
    let frame = 0;
    let lastFrame = 0;
    let ready = false;

    const clamp = value => Math.max(0, Math.min(1, value));
    const smooth = value => { const t = clamp(value); return t * t * (3 - 2 * t); };
    function random() {
        seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
        return seed / 4294967296;
    }

    function buildTufts(zone) {
        for (let row = -zone.ry; row < zone.ry; row += zone.spacing) {
            for (let col = -zone.rx; col < zone.rx; col += zone.spacing) {
                const dx = col + (random() - .5) * zone.spacing;
                const dy = row + (random() - .5) * zone.spacing;
                const edge = 1 - Math.hypot(dx / zone.rx, dy / zone.ry);
                if (edge <= 0) continue;
                const angle = zone.angle + zone.spread * dx / zone.rx + (random() - .5) * .56;
                tufts.push({
                    x: zone.x + dx, y: zone.y + dy,
                    ux: Math.cos(angle), uy: Math.sin(angle),
                    length: zone.length * (.55 + random() * .8),
                    width: .85 + random() * 1.15,
                    splay: Math.tanh(dx / 15) * .5 + Math.sin((dx + dy * .7) / 9) * .24,
                    edge: smooth(edge / .46), light: zone.light, shade: zone.shade,
                    press: 0, sweep: 0, lift: 0,
                });
            }
        }
    }

    function curve(tuft, dx, dy, weight) {
        const { x, y, ux, uy, length, width: lineWidth } = tuft;
        const nx = -uy * lineWidth;
        const ny = ux * lineWidth;
        context.beginPath();
        context.moveTo(x + nx * weight, y + ny * weight);
        context.quadraticCurveTo(x + ux * length * .42 + nx, y + uy * length * .42 + ny, x + dx, y + dy);
        context.quadraticCurveTo(x + dx * .51 - nx, y + dy * .51 - ny, x - nx * weight, y - ny * weight);
        context.fill();
    }

    function paintTuft(tuft) {
        const { ux, uy, length, edge, press, sweep, lift, splay } = tuft;
        const lay = 1 - press * .40 - lift * .82;
        const turn = sweep * .85 - splay * lift;
        const dx = length * (ux * lay - uy * turn);
        const dy = length * (uy * lay + ux * turn);
        context.fillStyle = tuft.shade;
        context.globalAlpha = edge * (.037 + press * .15 + lift * .46 + Math.abs(sweep) * .15);
        curve(tuft, dx, dy, .54 + lift * .54);
        context.fillStyle = tuft.light;
        context.globalAlpha = edge * (.11 + press * .18 + lift * .52);
        curve(tuft, dx - uy * 1.28, dy + ux * 1.28, .31 + lift * .26);
    }

    function paint() {
        context.setTransform(canvas.width / width, 0, 0, canvas.height / height, 0, 0);
        context.clearRect(0, 0, width, height);
        context.fillStyle = '#698875';
        context.globalAlpha = .22;
        context.beginPath();
        context.ellipse(192, 448, 119, 13, 0, 0, Math.PI * 2);
        context.fill();
        context.globalAlpha = 1;
        context.translate(-offsetX, -offsetY);
        context.drawImage(portrait, 0, 0);
        for (const tuft of tufts) paintTuft(tuft);
        context.globalAlpha = 1;
    }

    function animate(time) {
        frame = 0;
        const elapsed = lastFrame ? Math.min(time - lastFrame, 48) : 16;
        const decay = Math.exp(-elapsed / (reducedMotion.matches ? 40 : 162));
        lastFrame = time;
        let active = false;
        for (const tuft of tufts) {
            const held = last ? pressureAt(tuft, last.x, last.y) * .70 : 0;
            tuft.press = held + Math.max(0, tuft.press - held) * decay;
            tuft.sweep *= decay;
            tuft.lift *= decay;
            if (tuft.press - held + Math.abs(tuft.sweep) + tuft.lift < .007) {
                tuft.press = held;
                tuft.sweep = tuft.lift = 0;
            } else active = true;
        }
        paint();
        if (active) requestFrame();
        else lastFrame = 0;
    }

    function requestFrame() {
        if (!frame) frame = requestAnimationFrame(animate);
    }

    // The fog-like falloff controls bending and shading, not the portrait's opacity.
    function pressureAt(tuft, x, y) {
        const dx = tuft.x - x;
        const dy = tuft.y - y;
        return smooth(1 - (dx * dx + dy * dy) / (47 * 47));
    }

    function imprint(x, y, dx, dy, speed) {
        for (const tuft of tufts) {
            const weight = pressureAt(tuft, x, y);
            if (weight === 0) continue;
            const along = tuft.ux * dx + tuft.uy * dy;
            const across = tuft.ux * dy - tuft.uy * dx;
            tuft.press = Math.max(tuft.press, weight * (.66 + Math.max(0, along) * .34 * speed));
            tuft.sweep += (across * speed - tuft.sweep) * weight * .7;
            tuft.lift += (Math.max(0, -along) * speed - tuft.lift) * weight * .85;
        }
    }

    function stroke(from, to) {
        const dx = to.x - from.x;
        const dy = to.y - from.y;
        const distance = Math.hypot(dx, dy);
        if (distance < 1) return false;
        const speed = clamp(distance / Math.max(12, to.time - from.time) * 1.6);
        const steps = Math.min(76, Math.ceil(distance / 8));
        for (let i = 1; i <= steps; i++) {
            imprint(from.x + dx * i / steps, from.y + dy * i / steps, dx / distance, dy / distance, speed);
        }
        return true;
    }

    function point(event) {
        const bounds = canvas.getBoundingClientRect();
        return {
            x: offsetX + (event.clientX - bounds.left) * width / bounds.width,
            y: offsetY + (event.clientY - bounds.top) * height / bounds.height,
            time: event.timeStamp,
        };
    }

    function end(event) {
        if (event && event.pointerId !== pointerId) return;
        const previous = pointerId;
        pointerId = null;
        last = null;
        if (previous !== null && canvas.hasPointerCapture(previous)) canvas.releasePointerCapture(previous);
        if (previous !== null) requestFrame();
    }

    canvas.addEventListener('pointerdown', event => {
        if (!ready || pointerId !== null || event.button !== 0 || mode !== 'live') return;
        pointerId = event.pointerId;
        last = point(event);
        canvas.setPointerCapture(pointerId);
        imprint(last.x, last.y, 0, 0, 0);
        requestFrame();
    });
    canvas.addEventListener('pointermove', event => {
        if (event.pointerId !== pointerId) return;
        const next = point(event);
        if (stroke(last, next)) last = next;
        requestFrame();
    });
    for (const type of ['pointerup', 'pointercancel', 'lostpointercapture']) canvas.addEventListener(type, end);
    window.addEventListener('blur', () => end());
    canvas.addEventListener('keydown', event => {
        if (event.key !== ' ' && event.key !== 'Enter') return;
        event.preventDefault();
        if (!ready || event.repeat || pointerId !== null || mode !== 'live') return;
        const chest = zones[1];
        stroke({ x: chest.x, y: chest.y - 26, time: 0 }, { x: chest.x, y: chest.y + 24, time: 89 });
        requestFrame();
    });

    for (const button of buttons) {
        button.addEventListener('click', () => {
            if (!ready || mode === button.dataset.mode) return;
            mode = button.dataset.mode;
            end();
            for (const tuft of tufts) tuft.press = tuft.sweep = tuft.lift = 0;
            for (const option of buttons) option.setAttribute('aria-pressed', String(option.dataset.mode === mode));
            paint();
        });
    }

    function resize(force = false) {
        const bounds = canvas.getBoundingClientRect();
        const pixelRatio = Math.min(devicePixelRatio, 2);
        const targetWidth = Math.max(1, Math.round(bounds.width * pixelRatio));
        const targetHeight = Math.max(1, Math.round(bounds.height * pixelRatio));
        if (canvas.width !== targetWidth || canvas.height !== targetHeight) {
            canvas.width = targetWidth;
            canvas.height = targetHeight;
        } else if (!force) return;
        paint();
    }

    function fail() {
        document.querySelector('[role="alert"]').hidden = false;
        canvas.tabIndex = -1;
        for (const button of buttons) button.disabled = true;
        main.dataset.ready = 'error';
    }

    if (!context) {
        fail();
        return;
    }
    portrait.onload = () => {
        for (const zone of zones) buildTufts(zone);
        ready = true;
        resize(true);
        new ResizeObserver(() => resize()).observe(canvas);
        main.dataset.ready = 'ready';
    };
    portrait.onerror = fail;
    portrait.src = '../pet-affection/shiba.webp';
})();
