import { BufferAttribute, PlaneGeometry } from 'three';
import { bindSkin, clamp, GRID, SIZE } from '../pet-mesh/skin.js';
import { resetSkin, UNIT } from './motion.js';

export const POSTURES = ['sitMesh'];
const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
const at = (id, parent, x, y) => ({ id, parent, u: x / 452, v: y / 512 });
export const POSTURE_SPEC = [
    at('root', null, 226, 256), at('chest', 'root', 147, 347), at('head', 'chest', 145, 165),
    at('earFar', 'head', 97, 90), at('earNear', 'head', 165, 125),
    at('pelvis', 'root', 315, 360), at('hindFoot', 'root', 290, 456), at('tailBase', 'pelvis', 372, 411),
];
const mix = (base, added, amount) => {
    for (const id of Object.keys(base)) base[id] *= 1 - amount;
    for (const [id, weight] of Object.entries(added)) base[id] = (base[id] || 0) + weight * amount;
};
export function postureWeightsAt(x, y) {
    if (!Number.isFinite(x) || !Number.isFinite(y)) throw new Error('Invalid source coordinate');
    const rear = smooth(175, 330, x), weights = { 1: 1 - rear, 5: rear };
    const head = (1 - smooth(250, 310, y)) * (1 - smooth(260, 290, x));
    mix(weights, { 2: 1 }, head);
    mix(weights, { 3: 1 }, (1 - smooth(78, 125, y)) * (1 - smooth(100, 140, x)) * head);
    mix(weights, { 4: 1 }, (1 - smooth(100, 165, y)) * smooth(110, 155, x) * head);
    mix(weights, { 6: 1 }, smooth(370, 490, y) * smooth(220, 255, x) * (1 - smooth(346, 371, x)));
    mix(weights, { 7: 1 }, smooth(345, 369, x) * smooth(390, 436, y));
    const entries = Object.entries(weights).filter(([, weight]) => weight > 1e-7).sort((a, b) => b[1] - a[1]).slice(0, 4);
    const sum = entries.reduce((total, [, weight]) => total + weight, 0);
    return entries.map(([index, weight]) => ({ index: Number(index), weight: weight / sum }));
}
export function createPostureBody(material) {
    const geometry = new PlaneGeometry(SIZE.width, SIZE.height, GRID.columns, GRID.rows);
    const uv = geometry.getAttribute('uv'), indices = [], weights = [];
    for (let i = 0; i < uv.count; i++) {
        const influences = postureWeightsAt(uv.getX(i) * 452, (1 - uv.getY(i)) * 512);
        for (let j = 0; j < 4; j++) { indices.push(influences[j]?.index || 0); weights.push(influences[j]?.weight || 0); }
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    return bindSkin(material, geometry, POSTURE_SPEC);
}
export function postureControls(pose) {
    const p = clamp(pose.posture, 0, 1);
    return {
        chest: [35 * p, 0], head: [35 * p, -4 * p], headAngle: -.035 * p,
        pelvis: [-10 * p, 65 * p], pelvisAngle: -.05 * p,
        hindFoot: [-10 * p, 0], tailAngle: 1.05 * p + pose.tail * .3,
        frontAngle: 0,
    };
}
const translate = (bone, [x, y]) => { bone.position.x += x * UNIT; bone.position.y -= y * UNIT; };
export function applyPostureBody(skin, pose) {
    resetSkin(skin);
    const c = postureControls(pose), { bones } = skin;
    translate(bones.chest, c.chest);
    bones.chest.rotation.z = c.chestAngle || 0;
    translate(bones.head, [c.head[0] - c.chest[0], c.head[1] - c.chest[1]]);
    bones.head.rotation.z = c.headAngle;
    bones.earNear.rotation.z = pose.ears; bones.earFar.rotation.z = -pose.ears * .65;
    translate(bones.pelvis, c.pelvis); bones.pelvis.rotation.z = c.pelvisAngle;
    translate(bones.hindFoot, c.hindFoot);
    bones.tailBase.rotation.z = c.tailAngle - c.pelvisAngle;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
export function applyPostureFront(skin, pose, side) {
    resetSkin(skin);
    const c = postureControls(pose), { bones } = skin;
    translate(bones.body, c.chest);
    if (pose.posture) {
        // Follow the settling shoulder horizontally, keeping the paw on the floor.
        bones.target.position.x += c.chest[0] * UNIT;
        bones.lower.rotation.z = -.01;
        skin.mesh.updateMatrixWorld(true); skin.solver.update();
        bones.ankle.rotation.z = -bones.upper.rotation.z - bones.lower.rotation.z;
    }
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
export function applyPostureFarHind(skin, pose) {
    resetSkin(skin);
    const c = postureControls(pose), { bones } = skin;
    translate(bones.body, c.pelvis);
    bones.target.position.x += c.hindFoot[0] * UNIT;
    if (pose.posture) {
        bones.lower.rotation.z = .12;
        skin.mesh.updateMatrixWorld(true); skin.solver.update();
        bones.ankle.rotation.z = -bones.upper.rotation.z - bones.lower.rotation.z;
    }
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
