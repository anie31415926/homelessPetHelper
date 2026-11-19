import assert from 'node:assert/strict';
import test from 'node:test';
import { MeshBasicMaterial, Vector3 } from 'three';
import { ACTIONS, applyPose, BONE_SPEC, createSkin, GRID, makeGeometry, samplePose, weightsAt } from '../src/pet-mesh/skin.js';

test('every mesh vertex has at most four normalized finite influences', () => {
    const geometry = makeGeometry(), weights = geometry.getAttribute('skinWeight'), indices = geometry.getAttribute('skinIndex');
    assert.equal(geometry.getAttribute('position').count, 2009);
    assert.equal(geometry.index.count / 3, 3840);
    for (let i = 0; i < weights.count; i++) {
        const w = [weights.getX(i), weights.getY(i), weights.getZ(i), weights.getW(i)];
        assert.ok(w.every(value => Number.isFinite(value) && value >= 0));
        assert.ok(Math.abs(w.reduce((sum, value) => sum + value, 0) - 1) < 1e-6);
        assert.ok(indices.getX(i) < BONE_SPEC.length);
    }
    geometry.dispose();
});

test('zero pose preserves the same original geometry, without repacking parts', () => {
    const material = new MeshBasicMaterial(), skin = createSkin(material), point = new Vector3(), original = new Vector3();
    applyPose(skin, samplePose(0, 'breathe', 0));
    for (let i = 0; i < skin.geometry.getAttribute('position').count; i++) {
        original.fromBufferAttribute(skin.geometry.getAttribute('position'), i);
        point.copy(original); skin.mesh.applyBoneTransform(i, point);
        assert.ok(point.distanceTo(original) < 1e-5);
    }
    skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose();
});

test('neck, ear root and tail root blend adjacent bones instead of rigid disconnected parts', () => {
    assert.ok(weightsAt(.32, .57).length > 1);
    assert.ok(weightsAt(.365, .25).length > 1);
    assert.ok(weightsAt(.825, .7).length > 1);
    const geometry = makeGeometry(), edges = new Map();
    for (let i = 0; i < geometry.index.count; i += 3) {
        const ids = [geometry.index.getX(i), geometry.index.getX(i + 1), geometry.index.getX(i + 2)];
        for (let j = 0; j < 3; j++) {
            const key = [ids[j], ids[(j + 1) % 3]].sort((a, b) => a - b).join(',');
            edges.set(key, (edges.get(key) || 0) + 1);
        }
    }
    assert.ok([...edges.values()].every(count => count === 1 || count === 2));
    assert.equal([...edges.values()].filter(count => count === 1).length, 2 * (GRID.columns + GRID.rows), 'no internal cut edges');
    geometry.dispose();
});

test('each local control actually deforms vertices and ear bones inherit the head', () => {
    const material = new MeshBasicMaterial(), skin = createSkin(material), point = new Vector3(), original = new Vector3();
    assert.equal(skin.bones.earNear.parent, skin.bones.head);
    for (const [joint, value] of Object.entries({ head: 10, ears: 18, tail: 20, paw: 100 })) {
        applyPose(skin, samplePose(0, 'breathe', 0, { [joint]: value }));
        let moved = 0;
        for (let i = 0; i < skin.geometry.getAttribute('position').count; i++) {
            original.fromBufferAttribute(skin.geometry.getAttribute('position'), i); point.copy(original);
            skin.mesh.applyBoneTransform(i, point);
            assert.ok([point.x, point.y, point.z].every(Number.isFinite));
            if (point.distanceTo(original) > .1) moved++;
        }
        assert.ok(moved > 30, `${joint} moves actual vertices`);
    }
    skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose();
});

test('pose sampling is bounded, deterministic, and safely clamps manual input', () => {
    for (const action of ACTIONS) for (let i = 0; i < 80; i++) assert.ok(Object.values(samplePose(i / 10, action)).every(Number.isFinite));
    assert.deepEqual(samplePose(NaN), samplePose(0));
    assert.deepEqual(samplePose(2, 'tail'), samplePose(2, 'tail'));
    assert.equal(samplePose(0, 'breathe', 0, { paw: 999 }).paw, 1);
    assert.throws(() => samplePose(0, 'missing'));
    assert.throws(() => weightsAt(NaN, 0));
});
