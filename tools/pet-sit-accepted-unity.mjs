import assert from 'node:assert/strict';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import sharp from 'sharp';
import {BufferAttribute, PlaneGeometry} from 'three';
import {bakeAppearance} from '../src/pet-sit-chibi/bake.js';
import {DEFAULT_APPEARANCE as profile} from '../src/pet-sit-chibi/profile.js';
import {sourcePoint} from '../src/pet-sit-chibi/rig.js';
import {hindBounds, hindWeights} from '../src/pet-mesh-sit/join.js';
import {continuousLegPixels, repartitionPixels} from '../src/pet-mesh-sit/partition.js';
import {createPawContact} from '../src/pet-mesh-sit/paw-contact.js';

const target = 'art-source/pet-sit-chibi-20261123/accepted-native';
const check = process.argv.includes('--check');
const manifest = JSON.parse(await readFile(profile.art.atlasManifest, 'utf8'));
const [left, top, width, height] = hindBounds;
const atlasHeight = manifest.height + height + 4;
const raw = (width, height) => ({width, height, channels: 4});
const original = await sharp(profile.art.sourceImage).ensureAlpha().raw().toBuffer({resolveWithObject: true});
const atlas = await sharp(profile.art.atlasTexture).ensureAlpha().raw().toBuffer();
const source = {width: original.info.width, height: original.info.height, data: original.data};
assert.equal(source.width, 452);
assert.equal(source.height, 512);
repartitionPixels(atlas, manifest, source);
const continuous = Buffer.alloc(width * height * 4);
continuousLegPixels(source, continuous);
const pixels = Buffer.alloc(manifest.width * atlasHeight * 4);
atlas.copy(pixels);
for (let y = 0; y < height; y++) continuous.copy(pixels, ((manifest.height + 2 + y) * manifest.width + 2) * 4,
    y * width * 4, (y + 1) * width * 4);

function prepareSkin(skin) {
    for (const mesh of skin.meshes) {
        const uv = mesh.geometry.attributes.uv;
        for (let i = 0; i < uv.count; i++) uv.setY(i, 1 - (1 - uv.getY(i)) * manifest.height / atlasHeight);
    }
    const mesh = skin.meshes.find(item => item.name === 'nearLowerCoat');
    const geometry = new PlaneGeometry(width, height, 39, 62);
    const {position, uv} = geometry.attributes;
    const ids = [], weights = [];
    for (let i = 0; i < uv.count; i++) {
        const x = left + uv.getX(i) * width, y = top + (1 - uv.getY(i)) * height;
        const p = sourcePoint(x, y);
        position.setXYZ(i, p.x, p.y, 0);
        uv.setXY(i, (2 + x - left) / manifest.width, 1 - (manifest.height + 2 + y - top) / atlasHeight);
        for (const [bone, weight] of hindWeights('near', y, x).sort((a, b) => b[1] - a[1])) {
            ids.push(skin.skeleton.bones.findIndex(item => item.name === bone));
            weights.push(weight);
        }
        ids.push(0, 0); weights.push(0, 0);
    }
    geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(ids), 4));
    geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4));
    mesh.geometry.dispose();
    mesh.geometry = geometry;
    mesh.renderOrder = 5.13;
    const oldPaw = skin.meshes.splice(skin.meshes.findIndex(item => item.name === 'nearPaw'), 1)[0];
    skin.rig.group.remove(oldPaw);
    oldPaw.geometry.dispose(); oldPaw.material.dispose();
}

let applyPaw;
const afterSit = (skin, progress) => (applyPaw ??= createPawContact(skin, manifest))(true, progress);
const bake = bakeAppearance(profile, manifest, {prepareSkin, afterSit});
const active = new Set(['torso', 'tail', 'nearThigh', 'nearLowerCoat']);
for (const mesh of bake.meshes) if (active.has(mesh.id)) mesh.seatContact = mesh.id === 'nearLowerCoat' ? 2 : 1;
assert.ok(bake.takes[0].curves.find(c => c.path === 'nearPaw' && c.property === 'm_LocalRotation.z').values.at(-1) > .3);
assert.equal(bake.takes[1].curves.find(c => c.path === 'nearPaw' && c.property === 'm_LocalRotation.z').values.at(-1), 0);

if (!check) await mkdir(target, {recursive: true});
const png = await sharp(pixels, {raw: raw(manifest.width, atlasHeight)}).png().toBuffer();
for (const [file, bytes] of [[`${target}/bake.json`, Buffer.from(JSON.stringify(bake) + '\n')],
    [profile.export.fixturePath, Buffer.from(JSON.stringify({fixtures: bake.fixtures}) + '\n')],
    ['unity/HomelessPetHelper/' + profile.unity.atlasPath, png]]) {
    if (check) assert.deepEqual(await readFile(file), bytes, 'Stale accepted asset: ' + file);
    else await writeFile(file, bytes);
}
console.log(`PASS accepted bake: ${bake.meshes.length} renderers, ${manifest.width}x${atlasHeight} atlas`);
