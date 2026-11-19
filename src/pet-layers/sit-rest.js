import { clamp } from '../pet-mesh/skin.js';
import { resetSkin, UNIT } from './motion.js';
import { createRestSkin } from './rest-skin.js';

const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
const at = (id, parent, x, y) => ({ id, parent, u: x / 452, v: y / 512 });
export const SIT_SPEC = [
    at('root', null, 226, 256), at('chest', 'root', 143, 399), at('head', 'root', 115, 277),
    at('earFar', 'head', 65, 109), at('earNear', 'head', 120, 137), at('tailBase', 'root', 311, 459),
];
const mix = (weights, id, amount) => {
    for (const key of Object.keys(weights)) weights[key] *= 1 - amount;
    weights[id] = (weights[id] || 0) + amount;
};
export function sitWeightsAt(x, y) {
    if (!Number.isFinite(x) || !Number.isFinite(y)) throw new Error('Invalid source coordinate');
    const chest = (1 - smooth(190, 275, x)) * (1 - smooth(365, 420, y));
    const weights = { 0: 1 - chest, 1: chest };
    const head = 1 - smooth(252, 300, y);
    mix(weights, 2, head);
    mix(weights, 3, head * (1 - smooth(75, 95, x)) * (1 - smooth(105, 139, y)));
    mix(weights, 4, head * smooth(85, 108, x) * (1 - smooth(130, 162, y)));
    mix(weights, 5, smooth(304, 328, x) * smooth(390, 414, y));
    const entries = Object.entries(weights).filter(([, w]) => w > 1e-7).sort((a, b) => b[1] - a[1]).slice(0, 4);
    const sum = entries.reduce((total, [, w]) => total + w, 0);
    return entries.map(([index, weight]) => ({ index: Number(index), weight: weight / sum }));
}
export function createSitRest(material) {
    return createRestSkin(material, SIT_SPEC, sitWeightsAt);
}
export function applySitRest(skin, pose) {
    resetSkin(skin);
    // Keep the seated silhouette and planted feet; no standing-to-sitting warp.
    skin.bones.chest.position.y += pose.breathe * 140 * UNIT;
    skin.bones.head.rotation.z = pose.head;
    skin.bones.earNear.rotation.z = pose.ears;
    skin.bones.earFar.rotation.z = -pose.ears * .65;
    skin.bones.tailBase.rotation.z = pose.tail * .35;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
