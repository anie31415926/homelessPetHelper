import assert from 'node:assert/strict';
import test from 'node:test';
import {createSitRig, KEY_POSES, LENGTHS, samplePose} from '../src/pet-sit-study/rig.js';

test('five key poses and explicit finite input bounds', () => {
    assert.equal(KEY_POSES.length, 5);
    for (const value of [NaN, Infinity, -.001, 1.001]) assert.throws(() => samplePose(value), RangeError);
    assert.equal(samplePose(0).name, '站立'); assert.equal(samplePose(1).name, '坐稳');
});

test('all 501 samples retain bone lengths, unit scales and reach ground targets', () => {
    const rig = createSitRig();
    try {
        for (let i = 0; i <= 500; i++) {
            rig.apply(i / 500);
            for (const {from, to, length} of rig.segments) assert.ok(Math.abs(rig.world(from).distanceTo(rig.world(to)) - length) < 1e-10, `${i}: ${from} -> ${to}`);
            for (const bone of rig.bones) assert.deepEqual(bone.scale.toArray(), [1, 1, 1]);
            assert.ok(Math.min(rig.world('nearFrontToe').y, rig.world('farFrontToe').y) < .0755, `${i}: both forepaws airborne`);
            for (const side of ['near', 'far']) {
                for (const name of ['Hock', 'Wrist']) assert.ok(rig.world(side + name).distanceTo(rig.world(side + name + 'Target')) < .0005, `${i}: ${side}${name}`);
                for (const name of ['Toe', 'FrontToe', 'Hock', 'Wrist', 'Knee', 'Elbow']) assert.ok(rig.world(side + name).y >= .0745, `${i}: below ground ${side}${name}`);
                assert.ok(Math.abs(rig.world(side + 'Toe').y - .075) < .0005);
                if (i >= 100) assert.ok(Math.abs(rig.world(side + 'FrontToe').y - .075) < .0005);
            }
        }
    } finally {rig.dispose();}
});

test('seated hind chain folds forward/up then backward/down with low hock and forward metatarsus', () => {
    const rig = createSitRig();
    try {
        rig.apply(1);
        for (const side of ['near', 'far']) {
            const hip = rig.world(side + 'Hip'), knee = rig.world(side + 'Knee'), hock = rig.world(side + 'Hock'), toe = rig.world(side + 'Toe');
            assert.ok(knee.x > hip.x + .5 && knee.y > hip.y + .4);
            assert.ok(hock.x < knee.x && hock.y < knee.y && hock.y < .17);
            assert.ok(toe.x > hock.x + .75 && Math.abs(hock.distanceTo(toe) - LENGTHS.metatarsal) < 1e-10);
            assert.ok(rig.world(side + 'Shoulder').y > hip.y + 1.4);
        }
    } finally {rig.dispose();}
});

test('pelvis descends while spine/chest reorganize and forepaws make a preparation step', () => {
    const rig = createSitRig();
    try {
        rig.apply(0); const start = rig.world('root'), chest = rig.world('spine5'), front = rig.world('nearFrontToe');
        rig.apply(.05); assert.ok(rig.world('nearFrontToe').y > front.y + .09);
        rig.apply(.2); assert.ok(rig.world('nearFrontToe').x < front.x - .2);
        rig.apply(1); assert.ok(rig.world('root').y < start.y - 1.6); assert.ok(rig.world('spine5').x < chest.x - .9);
        assert.ok(rig.world('spine5').y > chest.y - .15);
    } finally {rig.dispose();}
});

test('random seeks are deterministic with no accumulating IK state', () => {
    const rig = createSitRig();
    const snapshot = p => {rig.apply(p); return rig.bones.map(bone => bone.matrixWorld.toArray());};
    try {
        for (const at of [0, .035, .2, .333, .55, .86, .972, 1]) {
            const expected = snapshot(at); snapshot(1 - at); snapshot(.71); assert.deepEqual(snapshot(at), expected);
        }
    } finally {rig.dispose();}
});

test('continuous path has no branch flipping or key-pose jump', () => {
    const rig = createSitRig();
    try {
        let last;
        for (let i = 0; i <= 1000; i++) {
            rig.apply(i / 1000);
            const points = ['nearHip', 'nearKnee', 'nearHock', 'nearToe', 'nearShoulder', 'nearElbow', 'nearWrist', 'skull'].map(rig.world);
            if (last) points.forEach((point, index) => assert.ok(point.distanceTo(last[index]) < .016, `jump at ${i}: ${index}`));
            last = points;
        }
    } finally {rig.dispose();}
});
