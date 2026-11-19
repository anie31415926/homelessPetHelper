import assert from 'node:assert/strict';
import test from 'node:test';
import { MeshBasicMaterial, Vector3 } from 'three';
import { createSkin } from '../src/pet-mesh/skin.js';
import { ACTIONS, applyFarPose, createFarSkin, makeFarGeometry, poseAt } from '../src/pet-layers/skin.js';

test('far hind leg has its own upper/lower skeleton and normalized blend', () => {
    const geometry = makeFarGeometry(), weights = geometry.getAttribute('skinWeight');
    for (let i = 0; i < weights.count; i++) assert.ok(Math.abs(weights.getX(i) + weights.getY(i) - 1) < 1e-6);
    geometry.dispose();
    const material = new MeshBasicMaterial(), skin = createFarSkin(material);
    assert.equal(skin.bones.lower.parent, skin.bones.upper);
    assert.equal(skin.bones.upper.parent, skin.bones.body);
    skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose();
});
test('rest matches original coordinates and far motion cannot deform the foreground body', () => {
    const material = new MeshBasicMaterial(), far = createFarSkin(material), front = createSkin(material);
    const point = new Vector3(), original = new Vector3(), position = far.geometry.getAttribute('position');
    applyFarPose(far, poseAt(0, 'rest'));
    for (let i = 0; i < position.count; i++) {
        original.fromBufferAttribute(position, i); point.copy(original); far.mesh.applyBoneTransform(i, point);
        assert.ok(point.distanceTo(original) < 1e-5);
    }
    applyFarPose(far, poseAt(1.2, 'forward', 1));
    let moved = 0;
    for (let i = 0; i < position.count; i++) {
        original.fromBufferAttribute(position, i); point.copy(original); far.mesh.applyBoneTransform(i, point);
        if (point.distanceTo(original) > .1) moved++;
        point.fromBufferAttribute(front.geometry.getAttribute('position'), i); front.mesh.applyBoneTransform(i, point);
        assert.ok(point.distanceTo(original) < 1e-5);
    }
    assert.ok(moved > 50);
    for (const skin of [far, front]) { skin.geometry.dispose(); skin.skeleton.dispose(); } material.dispose();
});
test('far pose sampling stays finite, bounded and deterministic', () => {
    for (const action of ACTIONS) for (let i = 0; i < 100; i++) assert.ok(Object.values(poseAt(i / 10, action)).every(Number.isFinite));
    assert.deepEqual(poseAt(NaN), poseAt(0));
    assert.deepEqual(poseAt(2), poseAt(2));
    assert.ok(poseAt(2, 'tuck', 1, { hip: 999, knee: 999 }).knee <= 55 * Math.PI / 180);
    assert.throws(() => poseAt(0, 'missing'));
});
