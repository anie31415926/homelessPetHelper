import { distribute } from './profile.js';

const clamp = value => Math.min(1, Math.max(0, value));
function smooth(value) { const t = clamp(value); return t * t * (3 - 2 * t); }

export function createPatches(regions, previous = []) {
    const byId = new Map(previous.map(patch => [patch.id, patch.roughness]));
    return distribute(regions).map(patch => {
        const roughness = byId.get(patch.id) ?? 1;
        return { ...patch, roughness, lift: patch.initial * roughness, curl: patch.roll * Math.sqrt(roughness), flick: 0, linger: 0 };
    });
}

export function settle(patches, mode, restore = false) {
    for (const patch of patches) {
        if (restore) patch.roughness = 1;
        patch.flick = 0;
        patch.linger = 0;
        patch.lift = mode === 'brush' ? patch.roughness * patch.initial : 0;
        patch.curl = mode === 'brush' ? Math.sqrt(patch.roughness) * patch.roll : 0;
    }
}

function touch(patch, x, y) {
    const dx = x - patch.x;
    const dy = y - patch.y;
    const forward = dx * patch.gx + dy * patch.gy;
    const across = Math.abs(dx * patch.gy - dy * patch.gx);
    return smooth(1 - across / (patch.width * .57)) *
        smooth((forward + 18) / 21) * smooth((patch.length + 17 - forward) / 28);
}

export function move(patches, mode, fromX, fromY, toX, toY) {
    const dx = toX - fromX;
    const dy = toY - fromY;
    const distance = Math.hypot(dx, dy);
    if (distance < 1) return false;
    if (mode === 'pet') return petStroke(patches, fromX, fromY, dx / distance, dy / distance, distance);
    const samples = Math.min(64, Math.ceil(distance / 7));
    let used = false;
    for (const patch of patches) {
        const along = (dx * patch.gx + dy * patch.gy) / distance;
        if (Math.abs(along) <= .32) continue;
        let contact = 0;
        for (let step = 0; step < samples; step++) {
            const t = (step + .5) / samples;
            contact += touch(patch, fromX + dx * t, fromY + dy * t);
        }
        contact = contact * distance / samples;
        if (contact <= 0) continue;
        const travel = Math.max(24, patch.length) * (along > 0 ? 1 : 1.16);
        const delta = contact * Math.sign(along) * (Math.abs(along) - .32) / travel;
        const roughness = clamp(patch.roughness - delta);
        if (roughness === patch.roughness) continue;
        patch.roughness = along > 0 && roughness < .045 ? 0 : roughness;
        used = true;
    }
    return used;
}

function petStroke(patches, x, y, ux, uy, distance) {
    let used = false;
    for (const patch of patches) {
        const rx = patch.x + patch.gx * patch.length * .43 - x;
        const ry = patch.y + patch.gy * patch.length * .43 - y;
        const radius = 14 + patch.width * .49;
        const cross = rx * uy - ry * ux;
        const cross2 = cross * cross;
        if (cross2 >= radius * radius) continue;
        const along = rx * ux + ry * uy;
        const reach = Math.sqrt(radius * radius - cross2);
        const enter = Math.max(0, along - reach);
        const leave = Math.min(distance, along + reach);
        if (enter >= leave) continue;

        // Integrate the soft brush across this segment instead of stamping pointer events.
        const start = enter - along, end = leave - along;
        const contact = ((radius * radius - cross2) * (end - start) - (end ** 3 - start ** 3) / 3) / (radius * radius);
        const against = smooth((.82 - (patch.gx * ux + patch.gy * uy)) / 1.82);
        const strength = .10 + .90 * against;
        patch.flick = Math.min(1, patch.flick + contact * strength / 24);
        patch.linger = Math.max(patch.linger, (44 + 45 * patch.seed) * strength);
        used = true;
    }
    return used;
}

export function advance(patches, mode, elapsed, reducedMotion) {
    let moving = false;
    const follow = Math.exp(-elapsed / (reducedMotion ? 28 : mode === 'brush' ? 116 : 50));
    for (const patch of patches) {
        if (patch.flick > 0) {
            const linger = reducedMotion ? 0 : patch.linger;
            const fadeTime = Math.max(0, elapsed - linger);
            patch.linger = Math.max(0, linger - elapsed);
            if (fadeTime > 0) patch.flick *= Math.exp(-fadeTime / (reducedMotion ? 54 : 125 + 32 * patch.seed));
        }
        if (patch.flick < .004) patch.flick = 0;
        const lift = mode === 'brush' ? patch.initial * patch.roughness : patch.flick * patch.initial;
        const curl = mode === 'brush' ? patch.roll * Math.sqrt(patch.roughness) : patch.flick * patch.roll * .65;
        patch.lift = lift + (patch.lift - lift) * follow;
        patch.curl = curl + (patch.curl - curl) * follow;
        if (Math.abs(patch.lift - lift) + Math.abs(patch.curl - curl) > .002 || patch.flick > 0) {
            moving = true;
        } else {
            patch.lift = lift;
            patch.curl = curl;
        }
    }
    return moving;
}
