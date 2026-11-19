import { BufferAttribute, PlaneGeometry } from 'three';
import { bindSkin, BONE_SPEC, clamp, GRID, SIZE, weightsAt } from '../pet-mesh/skin.js';
import { createLimbSolver, resetSkin, UNIT } from './motion.js';

const at = (id, parent, x, y) => ({ id, parent, u: x / 452, v: y / 512 });
export const LYING_SPEC = [
    ...BONE_SPEC.slice(0, 5),
    at('pelvis', 'body', 315, 360),
    at('tailBase', 'pelvis', 372, 411),
    at('upper', 'pelvis', 308, 348),
    at('lower', 'upper', 267, 422),
    at('ankle', 'lower', 290, 456),
    at('paw', 'ankle', 295, 482),
    at('target', 'root', 290, 456),
];
const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
const blend = (weights, added, amount) => {
    for (const id of Object.keys(weights)) weights[id] *= 1 - amount;
    for (const [id, weight] of Object.entries(added)) weights[id] = (weights[id] || 0) + weight * amount;
};

export function lyingWeightsAt(x, y) {
    const weights = {};
    for (const item of weightsAt(x / 452, y / 512)) {
        const index = item.index < 5 ? item.index : 1;
        weights[index] = (weights[index] || 0) + item.weight;
    }
    const pelvis = smooth(208, 310, x) * smooth(282, 365, y);
    blend(weights, { 5: 1 }, pelvis);
    // The abdomen and outer croup stay with the torso; fold the lower limb underneath them.
    const abdomen = 1 - smooth(235, 300, x);
    const compression = smooth(360, 490, y) * (1 - abdomen) + clamp((y - 425) / 70, 0, 1) * abdomen;
    const leg = smooth(185, 235, x) * (1 - smooth(349, 375, x)) * compression;
    // A broad pelvis-to-planted-foot blend compresses the visible outline without flipping the thigh surface.
    blend(weights, { 9: 1 }, leg);
    // The tail core is rigid; only a short root band blends into the moving pelvis.
    blend(weights, { 6: 1 }, smooth(345, 369, x) * smooth(390, 436, y));
    const entries = Object.entries(weights).filter(([, weight]) => weight > 1e-7).sort((a, b) => b[1] - a[1]).slice(0, 4);
    const sum = entries.reduce((total, [, weight]) => total + weight, 0);
    return entries.map(([index, weight]) => ({ index: Number(index), weight: weight / sum }));
}

export function createLyingSkin(material) {
    const geometry = new PlaneGeometry(SIZE.width, SIZE.height, GRID.columns, GRID.rows);
    const uv = geometry.getAttribute('uv'), indices = [], weights = [];
    for (let i = 0; i < uv.count; i++) {
        const influences = lyingWeightsAt(uv.getX(i) * 452, (1 - uv.getY(i)) * 512);
        for (let j = 0; j < 4; j++) { indices.push(influences[j]?.index || 0); weights.push(influences[j]?.weight || 0); }
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    const skin = bindSkin(material, geometry, LYING_SPEC);
    skin.solver = createLimbSolver(skin.mesh, 'hind', { target: 11, ankle: 9, lower: 8, upper: 7 });
    return skin;
}

export function applyLyingSkin(skin, pose) {
    resetSkin(skin);
    const { bones } = skin;
    bones.head.rotation.z = pose.head;
    bones.earNear.rotation.z = pose.ears; bones.earFar.rotation.z = -pose.ears * .65;
    bones.pelvis.position.y += pose.lie * 6 * UNIT; bones.pelvis.rotation.z = pose.lie * .025;
    bones.tailBase.rotation.z = pose.lie * 1.04 + pose.tail * .35;
    bones.target.position.x += pose.feet.nearHind.dx * UNIT;
    bones.target.position.y += pose.down * UNIT;
    skin.mesh.updateMatrixWorld(true);
    if (pose.down) {
        bones.lower.rotation.z = .12;
        skin.mesh.updateMatrixWorld(true); skin.solver.update();
    }
    bones.ankle.rotation.z = -bones.pelvis.rotation.z - bones.upper.rotation.z - bones.lower.rotation.z;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
