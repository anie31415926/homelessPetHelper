import { BufferAttribute, PlaneGeometry } from 'three';
import { bindSkin, clamp, GRID, SIZE } from '../pet-mesh/skin.js';

export const LIMBS = {
    front: { root: [127, 337], elbow: [111, 410], ankle: [119, 455], paw: [109, 477], blends: [395, 423, 442, 465, 470, 486] },
    hind: { root: [307, 341], elbow: [253, 426], ankle: [283, 453], paw: [258, 477], blends: [411, 439, 446, 463, 470, 486] },
};
export const COPY_LIMBS = {
    front: { root: [122, 340], elbow: [129, 403], ankle: [111, 451], paw: [111, 474], blends: [388, 418, 437, 461, 465, 487] },
    hind: { root: [277, 340], elbow: [236, 414], ankle: [259, 448], paw: [264, 474], blends: [397, 426, 435, 460, 465, 487] },
};
function definitionFor(kind, version) {
    const definitions = { ai: LIMBS, copy: COPY_LIMBS }[version];
    if (!definitions?.[kind]) throw new Error('Unknown limb or version');
    return definitions[kind];
}
const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };

export function limbSpec(kind, version = 'ai') {
    const definition = definitionFor(kind, version);
    return [
        { id: 'root', parent: null, u: .5, v: .5 },
        { id: 'body', parent: 'root', u: .56, v: .67 },
        ...['upper', 'lower', 'ankle', 'paw'].map((id, index) => {
            const position = definition[['root', 'elbow', 'ankle', 'paw'][index]];
            return { id, parent: ['body', 'upper', 'lower', 'ankle'][index], u: position[0] / 452, v: position[1] / 512 };
        }),
    ];
}

export function makeLimbGeometry(kind, version = 'ai', offsetY = 0) {
    const definition = definitionFor(kind, version);
    const geometry = new PlaneGeometry(SIZE.width, SIZE.height, GRID.columns, GRID.rows);
    const uv = geometry.getAttribute('uv'), indices = [], weights = [], [l1, l2, a1, a2, p1, p2] = definition.blends;
    for (let i = 0; i < uv.count; i++) {
        const y = (1 - uv.getY(i)) * 512 - offsetY, lower = smooth(l1, l2, y), ankle = smooth(a1, a2, y), paw = smooth(p1, p2, y);
        indices.push(2, 3, 4, 5);
        weights.push((1 - lower) * (1 - ankle) * (1 - paw), lower * (1 - ankle) * (1 - paw), ankle * (1 - paw), paw);
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    return geometry;
}
export const createLimbSkin = (material, kind, version = 'ai') => bindSkin(material, makeLimbGeometry(kind, version), limbSpec(kind, version));

export function limbPose(time, action = 'swing', amount = .8, manual = {}, kind = 'front') {
    if (!LIMBS[kind]) throw new Error('Unknown limb');
    if (!['rest', 'forward', 'tuck', 'swing'].includes(action)) throw new Error('Unknown limb action');
    const t = Number.isFinite(time) ? Math.max(0, time) : 0, a = clamp(amount, 0, 1), front = kind === 'front';
    const phase = t * 2.2 + (front ? 0 : Math.PI);
    const preset = { rest: [0, 0], forward: front ? [-20, 14] : [-15, 20], tuck: front ? [-14, 46] : [-12, 40],
        swing: [Math.sin(phase) * (front ? 18 : 14), (1 - Math.cos(phase)) * (front ? 10 : 14)] }[action];
    const upper = clamp(preset[0] * a + clamp(manual.hip, -25, 25), -32, 32);
    const lower = clamp(preset[1] * a + clamp(manual.knee, 0, 40), 0, 65), rad = Math.PI / 180;
    return { upper: upper * rad, lower: lower * rad, ankle: clamp(-lower * .6 - upper * .35, -45, 25) * rad, paw: -lower * .2 * rad };
}

export function applyLimbPose(skin, pose) {
    for (const bone of Object.values(skin.bones)) { bone.position.copy(bone.userData.rest); bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1); }
    for (const id of ['upper', 'lower', 'ankle', 'paw']) skin.bones[id].rotation.z = pose[id];
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
