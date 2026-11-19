import { clamp } from '../pet-mesh/skin.js';
import { resetSkin, UNIT } from './motion.js';
import { createRestSkin } from './rest-skin.js';

const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
const at = (id, parent, x, y) => ({ id, parent, u: x / 452, v: y / 512 });
export const SIDE_SPEC = [
    at('root', null, 226, 256), at('chest', 'root', 250, 430), at('head', 'root', 137, 425),
    at('earFar', 'head', 65, 367), at('earNear', 'head', 150, 313), at('tailBase', 'root', 387, 410),
];
const mix = (weights, id, amount) => {
    for (const key of Object.keys(weights)) weights[key] *= 1 - amount;
    weights[id] = (weights[id] || 0) + amount;
};
export function sideWeightsAt(x, y) {
    if (!Number.isFinite(x) || !Number.isFinite(y)) throw new Error('Invalid source coordinate');
    const chest = smooth(200, 245, x) * (1 - smooth(286, 355, x)) * (1 - smooth(420, 439, y));
    const weights = { 0: 1 - chest, 1: chest };
    const head = (1 - smooth(195, 238, x)) * (1 - smooth(411, 434, y));
    mix(weights, 2, head);
    mix(weights, 3, head * (1 - smooth(80, 104, x)) * (1 - smooth(357, 380, y)));
    mix(weights, 4, head * smooth(106, 129, x) * (1 - smooth(310, 335, y)));
    const tail = Math.max(smooth(374, 390, x) * smooth(401, 425, y), smooth(319, 340, x) * smooth(469, 482, y));
    mix(weights, 5, tail);
    const entries = Object.entries(weights).filter(([, w]) => w > 1e-7).sort((a, b) => b[1] - a[1]).slice(0, 4);
    const sum = entries.reduce((total, [, w]) => total + w, 0);
    return entries.map(([index, weight]) => ({ index: Number(index), weight: weight / sum }));
}
export function createSideRest(material) {
    return createRestSkin(material, SIDE_SPEC, sideWeightsAt);
}
export function applySideRest(skin, pose) {
    resetSkin(skin);
    // Whole-pose art stays side-reclining. Local translations/rotations only, no fake rolling or tail scaling.
    skin.bones.chest.position.y += pose.breathe * 160 * UNIT;
    skin.bones.head.rotation.z = pose.head;
    skin.bones.earNear.rotation.z = pose.ears;
    skin.bones.earFar.rotation.z = -pose.ears * .65;
    skin.bones.tailBase.rotation.z = pose.tail * .35;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
