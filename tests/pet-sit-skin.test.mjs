import assert from 'node:assert/strict';
import test from 'node:test';
import {readFile} from 'node:fs/promises';
import sharp from 'sharp';
import {Vector3} from 'three';
import {createSkin} from '../src/pet-sit-skin/skin.js';
import {createSitRig} from '../src/pet-sit-study/rig.js';

const manifest = JSON.parse(await readFile('public/pet-sit-skin/atlas.json', 'utf8'));
const vertex = (mesh, index) => mesh.getVertexPosition(index, new Vector3());
const area = (a, b, c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

test('all 501 poses keep every approved motion bone bit-identical', () => {
    const skin = createSkin(manifest, null), approved = createSitRig();
    try {
        for (let i = 0; i <= 500; i++) {
            skin.apply(i / 500); approved.apply(i / 500);
            skin.rig.bones.forEach((bone, index) => assert.deepEqual(bone.matrixWorld.toArray(), approved.bones[index].matrixWorld.toArray()));
        }
    } finally {skin.dispose(); approved.dispose();}
});

test('every attachment uses one rendering skeleton and normalized finite weights', () => {
    const skin = createSkin(manifest, null);
    try {
        assert.equal(skin.meshes.length, 14);
        for (const mesh of skin.meshes) {
            assert.equal(mesh.skeleton, skin.skeleton);
            const weights = mesh.geometry.attributes.skinWeight, indices = mesh.geometry.attributes.skinIndex;
            for (let i = 0; i < weights.count; i++) {
                let sum = 0;
                for (let j = 0; j < 4; j++) {const weight = weights.array[i * 4 + j]; assert.ok(Number.isFinite(weight) && weight >= 0); assert.ok(indices.array[i * 4 + j] < skin.skeleton.bones.length); sum += weight;}
                assert.ok(Math.abs(sum - 1) < 1e-6);
            }
        }
    } finally {skin.dispose();}
});

test('head, both thighs and paws preserve all local edge lengths and area', () => {
    const skin = createSkin(manifest, null);
    try {
        skin.apply(0);
        const rigid = skin.meshes.filter(mesh => /Thigh|Paw|head/.test(mesh.name));
        const samples = rigid.map(mesh => {const a = vertex(mesh, 0), b = vertex(mesh, 1), c = vertex(mesh, mesh.geometry.index.getX(2)); return {mesh, edge: a.distanceTo(b), area: area(a, b, c)};});
        for (let i = 0; i <= 100; i++) {
            skin.apply(i / 100);
            for (const sample of samples) {
                const {mesh} = sample, a = vertex(mesh, 0), b = vertex(mesh, 1), c = vertex(mesh, mesh.geometry.index.getX(2));
                assert.ok(Math.abs(a.distanceTo(b) - sample.edge) < 1e-8, `${i}: ${mesh.name} edge`);
                assert.ok(Math.abs(area(a, b, c) - sample.area) < 1e-8, `${i}: ${mesh.name} area`);
            }
        }
    } finally {skin.dispose();}
});

test('all mesh triangles remain positive through 101 poses', () => {
    const skin = createSkin(manifest, null);
    try {
        for (let i = 0; i <= 100; i++) {
            skin.apply(i / 100);
            for (const mesh of skin.meshes) for (let t = 0; t < mesh.geometry.index.count; t += 3) {
                const points = [0, 1, 2].map(j => vertex(mesh, mesh.geometry.index.getX(t + j)));
                assert.ok(area(...points) > 1e-10, `${i}: folded ${mesh.name} triangle ${t / 3}`);
            }
        }
    } finally {skin.dispose();}
});

test('arbitrary seeks restore identical skin vertices, anchors and matrices', () => {
    const skin = createSkin(manifest, null);
    const snapshot = progress => {skin.apply(progress); return skin.meshes.flatMap(mesh => [0, 3, 20, mesh.geometry.attributes.position.count - 1].flatMap(index => vertex(mesh, index).toArray()));};
    try {for (const at of [0, .073, .2, .55, .86, 1]) {const expected = snapshot(at); snapshot(1 - at); snapshot(.31); assert.deepEqual(snapshot(at), expected);}} finally {skin.dispose();}
});

test('source face pixels remain exact; atlas stays within local preview budget', async () => {
    const head = manifest.layers.find(layer => layer.id === 'head'), old = JSON.parse(await readFile('public/pet-transition/atlas.json', 'utf8')).layers.find(layer => layer.id === 'head');
    const crop = {width: 244, height: 240};
    const actual = await sharp('public/pet-sit-skin/atlas.png').extract({left: head.atlasX, top: head.atlasY, ...crop}).raw().toBuffer();
    const expected = await sharp('public/pet-transition/atlas.png').extract({left: old.atlasX, top: old.atlasY, ...crop}).raw().toBuffer();
    assert.ok(actual.equals(expected)); assert.ok((await readFile('public/pet-sit-skin/atlas.png')).length <= 393216);
    assert.ok(manifest.width <= 1024 && manifest.height <= 1024);
});

test('opaque skin samples stay above ground and three supporting paws remain close to it', async () => {
    const skin = createSkin(manifest, null), pixels = await sharp('public/pet-sit-skin/atlas.png').ensureAlpha().raw().toBuffer();
    const opaque = new Map(skin.meshes.map(mesh => {
        const uv = mesh.geometry.attributes.uv, indices = [];
        for (let i = 0; i < uv.count; i++) {
            const x = Math.max(0, Math.min(manifest.width - 1, Math.round(uv.getX(i) * manifest.width))), y = Math.max(0, Math.min(manifest.height - 1, Math.round((1 - uv.getY(i)) * manifest.height)));
            if (pixels[(y * manifest.width + x) * 4 + 3] > 96) indices.push(i);
        }
        return [mesh, indices];
    }));
    try {
        for (let i = 0; i <= 100; i++) {
            skin.apply(i / 100); const bottoms = {};
            for (const [mesh, indices] of opaque) {
                const bottom = Math.min(...indices.map(index => vertex(mesh, index).y));
                assert.ok(bottom >= .004, `${i}: ${mesh.name} below ground ${bottom}`); bottoms[mesh.name] = bottom;
            }
            assert.ok(bottoms.nearPaw < .035 && bottoms.farPaw < .035);
            assert.ok(Math.min(bottoms.nearForeleg, bottoms.farForeleg) < .035);
        }
    } finally {skin.dispose();}
});
