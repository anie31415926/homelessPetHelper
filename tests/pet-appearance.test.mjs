import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createHash} from 'node:crypto';
import {Matrix4, Vector3} from 'three';
import {DEFAULT_APPEARANCE, defineAppearance, validateAtlas} from '../src/pet-sit-chibi/profile.js';
import {createChibiRig, sourcePointFor} from '../src/pet-sit-chibi/rig.js';
import {createChibiSkin} from '../src/pet-sit-chibi/skin.js';
const read = path => JSON.parse(fs.readFileSync(path, 'utf8'));
const manifest = read('public/pet-sit-chibi/atlas.json');
const sample = defineAppearance(read('src/pet-sit-chibi/appearances/cat-compact-sample.json'));

test('profile extraction preserves the approved baked geometry and animation exactly', () => {
    const actual = read(DEFAULT_APPEARANCE.export.bakePath);
    const approved = read('tests/fixtures/pet-sit-chibi-approved.json');
    for (const [section, hash] of Object.entries(approved.sha256))
        assert.equal(createHash('sha256').update(JSON.stringify(actual[section])).digest('hex'), hash, section);
});

test('appearance definitions are immutable and reject missing, invalid and ambiguous inputs', () => {
    assert.throws(() => {DEFAULT_APPEARANCE.landmarks.hip[0] = 0;}, TypeError);
    const mutations = [
        p => {p.source.pixelsPerUnit = 0;}, p => {delete p.landmarks.wrist;},
        p => {p.sit.keys[2].at = p.sit.keys[1].at;}, p => {p.sit.keys[1].spine = [0];},
        p => {p.tail.periodSeconds = NaN;}, p => {p.skin.wristBlendPixels[1] = -1;},
        p => {p.template = 'front';}, p => {p.unity.assetFolder = 'Assets/../Other';},
        p => {p.export.bakePath = 'src/erase.js';}, p => {p.tail.swishLiftDegrees = 0;},
    ];
    for (const mutate of mutations) {const p = structuredClone(DEFAULT_APPEARANCE); mutate(p); assert.throws(() => defineAppearance(p), /Invalid animal appearance/);}
    const atlas = structuredClone(manifest); atlas.layers[0].atlasX = atlas.width;
    assert.throws(() => validateAtlas(DEFAULT_APPEARANCE, atlas), /bounds/);
    atlas.layers = atlas.layers.filter(l => l.id !== DEFAULT_APPEARANCE.skin.layers.tail);
    assert.throws(() => validateAtlas(DEFAULT_APPEARANCE, atlas), /missing role/);
});

test('second profile recomputes limb lengths, head pivot, timing and tail motion without a second player', () => {
    const first = createChibiRig(), second = createChibiRig(sample);
    const skin = createChibiSkin(manifest, null, sample), point = sourcePointFor(sample);
    const tailIndex = skin.skeleton.bones.findIndex(b => b.name === 'tailArt');
    const pelvisIndex = skin.skeleton.bones.findIndex(b => b.name === 'bodyPelvis');
    try {
        assert.notEqual(first.lengths.femur, second.lengths.femur);
        assert.ok(second.world('skull').distanceTo(point(...sample.landmarks.skull)) < 1e-9);
        assert.notEqual(sample.sit.seconds, DEFAULT_APPEARANCE.sit.seconds);
        assert.notEqual(sample.tail.periodSeconds, DEFAULT_APPEARANCE.tail.periodSeconds);
        for (let i = 0; i <= 40; i++) {
            const progress = i / 40; second.apply(progress);
            for (const {from, to, length} of second.segments)
                assert.ok(Math.abs(second.world(from).distanceTo(second.world(to)) - length) < 1e-8);
            for (const side of ['near', 'far']) for (const joint of ['Hock', 'Wrist'])
                assert.ok(second.world(side + joint).distanceTo(second.world(side + joint + 'Target')) < .0001);
            skin.apply(progress, 0, 0, {raise: .5, swish: .5}, i / 30);
            const joined = point(...sample.skin.tailJoinPixels).applyMatrix4(new Matrix4().multiplyMatrices(
                skin.skeleton.bones[pelvisIndex].matrixWorld, skin.skeleton.boneInverses[pelvisIndex]));
            assert.ok(skin.tailJoinLocal.clone().applyMatrix4(skin.skeleton.bones[tailIndex].matrixWorld).distanceTo(joined) < 1e-9);
            for (const mesh of skin.meshes) for (let index = 0; index < mesh.geometry.attributes.position.count; index += 19)
                assert.ok(mesh.getVertexPosition(index, new Vector3()).toArray().every(Number.isFinite));
        }
    } finally {first.dispose(); second.dispose(); skin.dispose();}
});

test('new canvas coordinates and renamed layer IDs are handled by data rather than orange-cat pixel literals', () => {
    const p = structuredClone(DEFAULT_APPEARANCE), m = structuredClone(manifest), dx = 73, dy = 41;
    const shift = pair => [pair[0] + dx, pair[1] + dy];
    p.source.origin = shift(p.source.origin);
    for (const key of Object.keys(p.landmarks)) p.landmarks[key] = shift(p.landmarks[key]);
    p.skin.tailJoinPixels = shift(p.skin.tailJoinPixels);
    p.skin.hipBlendPixels[0] += dx; p.skin.elbowBlendPixels[0] += dy; p.skin.wristBlendPixels[0] += dy;
    // Shaft is a local donor patch, deliberately independent of the main image canvas.
    for (const layer of m.layers) {
        const old = layer.id; layer.id = 'other-' + old; p.skin.layers[old] = layer.id;
        if (old === 'shaft') continue;
        layer.left += dx; layer.top += dy;
        if (layer.support) layer.support = layer.support.map(shift);
    }
    const original = createChibiSkin(manifest, null), shifted = createChibiSkin(m, null, defineAppearance(p));
    try {
        for (const progress of [0, .2, .55, 1]) {
            original.apply(progress); shifted.apply(progress);
            for (let i = 0; i < original.meshes.length; i++) for (const index of [0, 20])
                assert.ok(original.meshes[i].getVertexPosition(index, new Vector3()).distanceTo(
                    shifted.meshes[i].getVertexPosition(index, new Vector3())) < 1e-8, `${progress}: ${original.meshes[i].name}, vertex ${index}`);
        }
    } finally {original.dispose(); shifted.dispose();}
});
