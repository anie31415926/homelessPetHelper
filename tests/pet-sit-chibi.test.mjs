import assert from 'node:assert/strict';
import test from 'node:test';
import {readFile} from 'node:fs/promises';
import sharp from 'sharp';
import {Matrix4, Vector3} from 'three';
import {createChibiRig, LANDMARKS, LENGTHS, sourcePoint} from '../src/pet-sit-chibi/rig.js';
import {createChibiSkin} from '../src/pet-sit-chibi/skin.js';
import {createSitRig, LENGTHS as STUDY_LENGTHS} from '../src/pet-sit-study/rig.js';

const manifest = JSON.parse(await readFile('public/pet-sit-chibi/atlas.json', 'utf8'));
const vertex = (mesh, index) => mesh.getVertexPosition(index, new Vector3());
const area = (a, b, c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

test('short bones retain approved topology rather than scaling the long-leg artwork', () => {
    const rig = createChibiRig(), study = createSitRig();
    try {
        assert.deepEqual(rig.bones.map(b => [b.name, b.parent.name]), study.bones.map(b => [b.name, b.parent.name]));
        for (const key of ['femur', 'tibia', 'metatarsal', 'humerus', 'radius', 'spine']) assert.ok(LENGTHS[key] < STUDY_LENGTHS[key] * .75);
        assert.equal(rig.bones.length, 30);
    } finally {rig.dispose(); study.dispose();}
});

test('standing joints match original pixel landmarks, without stretching the bind pose', () => {
    const rig = createChibiRig();
    try {
        for (const [key, name] of [['hip', 'Hip'], ['knee', 'Knee'], ['hock', 'Hock'], ['toe', 'Toe'], ['shoulder', 'Shoulder'], ['elbow', 'Elbow'], ['wrist', 'Wrist'], ['frontToe', 'FrontToe']]) {
            const expected = sourcePoint(...LANDMARKS[key]); expected.z = .2;
            assert.ok(rig.world('near' + name).distanceTo(expected) < .0001, key);
        }
        assert.ok(rig.world('skull').distanceTo(sourcePoint(...LANDMARKS.skull)) < 1e-10);
        for (const p of [-.01, 1.01, NaN, Infinity]) assert.throws(() => rig.apply(p), RangeError);
    } finally {rig.dispose();}
});

test('501 samples keep fixed lengths, unit scale and reachable contacts', () => {
    const rig = createChibiRig();
    try {
        for (let i = 0; i <= 500; i++) {
            rig.apply(i / 500);
            for (const {from, to, length} of rig.segments) assert.ok(Math.abs(rig.world(from).distanceTo(rig.world(to)) - length) < 1e-10);
            for (const bone of rig.bones) assert.deepEqual(bone.scale.toArray(), [1, 1, 1]);
            for (const side of ['near', 'far']) for (const joint of ['Hock', 'Wrist']) assert.ok(rig.world(side + joint).distanceTo(rig.world(side + joint + 'Target')) < .0001, `${i}: ${side}${joint}`);
        }
    } finally {rig.dispose();}
});

test('short seated chain still puts knee ahead/above hip and hock below/behind knee', () => {
    const rig = createChibiRig();
    try {
        rig.apply(1);
        for (const side of ['near', 'far']) {
            const hip = rig.world(side + 'Hip'), knee = rig.world(side + 'Knee'), hock = rig.world(side + 'Hock'), toe = rig.world(side + 'Toe');
            assert.ok(knee.x > hip.x + .5 && knee.y > hip.y + .3);
            assert.ok(hock.x < knee.x && hock.y < knee.y - .5 && hock.y < .32);
            assert.ok(toe.x > hock.x + .45);
        }
    } finally {rig.dispose();}
});

test('all triangles stay positive and head/thigh/paw geometry remains rigid', () => {
    const skin = createChibiSkin(manifest, null), baselines = new Map();
    try {
        for (const mesh of skin.meshes.filter(mesh => /Thigh|Paw|head/.test(mesh.name))) {
            const points = [0, 1, mesh.geometry.index.getX(2)].map(index => vertex(mesh, index));
            baselines.set(mesh, {edge: points[0].distanceTo(points[1]), area: area(...points)});
        }
        for (let i = 0; i <= 100; i++) {
            skin.apply(i / 100);
            assert.equal(skin.contactShifts.nearThighArt, 0); assert.equal(skin.contactShifts.farThighArt, 0);
            for (const mesh of skin.meshes) {
                assert.equal(mesh.skeleton, skin.skeleton);
                for (let t = 0; t < mesh.geometry.index.count; t += 3) assert.ok(area(...[0, 1, 2].map(j => vertex(mesh, mesh.geometry.index.getX(t + j)))) > 1e-10, `${i}: ${mesh.name} folded`);
            }
            for (const [mesh, baseline] of baselines) {
                const points = [0, 1, mesh.geometry.index.getX(2)].map(index => vertex(mesh, index));
                assert.ok(Math.abs(points[0].distanceTo(points[1]) - baseline.edge) < 1e-8);
                assert.ok(Math.abs(area(...points) - baseline.area) < 1e-8);
            }
        }
    } finally {skin.dispose();}
});

test('native-sized head and original torso preserve source pixels and original bind coordinates', async () => {
    const skin = createChibiSkin(manifest, null);
    try {
        for (const id of ['head', 'torso']) {
            const mesh = skin.meshes.find(mesh => mesh.name === id), uv = mesh.geometry.attributes.uv, layer = manifest.layers.find(layer => layer.id === id);
            for (let i = 0; i < uv.count; i++) {
                const x = uv.getX(i) * manifest.width - layer.atlasX + layer.left, y = (1 - uv.getY(i)) * manifest.height - layer.atlasY + layer.top;
                assert.ok(vertex(mesh, i).distanceTo(sourcePoint(x, y)) < .000001);
            }
        }
        const head = manifest.layers.find(layer => layer.id === 'head');
        const actual = await sharp('public/pet-sit-chibi/atlas.png').extract({left: head.atlasX, top: head.atlasY, width: 244, height: 240}).raw().toBuffer();
        const expected = await sharp('public/pet-mesh/cat-side.png').extract({left: head.left, top: head.top, width: 244, height: 240}).ensureAlpha().raw().toBuffer();
        assert.ok(actual.equals(expected));
    } finally {skin.dispose();}
});

test('opaque skin does not cross ground, including tail and torso', async () => {
    const skin = createChibiSkin(manifest, null), pixels = await sharp('public/pet-sit-chibi/atlas.png').ensureAlpha().raw().toBuffer();
    const samples = new Map(skin.meshes.map(mesh => [mesh, [...Array(mesh.geometry.attributes.uv.count).keys()].filter(i => {
        const uv = mesh.geometry.attributes.uv, x = Math.max(0, Math.min(manifest.width - 1, Math.round(uv.getX(i) * manifest.width))), y = Math.max(0, Math.min(manifest.height - 1, Math.round((1 - uv.getY(i)) * manifest.height)));
        return pixels[(y * manifest.width + x) * 4 + 3] > 96;
    })]));
    try {
        for (let i = 0; i <= 100; i++) {
            skin.apply(i / 100);
            for (const [mesh, indices] of samples) for (const index of indices) assert.ok(vertex(mesh, index).y >= .004, `${i}: ${mesh.name} penetrated floor`);
        }
    } finally {skin.dispose();}
});

test('tailward rump settles onto the floor with a joined tail and unchanged movement bones', async () => {
    const skin = createChibiSkin(manifest, null), movement = createChibiRig();
    const torso = skin.meshes.find(mesh => mesh.name === 'torso'), tail = skin.meshes.find(mesh => mesh.name === 'tail'), uv = torso.geometry.attributes.uv;
    const layer = manifest.layers.find(layer => layer.id === 'torso'), pixels = await sharp('public/pet-sit-chibi/atlas.png').ensureAlpha().raw().toBuffer();
    const rear = [...Array(uv.count).keys()].filter(i => {
        const x = Math.round(uv.getX(i) * manifest.width), y = Math.round((1 - uv.getY(i)) * manifest.height);
        return x - layer.atlasX + layer.left >= 350 && pixels[(y * manifest.width + x) * 4 + 3] > 96;
    });
    const attachedPoint = name => {
        const index = skin.skeleton.bones.findIndex(bone => bone.name === name);
        return sourcePoint(363, 374).applyMatrix4(new Matrix4().multiplyMatrices(skin.skeleton.bones[index].matrixWorld, skin.skeleton.boneInverses[index]));
    };
    try {
        let previous;
        for (let i = 0; i <= 500; i++) {
            const p = i / 500; skin.apply(p); movement.apply(p);
            for (let j = 0; j < movement.bones.length; j++) assert.deepEqual(skin.rig.bones[j].matrixWorld.elements, movement.bones[j].matrixWorld.elements);
            assert.ok(attachedPoint('tailArt').distanceTo(attachedPoint('bodyPelvis')) < 1e-10, `Tail detached at ${p}`);
            const points = rear.map(index => vertex(torso, index));
            if (p >= .86) assert.ok(Math.min(...points.map(point => point.y)) < .06, `Rump floats at ${p}`);
            points.push(...[0, 20, tail.geometry.attributes.position.count - 1].map(index => vertex(tail, index)));
            if (previous) points.forEach((point, j) => assert.ok(point.distanceTo(previous[j]) < .025, `Coat jumps at ${p}`));
            previous = points;
        }
        skin.apply(1); assert.ok(skin.contactShifts.bodyPelvis < -.15); assert.equal(skin.contactShifts.tailArt, 0);
    } finally {skin.dispose(); movement.dispose();}
});

test('seeking is deterministic and motion is continuous without branch changes', () => {
    const skin = createChibiSkin(manifest, null);
    const snapshot = p => {skin.apply(p); return skin.meshes.flatMap(mesh => [0, 20, mesh.geometry.attributes.position.count - 1].flatMap(index => vertex(mesh, index).toArray()));};
    try {
        for (const p of [0, .035, .2, .55, .86, 1]) {const expected = snapshot(p); snapshot(1 - p); snapshot(.71); assert.deepEqual(snapshot(p), expected);}
        let last;
        for (let i = 0; i <= 1000; i++) {
            skin.apply(i / 1000); const points = ['nearHip', 'nearKnee', 'nearHock', 'nearToe', 'nearShoulder', 'nearElbow', 'nearWrist', 'skull'].map(skin.rig.world);
            if (last) points.forEach((point, j) => assert.ok(point.distanceTo(last[j]) < .012, `Jump at ${i}: ${j}`)); last = points;
        }
    } finally {skin.dispose();}
});

test('descent carries velocity through intermediate keys instead of stopping at every pose', () => {
    const rig = createChibiRig(), h = .0001;
    const height = p => {rig.apply(p); return rig.world('root').y;};
    try {
        for (const p of [.2, .55]) {
            const before = (height(p) - height(p - h)) / h, after = (height(p + h) - height(p)) / h;
            assert.ok(before < -.2 && after < -.2, `Descent stalls at ${p}`);
            assert.ok(Math.abs(before - after) < .01, `Velocity breaks at ${p}`);
        }
        assert.ok(Math.abs((height(h) - height(0)) / h) < .01);
        assert.ok(Math.abs((height(1) - height(1 - h)) / h) < .01);
    } finally {rig.dispose();}
});


test('tail expressions keep the root joined and stay above ground throughout posture and blends', () => {
    const skin = createChibiSkin(manifest, null), layer = manifest.layers.find(l => l.id === 'tail');
    const tailIndex = skin.skeleton.bones.findIndex(b => b.name === 'tailArt');
    const pelvisIndex = skin.skeleton.bones.findIndex(b => b.name === 'bodyPelvis');
    const tail = skin.skeleton.bones[tailIndex], local = skin.tailJoinLocal;
    try {
        for (const progress of [0, .2, .55, .75, .86, 1]) {
            for (const expression of [{raise: 0, swish: 1}, {raise: 1, swish: 0}, {raise: .5, swish: .5}]) {
                for (let frame = 0; frame <= 16; frame++) {
                    skin.apply(progress, 0, 0, expression, frame / 20);
                    const join = sourcePoint(363, 374).applyMatrix4(skin.skeleton.boneInverses[pelvisIndex]).applyMatrix4(skin.skeleton.bones[pelvisIndex].matrixWorld);
                    assert.ok(local.clone().applyMatrix4(tail.matrixWorld).distanceTo(join) < 1e-9);
                    for (const [x, y] of layer.support) {
                        const point = sourcePoint(x, y).applyMatrix4(skin.skeleton.boneInverses[tailIndex]).applyMatrix4(tail.matrixWorld);
                        assert.ok(point.y >= .004, 'Expressive tail crosses ground');
                    }
                }
            }
        }
        skin.apply(1, 0, 0, {raise: 0, swish: 1}, .2); const first = tail.quaternion.clone();
        skin.apply(1, 0, 0, {raise: 0, swish: 1}, .6);
        assert.ok(first.angleTo(tail.quaternion) > .5, 'Irritated tail must move in both directions');
        skin.apply(1, 0, 0, {raise: 1, swish: 0}, .2); const happy = tail.matrixWorld.clone();
        skin.apply(1, 0, 0, {raise: 1, swish: 0}, .6); assert.deepEqual(tail.matrixWorld.elements, happy.elements);
    } finally {skin.dispose();}
});


test('one-second descent reaches the seat without a separate late hip readjustment', () => {
    const rig = createChibiRig();
    try {
        let previous = null;
        for (let i = 0; i <= 100; i++) {
            rig.apply(i / 100); const hip = rig.world('root');
            if (previous) {
                assert.ok(hip.x <= previous.x + 1e-9, 'Hip reverses after touching down');
                assert.ok(hip.y <= previous.y + 1e-9, 'Hip bounces after touching down');
            }
            previous = hip;
        }
    } finally {rig.dispose();}
});
