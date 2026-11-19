import assert from 'node:assert/strict';
import test from 'node:test';
import { MeshBasicMaterial, Vector3 } from 'three';
import { GRID, SIZE } from '../src/pet-mesh/skin.js';
import { applyLyingSkin, createLyingSkin, lyingWeightsAt, LYING_SPEC } from '../src/pet-layers/lying.js';
import { sampleMotion, UNIT } from '../src/pet-layers/motion.js';

function withSkin(check) {
    const material = new MeshBasicMaterial(), skin = createLyingSkin(material);
    try { check(skin); } finally { skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose(); }
}
const vertex = (skin, index) => skin.mesh.applyBoneTransform(index, new Vector3().fromBufferAttribute(skin.geometry.getAttribute('position'), index));

test('lying body/hip/hind uses one indexed surface with no internal split or duplicated seam vertices', () => withSkin(skin => {
    const positions = skin.geometry.getAttribute('position'), edges = new Map();
    assert.equal(positions.count, (GRID.columns + 1) * (GRID.rows + 1));
    const triangles = skin.geometry.index.array;
    for (let i = 0; i < triangles.length; i += 3) for (let j = 0; j < 3; j++) {
        const a = triangles[i + j], b = triangles[i + (j + 1) % 3], key = [Math.min(a, b), Math.max(a, b)].join(':');
        edges.set(key, (edges.get(key) || 0) + 1);
    }
    for (const [key, count] of edges) {
        assert.ok(count === 1 || count === 2);
        if (count === 2) continue;
        const [a, b] = key.split(':').map(Number);
        const outerX = Math.abs(positions.getX(a)) === SIZE.width / 2 && positions.getX(a) === positions.getX(b);
        const outerY = Math.abs(positions.getY(a)) === SIZE.height / 2 && positions.getY(a) === positions.getY(b);
        assert.ok(outerX || outerY, `internal open edge ${key}`);
    }
    const weights = lyingWeightsAt(290, 415);
    assert.ok(weights.some(item => item.index === 5 && item.weight > .1), 'pelvis shared across hip');
    assert.ok(weights.some(item => item.index === 9 && item.weight > .1), 'same hip surface also blends toward the planted foot');
    assert.equal(skin.bones.upper.parent, skin.bones.pelvis);
    assert.equal(skin.bones.tailBase.parent, skin.bones.pelvis);
    const ids = skin.geometry.getAttribute('skinIndex'), amounts = skin.geometry.getAttribute('skinWeight');
    for (let i = 0; i < ids.count; i++) {
        let sum = 0;
        for (let j = 0; j < 4; j++) { assert.ok(ids.array[i * 4 + j] < LYING_SPEC.length); sum += amounts.array[i * 4 + j]; }
        assert.ok(Math.abs(sum - 1) < 1e-6);
    }
}));

test('red-line abdomen follows the torso while the outer croup stays continuously blended to the foot', () => withSkin(skin => {
    const belly = lyingWeightsAt(215, 430), croup = lyingWeightsAt(355, 400);
    assert.ok((belly.find(item => item.index === 9)?.weight || 0) < .1, 'folded leg must not hoist the belly edge');
    assert.ok(croup.some(item => item.index === 5 && item.weight > .5), 'outer butt remains pelvis-led');
    const positions = skin.geometry.getAttribute('position');
    const index = Array.from({ length: positions.count }, (_, i) => i).find(i => {
        const x = positions.getX(i) / UNIT + 226, y = 256 - positions.getY(i) / UNIT;
        return Math.abs(x - 203.4) < .1 && Math.abs(y - 426.667) < .1;
    });
    applyLyingSkin(skin, sampleMotion(2.2, 'lie', 1));
    const local = vertex(skin, index), originalY = positions.getY(index);
    const descent = 58 - (local.y - originalY) / UNIT;
    assert.ok(descent > 54 && descent < 62, `belly descends with torso: ${descent}px`);
}));

test('connected hind reaches the planted ankle throughout descent, hold and rise at all amplitudes', () => withSkin(skin => {
    let maximumError = 0;
    for (const amount of [0, .2, .5, .8, 1]) for (let i = 0; i <= 104; i++) {
        const pose = sampleMotion(i / 20, 'lie', amount); applyLyingSkin(skin, pose);
        const ankle = skin.bones.ankle.getWorldPosition(new Vector3()), target = skin.bones.target.getWorldPosition(new Vector3());
        maximumError = Math.max(maximumError, ankle.distanceTo(target) / UNIT);
        assert.ok(Math.abs(skin.bones.pelvis.rotation.z + skin.bones.upper.rotation.z + skin.bones.lower.rotation.z + skin.bones.ankle.rotation.z) < 1e-8);
    }
    assert.ok(maximumError < 2.5, `ankle residual ${maximumError.toFixed(3)}px`);
}));

test('lying tail core is rigid, follows the pelvis and only pivots at its root without any scaling', () => withSkin(skin => {
    const positions = skin.geometry.getAttribute('position'), ids = skin.geometry.getAttribute('skinIndex'), weights = skin.geometry.getAttribute('skinWeight');
    const core = [];
    for (let i = 0; i < positions.count; i++) if (ids.getX(i) === 6 && weights.getX(i) === 1) core.push(i);
    assert.ok(core.length > 20); assert.equal(skin.bones.tailMid, undefined); assert.equal(skin.bones.tailTip, undefined);
    const restDistance = new Vector3().fromBufferAttribute(positions, core[0]).distanceTo(new Vector3().fromBufferAttribute(positions, core.at(-1)));
    for (const amount of [0, .5, 1]) for (const time of [0, .4, .7, 1, 1.6, 2.2, 3.8, 4.5, 4.9]) {
        const pose = sampleMotion(time, 'lie', amount); applyLyingSkin(skin, pose);
        for (const bone of skin.skeleton.bones) assert.deepEqual(bone.scale.toArray(), [1, 1, 1]);
        assert.ok(Math.abs(vertex(skin, core[0]).distanceTo(vertex(skin, core.at(-1))) - restDistance) < 1e-5);
        const base = skin.bones.tailBase.getWorldPosition(new Vector3()), pelvis = skin.bones.pelvis.getWorldPosition(new Vector3());
        assert.ok(Math.abs(base.distanceTo(pelvis) - skin.bones.tailBase.userData.rest.length()) < 1e-8);
    }
}));

test('lying deformation is finite and continuous through the pelvis/leg blend, including arbitrary seeks', () => withSkin(skin => {
    const positions = skin.geometry.getAttribute('position'), hip = [];
    for (let i = 0; i < positions.count; i++) {
        const x = positions.getX(i) / UNIT + 226, y = 256 - positions.getY(i) / UNIT;
        if (x > 240 && x < 370 && y > 330 && y < 430) hip.push(i);
    }
    for (const time of [.4, .8, 1.2, 1.6, 3.2, 3.8, 4.5]) {
        applyLyingSkin(skin, sampleMotion(time, 'lie', 1)); const before = hip.map(index => vertex(skin, index));
        applyLyingSkin(skin, sampleMotion(time + .0001, 'lie', 1));
        for (const [i, index] of hip.entries()) {
            const after = vertex(skin, index); assert.ok(after.toArray().every(Number.isFinite));
            assert.ok(after.distanceTo(before[i]) / UNIT < .1, 'no discontinuous root motion');
        }
        applyLyingSkin(skin, sampleMotion(2.4, 'lie', 1));
        applyLyingSkin(skin, sampleMotion(time, 'lie', 1));
        for (const [i, index] of hip.entries()) assert.ok(vertex(skin, index).distanceTo(before[i]) < 1e-8);
    }
}));
