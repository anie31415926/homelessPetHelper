import { Bone, BufferAttribute, PlaneGeometry, Skeleton, SkinnedMesh, Vector3 } from 'three';

export const SIZE = { width: 460 * 452 / 512, height: 460 };
export const GRID = { columns: 40, rows: 48 };
export const ACTIONS = ['together', 'breathe', 'head', 'ears', 'tail', 'paw'];
export const clamp = (value, min, max) => Math.max(min, Math.min(max, Number.isFinite(value) ? value : 0));
const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
export const BONE_SPEC = [
    { id: 'root', parent: null, u: .5, v: .5 },
    { id: 'body', parent: 'root', u: .56, v: .67 },
    { id: 'head', parent: 'body', u: .32, v: .57 },
    { id: 'earFar', parent: 'head', u: .215, v: .175 },
    { id: 'earNear', parent: 'head', u: .365, v: .245 },
    { id: 'tailBase', parent: 'body', u: .825, v: .68 },
    { id: 'tailMid', parent: 'tailBase', u: .855, v: .825 },
    { id: 'tailTip', parent: 'tailMid', u: .93, v: .915 },
    { id: 'frontUpper', parent: 'body', u: .285, v: .67 },
    { id: 'frontLower', parent: 'frontUpper', u: .30, v: .835 },
];
const boneIndex = Object.fromEntries(BONE_SPEC.map((bone, i) => [bone.id, i]));
const mix = (base, added, amount) => {
    const result = Object.fromEntries(Object.entries(base).map(([id, weight]) => [id, weight * (1 - amount)]));
    for (const [id, weight] of Object.entries(added)) result[id] = (result[id] || 0) + weight * amount;
    return result;
};

export function weightsAt(u, v) {
    if (!Number.isFinite(u) || !Number.isFinite(v)) throw new Error('Invalid UV');
    const body = 1 - smooth(.88, .98, v);
    let weights = { root: 1 - body, body };
    const head = (1 - smooth(.49, .635, v)) * (1 - smooth(.49, .61, u));
    weights = mix(weights, { head: 1 }, head);
    const far = (1 - smooth(.15, .23, v)) * (1 - smooth(.22, .31, u));
    const near = (1 - smooth(.215, .315, v)) * smooth(.24, .33, u);
    weights = mix(weights, { earFar: 1 }, far * head);
    weights = mix(weights, { earNear: 1 }, near * head);
    const tail = smooth(.77, .86, u) * smooth(.61, .72, v);
    const mid = smooth(.73, .865, v), tip = smooth(.855, .96, v);
    weights = mix(weights, { tailBase: 1 - mid, tailMid: mid * (1 - tip), tailTip: mid * tip }, tail);
    const leg = smooth(.66, .81, v) * smooth(.13, .21, u) * (1 - smooth(.37, .45, u));
    const lower = smooth(.785, .905, v);
    weights = mix(weights, { frontUpper: 1 - lower, frontLower: lower }, leg);
    const entries = Object.entries(weights).filter(([, w]) => w > 1e-7).sort((a, b) => b[1] - a[1]).slice(0, 4);
    const sum = entries.reduce((total, [, w]) => total + w, 0);
    return entries.map(([id, weight]) => ({ index: boneIndex[id], weight: weight / sum }));
}

export function makeGeometry() {
    const geometry = new PlaneGeometry(SIZE.width, SIZE.height, GRID.columns, GRID.rows);
    const uv = geometry.getAttribute('uv'), indices = [], weights = [];
    for (let i = 0; i < uv.count; i++) {
        const influences = weightsAt(uv.getX(i), 1 - uv.getY(i));
        for (let j = 0; j < 4; j++) { indices.push(influences[j]?.index || 0); weights.push(influences[j]?.weight || 0); }
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    return geometry;
}

export function createSkin(material) {
    return bindSkin(material, makeGeometry(), BONE_SPEC);
}

export function bindSkin(material, geometry, spec) {
    const mesh = new SkinnedMesh(geometry, material), bones = {};
    const point = (u, v) => new Vector3((u - .5) * SIZE.width, (.5 - v) * SIZE.height, 0);
    for (const definition of spec) {
        const bone = new Bone(); bone.name = definition.id;
        const rest = point(definition.u, definition.v);
        if (definition.parent) {
            const parentSpec = spec.find(item => item.id === definition.parent);
            if (!parentSpec || !bones[definition.parent]) throw new Error('Bone parents must precede children');
            bone.position.copy(rest).sub(point(parentSpec.u, parentSpec.v));
            bones[definition.parent].add(bone);
        } else { bone.position.copy(rest); mesh.add(bone); }
        bone.userData.rest = bone.position.clone(); bones[definition.id] = bone;
    }
    mesh.updateMatrixWorld(true);
    const skeleton = new Skeleton(spec.map(definition => bones[definition.id]));
    mesh.bind(skeleton); mesh.frustumCulled = false;
    return { mesh, bones, skeleton, geometry };
}

export function samplePose(time, action = 'together', amount = .7, manual = {}) {
    if (!ACTIONS.includes(action)) throw new Error('Invalid mesh action');
    const t = Math.max(0, Number.isFinite(time) ? time : 0), a = clamp(amount, 0, 1);
    const active = part => action === part || (action === 'together' && part !== 'paw');
    const rad = Math.PI / 180;
    return {
        breathe: active('breathe') ? Math.sin(t * 2.1) * .013 * a : 0,
        head: ((active('head') ? Math.sin(t * 1.6) * 6 * a : 0) + clamp(manual.head, -10, 10)) * rad,
        earNear: ((active('ears') ? Math.sin(t * 3.1) * 13 * a : 0) + clamp(manual.ears, -18, 18)) * rad,
        earFar: ((active('ears') ? -Math.sin(t * 3.1 + .6) * 9 * a : 0) - clamp(manual.ears, -18, 18) * .65) * rad,
        tail: ((active('tail') ? Math.sin(t * 2.2) * 11 * a : 0) + clamp(manual.tail, -20, 20)) * rad,
        paw: clamp((active('paw') ? (1 - Math.cos(t * 2.3)) * .5 * a : 0) + clamp(manual.paw, 0, 100) / 100, 0, 1),
    };
}

export function applyPose(skin, pose) {
    const { bones, mesh, skeleton } = skin;
    for (const bone of Object.values(bones)) { bone.position.copy(bone.userData.rest); bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1); }
    bones.body.scale.set(1 + pose.breathe * .45, 1 + pose.breathe, 1);
    bones.head.rotation.z = pose.head;
    bones.earNear.rotation.z = pose.earNear; bones.earFar.rotation.z = pose.earFar;
    bones.tailBase.rotation.z = pose.tail * .55; bones.tailMid.rotation.z = pose.tail; bones.tailTip.rotation.z = pose.tail * .8;
    bones.frontUpper.rotation.z = -pose.paw * .43; bones.frontLower.rotation.z = pose.paw * .78;
    bones.frontUpper.position.y += pose.paw * 6;
    mesh.updateMatrixWorld(true); skeleton.update();
}
