import assert from 'node:assert/strict';
import test from 'node:test';
import { MeshBasicMaterial, Vector3 } from 'three';
import { applyMotionBody, applyMotionLimb, createMotionBody, createMotionLimb, FEET, footStep, MOTIONS, motionLimbSpec, sampleMotion, STEP_PERIOD, UNIT } from '../src/pet-layers/motion.js';

test('all motion samples are deterministic, bounded and zero amplitude stays still', () => {
    for (const action of Object.keys(MOTIONS)) for (let i = 0; i < 200; i++) {
        const pose = sampleMotion(i / 17, action, 1);
        assert.ok([pose.x, pose.down, pose.head, pose.ears, pose.tail, ...Object.values(pose.feet).flatMap(foot => [foot.dx, foot.lift])].every(Number.isFinite));
        assert.ok(Math.abs(pose.x) <= 65 && pose.down >= 0 && pose.down <= 58);
        assert.deepEqual(pose, sampleMotion(i / 17, action, 1));
        const zero = sampleMotion(i / 17, action, 0);
        assert.equal(zero.down, 0); assert.equal(zero.x, 0);
        for (const foot of Object.values(zero.feet)) assert.ok(!foot.dx && !foot.lift);
    }
    assert.deepEqual(sampleMotion(NaN), sampleMotion(0)); assert.throws(() => sampleMotion(0, 'unknown')); assert.throws(() => sampleMotion(0, 'toString'));
});
test('walk has four staggered feet, a planted support phase, and a closed path with turn pauses', () => {
    for (let i = 0; i < 200; i++) {
        const time = i / 200 * STEP_PERIOD;
        const feet = FEET.map(foot => footStep(time, foot.phase));
        assert.ok(feet.filter(foot => foot.contact).length >= 2);
        for (const foot of feet) assert.ok(foot.contact ? foot.lift === 0 : foot.lift > 0);
    }
    const start = sampleMotion(.21, 'walk', 1), middle = sampleMotion(MOTIONS.walk * .6, 'walk', 1), end = sampleMotion(.21 + MOTIONS.walk, 'walk', 1);
    assert.ok(Math.abs(start.x - end.x) < 1e-10); assert.equal(start.mirror, end.mirror); assert.notEqual(start.mirror, middle.mirror);
    assert.equal(sampleMotion(.1, 'walk', 1).x, sampleMotion(.15, 'walk', 1).x);
    const a = sampleMotion(1.1, 'walk', 1), b = sampleMotion(1.101, 'walk', 1);
    for (const foot of FEET) if (a.feet[foot.id].contact && b.feet[foot.id].contact) {
        assert.ok(Math.abs(a.x + a.feet[foot.id].dx - b.x - b.feet[foot.id].dx) < 1e-8, 'planted ankle target is fixed in world space during steady travel');
    }
});
test('lying down holds a lower body, returns to standing, and keeps the supporting feet planted', () => {
    assert.equal(sampleMotion(0, 'lie', 1).down, 0); assert.equal(sampleMotion(2.2, 'lie', 1).down, 58);
    assert.equal(sampleMotion(4.7, 'lie', 1).down, 0);
    for (const foot of Object.values(sampleMotion(2.2, 'lie', 1).feet)) assert.equal(foot.lift, 0);
});
test('lying far hind retracts under the abdomen and extends continuously on rising without changing other feet', () => {
    const standing = sampleMotion(.4, 'lie', 1), lying = sampleMotion(2.2, 'lie', 1), risen = sampleMotion(4.7, 'lie', 1);
    assert.equal(standing.feet.farHind.retract, 0); assert.equal(risen.feet.farHind.retract, 0);
    assert.equal(lying.feet.farHind.retract, 28); assert.equal(lying.feet.farHind.contact, false);
    assert.equal(lying.feet.farHind.dx - lying.feet.nearHind.dx, 31);
    for (const id of ['nearFront', 'farFront', 'nearHind']) { assert.equal(lying.feet[id].retract, 0); assert.equal(lying.feet[id].contact, true); }
    for (const time of [.4, .8, 1.2, 1.6, 3.2, 3.8, 4.5]) {
        assert.ok(Math.abs(sampleMotion(time, 'lie', 1).feet.farHind.retract - sampleMotion(time + .0001, 'lie', 1).feet.farHind.retract) < .02);
    }
});
for (const foot of FEET) test(`${foot.id}: source-specific continuous mesh and official CCD solve meet the ankle target`, () => {
    const material = new MeshBasicMaterial(), skin = createMotionLimb(material, foot.kind, foot.side);
    const ankle = new Vector3(), target = new Vector3();
    try {
        const spec = motionLimbSpec(foot.kind, foot.side); assert.equal(spec.length, 7); assert.equal(spec[6].id, 'target');
        const weights = skin.geometry.getAttribute('skinWeight');
        for (let i = 0; i < weights.count; i++) assert.ok(Math.abs(weights.getX(i) + weights.getY(i) + weights.getZ(i) + weights.getW(i) - 1) < 1e-6);
        let maxError = 0;
        for (const action of Object.keys(MOTIONS)) for (const time of [0, .3, .8, 1.2, 2.1, 3.6, 4.3]) {
            const pose = sampleMotion(time, action, 1); applyMotionLimb(skin, pose, pose.feet[foot.id], foot.kind);
            skin.bones.ankle.getWorldPosition(ankle); skin.bones.target.getWorldPosition(target);
            maxError = Math.max(maxError, ankle.distanceTo(target) / UNIT);
            assert.ok(Math.abs(skin.bones.upper.rotation.z + skin.bones.lower.rotation.z + skin.bones.ankle.rotation.z) < 1e-8);
        }
        assert.ok(maxError < 2.5, `max ankle target error ${maxError.toFixed(3)}px`);
    } finally { skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose(); }
});
test('body skin retains head, ears and tail on the same image without leg influences', () => {
    const material = new MeshBasicMaterial(), skin = createMotionBody(material);
    try {
        const indices = skin.geometry.getAttribute('skinIndex'); assert.equal(skin.skeleton.bones.length, 8);
        assert.ok([...indices.array].every(index => index < 8));
        applyMotionBody(skin, sampleMotion(2, 'look', 1)); assert.notEqual(skin.bones.head.rotation.z, 0);
        assert.notEqual(skin.bones.tailMid.rotation.z, 0);
    } finally { skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose(); }
});
