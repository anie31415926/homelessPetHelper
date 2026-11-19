import assert from 'node:assert/strict';
import test from 'node:test';
import { MeshBasicMaterial, Vector3 } from 'three';
import { applyLimbPose, createLimbSkin, limbPose, limbSpec, makeLimbGeometry } from '../src/pet-layers/limbs.js';

for (const version of ['ai', 'copy']) for (const kind of ['front', 'hind']) {
    test(`${version} ${kind} is a full continuous chain from body root to complete paw`, () => {
        const spec = limbSpec(kind, version), geometry = makeLimbGeometry(kind, version), weights = geometry.getAttribute('skinWeight');
        assert.deepEqual(spec.map(bone => bone.id), ['root', 'body', 'upper', 'lower', 'ankle', 'paw']);
        assert.ok(spec[2].v * 512 < 350); assert.ok(spec[5].v * 512 > 470);
        for (let i = 0; i < weights.count; i++) {
            const values = [weights.getX(i), weights.getY(i), weights.getZ(i), weights.getW(i)];
            assert.ok(values.every(v => Number.isFinite(v) && v >= 0));
            assert.ok(Math.abs(values.reduce((a, b) => a + b) - 1) < 1e-6);
        }
        geometry.dispose();
        const material = new MeshBasicMaterial(), skin = createLimbSkin(material, kind, version), point = new Vector3(), original = new Vector3();
        const position = skin.geometry.getAttribute('position');
        applyLimbPose(skin, limbPose(0, 'rest', 1, {}, kind));
        for (let i = 0; i < position.count; i++) {
            point.fromBufferAttribute(position, i); original.copy(point); skin.mesh.applyBoneTransform(i, point);
            assert.ok(point.distanceTo(original) < 1e-5);
        }
        assert.equal(skin.bones.paw.parent, skin.bones.ankle); assert.equal(skin.bones.ankle.parent, skin.bones.lower);
        skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose();
    });
}
test('front and rear have different anatomy and oppose their swing phases', () => {
    assert.notDeepEqual(limbSpec('front'), limbSpec('hind'));
    assert.notDeepEqual(limbSpec('front', 'copy'), limbSpec('hind', 'copy'));
    assert.notDeepEqual(limbSpec('front', 'copy'), limbSpec('front', 'ai'));
    for (const kind of ['front', 'hind']) for (const action of ['rest', 'forward', 'tuck', 'swing']) for (let i = 0; i < 100; i++) {
        const pose = limbPose(i / 10, action, 1, { hip: Infinity, knee: 999 }, kind);
        assert.ok(Object.values(pose).every(value => Number.isFinite(value) && Math.abs(value) < 1.2));
    }
    assert.ok(limbPose(.5, 'swing', 1, {}, 'front').upper * limbPose(.5, 'swing', 1, {}, 'hind').upper < 0);
    assert.deepEqual(limbPose(NaN), limbPose(0));
    assert.throws(() => limbSpec('missing')); assert.throws(() => limbPose(0, 'missing'));
    assert.throws(() => limbSpec('front', 'unknown'));
});
