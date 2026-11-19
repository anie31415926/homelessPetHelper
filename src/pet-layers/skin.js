import { BufferAttribute, PlaneGeometry } from 'three';
import { bindSkin, clamp, GRID, SIZE } from '../pet-mesh/skin.js';

export const ACTIONS = ['rest', 'forward', 'tuck', 'swing'];
export const BONES = [
    { id: 'root', parent: null, u: .5, v: .5 },
    { id: 'body', parent: 'root', u: .56, v: .67 },
    { id: 'upper', parent: 'body', u: 285 / 452, v: 365 / 512 },
    { id: 'lower', parent: 'upper', u: 267 / 452, v: 439 / 512 },
];

export function makeFarGeometry() {
    const geometry = new PlaneGeometry(SIZE.width, SIZE.height, GRID.columns, GRID.rows);
    const uv = geometry.getAttribute('uv'), indices = [], weights = [];
    for (let i = 0; i < uv.count; i++) {
        const t = clamp(((1 - uv.getY(i)) - .80) / .105, 0, 1), lower = t * t * (3 - 2 * t);
        indices.push(2, 3, 0, 0); weights.push(1 - lower, lower, 0, 0);
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    return geometry;
}

export const createFarSkin = material => bindSkin(material, makeFarGeometry(), BONES);

export function poseAt(time, action = 'swing', amount = .8, manual = {}) {
    if (!ACTIONS.includes(action)) throw new Error('Invalid far leg action');
    const t = Number.isFinite(time) ? Math.max(0, time) : 0, a = clamp(amount, 0, 1);
    const presets = {
        rest: [0, 0], forward: [-22, 8], tuck: [-16, 40],
        swing: [Math.sin(t * 2.2) * 22, (1 - Math.cos(t * 2.2)) * 16],
    };
    const [hip, knee] = presets[action];
    return { hip: clamp(hip * a + clamp(manual.hip, -25, 25), -30, 30) * Math.PI / 180,
        knee: clamp(knee * a + clamp(manual.knee, 0, 40), 0, 55) * Math.PI / 180 };
}

export function applyFarPose(skin, pose) {
    for (const bone of Object.values(skin.bones)) { bone.position.copy(bone.userData.rest); bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1); }
    skin.bones.upper.rotation.z = pose.hip; skin.bones.lower.rotation.z = pose.knee;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
