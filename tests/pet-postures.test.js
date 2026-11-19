import assert from 'node:assert/strict';
import test from 'node:test';
import { MeshBasicMaterial, Vector3 } from 'three';
import { applyPostureBody, applyPostureFarHind, applyPostureFront, createPostureBody, postureWeightsAt, POSTURE_SPEC } from '../src/pet-layers/postures.js';
import { applySideRest, createSideRest, sideWeightsAt, SIDE_SPEC } from '../src/pet-layers/side-rest.js';
import { applySitRest, createSitRest, sitWeightsAt, SIT_SPEC } from '../src/pet-layers/sit-rest.js';
import { createMotionLimb, MOTIONS, sampleMotion, UNIT } from '../src/pet-layers/motion.js';

const dispose = skin => { skin.geometry.dispose(); skin.skeleton.dispose(); skin.mesh.material.dispose(); };
const vertex = (skin, x, y) => {
    const uv = skin.geometry.getAttribute('uv'); let best = 0, distance = Infinity;
    for (let i = 0; i < uv.count; i++) {
        const d = (uv.getX(i) * 452 - x) ** 2 + ((1 - uv.getY(i)) * 512 - y) ** 2;
        if (d < distance) { distance = d; best = i; }
    }
    return skin.mesh.getVertexPosition(best, new Vector3());
};
for (const [name, weightsAt, spec, create, apply, points] of [
    ['sitMesh', postureWeightsAt, POSTURE_SPEC, createPostureBody, applyPostureBody, [[407, 480], [430, 460]]],
    ['sit', sitWeightsAt, SIT_SPEC, createSitRest, applySitRest, [[365, 460], [420, 440]]],
    ['side', sideWeightsAt, SIDE_SPEC, createSideRest, applySideRest, [[407, 480], [430, 460]]],
]) {
    test(`${name}: continuous indexed source mesh has finite normalized max-four weights`, () => {
        const skin = create(new MeshBasicMaterial());
        try {
            assert.equal(skin.geometry.getAttribute('position').count, 2009); assert.equal(skin.geometry.index.count, 11520);
            assert.equal(skin.skeleton.bones.length, spec.length);
            for (let x = 0; x <= 452; x += 7) for (let y = 0; y <= 512; y += 7) {
                const weights = weightsAt(x, y); assert.ok(weights.length <= 4 && weights.length > 0);
                assert.ok(Math.abs(weights.reduce((sum, w) => sum + w.weight, 0) - 1) < 1e-10);
                assert.ok(weights.every(w => w.weight >= 0 && w.weight <= 1 && w.index >= 0 && w.index < spec.length));
            }
            assert.throws(() => weightsAt(NaN, 0)); assert.throws(() => weightsAt(0, Infinity));
        } finally { dispose(skin); }
    });
    test(`${name}: zero amount retains source geometry and arbitrary scrub is deterministic`, () => {
        const skin = create(new MeshBasicMaterial());
        try {
            const position = skin.geometry.getAttribute('position');
            for (const time of [0, .8, 2.5, MOTIONS[name] - .1]) {
                apply(skin, sampleMotion(time, name, 0));
                for (let i = 0; i < position.count; i += 11) assert.ok(skin.mesh.getVertexPosition(i, new Vector3()).distanceTo(new Vector3().fromBufferAttribute(position, i)) < 1e-5);
            }
            apply(skin, sampleMotion(2.5, name, 1)); const expected = vertex(skin, 240, 380);
            for (const t of [6, .3, 4, 1.2]) apply(skin, sampleMotion(t, name, 1));
            apply(skin, sampleMotion(2.5, name, 1)); assert.ok(vertex(skin, 240, 380).distanceTo(expected) < 1e-10);
        } finally { dispose(skin); }
    });
    test(`${name}: tail core remains rigid and every bone scale stays one`, () => {
        const skin = create(new MeshBasicMaterial());
        try {
            apply(skin, sampleMotion(0, name, 0)); const distance = vertex(skin, ...points[0]).distanceTo(vertex(skin, ...points[1]));
            for (let t = 0; t < MOTIONS[name]; t += .07) {
                apply(skin, sampleMotion(t, name, 1));
                assert.ok(Math.abs(vertex(skin, ...points[0]).distanceTo(vertex(skin, ...points[1])) - distance) < 1e-4);
                for (const bone of Object.values(skin.bones)) assert.deepEqual(bone.scale.toArray(), [1, 1, 1]);
            }
        } finally { dispose(skin); }
    });
}
test('legacy mesh sitting has a held lower pelvis and returns to the source standing shape', () => {
    const skin = createPostureBody(new MeshBasicMaterial());
    try {
        assert.equal(sampleMotion(.3, 'sitMesh', 1).posture, 0); assert.equal(sampleMotion(2.5, 'sitMesh', 1).posture, 1);
        assert.equal(sampleMotion(6, 'sitMesh', 1).posture, 0);
        applyPostureBody(skin, sampleMotion(0, 'sitMesh', 1)); const start = skin.bones.pelvis.getWorldPosition(new Vector3());
        applyPostureBody(skin, sampleMotion(2.5, 'sitMesh', 1)); assert.ok(start.y - skin.bones.pelvis.getWorldPosition(new Vector3()).y > 60 * UNIT);
        for (let t = .4; t < 6; t += .05) {
            applyPostureBody(skin, sampleMotion(t, 'sitMesh', 1)); const a = vertex(skin, 315, 390);
            applyPostureBody(skin, sampleMotion(t + .0001, 'sitMesh', 1)); assert.ok(a.distanceTo(vertex(skin, 315, 390)) < .05);
        }
    } finally { dispose(skin); }
});
test('legacy sitting separate front and far hind chains keep their ankle targets on the floor', () => {
    for (const [kind, side] of [['front', 'near'], ['front', 'far'], ['hind', 'far']]) {
        const skin = createMotionLimb(new MeshBasicMaterial(), kind, side);
        try {
            let max = 0;
            for (let t = 0; t < MOTIONS.sitMesh; t += .05) {
                const pose = sampleMotion(t, 'sitMesh', 1);
                if (kind === 'front') applyPostureFront(skin, pose, side); else applyPostureFarHind(skin, pose);
                max = Math.max(max, skin.bones.ankle.getWorldPosition(new Vector3()).distanceTo(skin.bones.target.getWorldPosition(new Vector3())) / UNIT);
            }
            assert.ok(max < 2.5, `${kind}/${side} ankle error ${max}`);
        } finally { dispose(skin); }
    }
});
test('generated sit stays seated, loops micromotion and keeps front paws fixed', () => {
    const skin = createSitRest(new MeshBasicMaterial());
    try {
        for (const t of [.2, 1.5, 5]) {
            const a = sampleMotion(t, 'sit', 1), b = sampleMotion(t + MOTIONS.sit, 'sit', 1);
            assert.equal(a.posture, 0); assert.equal(a.down, 0); assert.equal(a.x, 0);
            for (const k of ['head', 'ears', 'tail', 'breathe']) assert.ok(Math.abs(a[k] - b[k]) < 1e-10);
        }
        applySitRest(skin, sampleMotion(0, 'sit', 1)); const chest = vertex(skin, 140, 340), paw = vertex(skin, 100, 480);
        applySitRest(skin, sampleMotion(.8, 'sit', 1)); assert.ok(chest.distanceTo(vertex(skin, 140, 340)) > .5);
        assert.ok(paw.distanceTo(vertex(skin, 100, 480)) < 1e-5);
    } finally { dispose(skin); }
});
test('side rest is a periodic held pose with actual local mesh motion, not a standing-to-side morph', () => {
    const skin = createSideRest(new MeshBasicMaterial());
    try {
        for (const t of [.2, 1.5, 5]) {
            const a = sampleMotion(t, 'side', 1), b = sampleMotion(t + MOTIONS.side, 'side', 1);
            assert.equal(a.posture, 0); assert.equal(a.down, 0); assert.equal(a.x, 0);
            for (const k of ['head', 'ears', 'tail', 'breathe']) assert.ok(Math.abs(a[k] - b[k]) < 1e-10);
        }
        applySideRest(skin, sampleMotion(0, 'side', 1)); const chest = vertex(skin, 235, 380), paw = vertex(skin, 100, 457);
        applySideRest(skin, sampleMotion(.8, 'side', 1)); assert.ok(chest.distanceTo(vertex(skin, 235, 380)) > .5);
        assert.ok(paw.distanceTo(vertex(skin, 100, 457)) < 1e-5);
    } finally { dispose(skin); }
});
