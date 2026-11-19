import assert from 'node:assert/strict';
import test from 'node:test';
import { ACTIONS, createRig, PARTS, poseAt, PROPORTIONS } from '../src/pet-rig/rig.js';

test('front and side share the semantic skeleton, with view-specific pivots', () => {
    for (const view of ['front', 'side']) {
        const nodes = createRig(view);
        assert.deepEqual(nodes.map(n => n.id).sort(), [...PARTS].sort());
        assert.equal(new Set(nodes.map(n => n.id)).size, 9);
        const available = new Set(['root']);
        for (const node of nodes) { assert.ok(available.has(node.parent)); available.add(node.id); }
    }
    assert.notEqual(createRig('front')[0].x, createRig('side')[0].x);
});
test('all actions and proportion presets stay finite across a full cycle', () => {
    for (const action of ACTIONS) for (const preset of Object.keys(PROPORTIONS)) {
        for (let i = 0; i < 120; i++) {
            assert.ok(Object.values(poseAt(i / 12, action)).every(Number.isFinite));
            assert.ok(createRig('side', preset).every(n => n.width > 0 && n.height > 0));
        }
    }
});
test('gait alternates paws, ears are children of head, and only local joints change manually', () => {
    const pose = poseAt(.2, 'walk');
    assert.ok(pose.frontL * pose.frontR < 0);
    assert.ok(pose.backL * pose.backR < 0);
    assert.ok(pose.frontL * pose.backL < 0);
    assert.equal(createRig('front').find(n => n.id === 'earL').parent, 'head');
    const before = poseAt(0), after = poseAt(0, 'idle', { ears: 99, head: -99, tail: NaN });
    assert.equal(after.earL - before.earL, 22);
    assert.equal(after.head - before.head, -12);
    assert.equal(after.tail, before.tail);
    assert.equal(after.frontL, before.frontL);
});
test('sampling is deterministic, independent of wall clock, and rejects bad identifiers', () => {
    assert.deepEqual(poseAt(3, 'listen'), poseAt(3, 'listen'));
    assert.deepEqual(poseAt(NaN), poseAt(0));
    assert.throws(() => createRig('back'));
    assert.throws(() => createRig('front', 'unknown'));
    assert.throws(() => poseAt(0, 'unknown'));
});
