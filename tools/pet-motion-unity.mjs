// Bake the accepted Three rigs/CCDIK offline; Unity receives native meshes and bone curves, not frames.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { MeshBasicMaterial } from 'three';
import { createMotionBody, createMotionLimb, applyMotionBody, applyMotionLimb, sampleMotion, MOTIONS, UNIT } from '../src/pet-layers/motion.js';
import { createSitRest, applySitRest } from '../src/pet-layers/sit-rest.js';
import { createSideRest, applySideRest } from '../src/pet-layers/side-rest.js';
import { trimRig } from './pet-motion-atlas.mjs';

// This host's Node 24 optimizer produced unstable duration constants in this large offline bake.
// Keep the reproducible authoring step interpreted; the Unity runtime is unaffected.
if (!process.execArgv.includes('--jitless')) {
    const child = spawnSync(process.execPath, ['--jitless',fileURLToPath(import.meta.url),...process.argv.slice(2)], {stdio:'inherit'});
    process.exit(child.status ?? 1);
}

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const round = v => Math.round(v * 1e6) / 1e6;
const material = new MeshBasicMaterial();
const definitions = [
    ['farHind', 'far-hind-copy', () => createMotionLimb(material, 'hind', 'far'), 'hind'],
    ['farFront', 'far-front-copy', () => createMotionLimb(material, 'front', 'far'), 'front'],
    ['body', 'body', () => createMotionBody(material)],
    ['nearHind', 'near-hind', () => createMotionLimb(material, 'hind', 'near'), 'hind'],
    ['nearFront', 'near-front', () => createMotionLimb(material, 'front', 'near'), 'front'],
    ['sit', 'sit-rest', () => createSitRest(material)],
    ['side', 'side-rest', () => createSideRest(material)],
];
const skins = definitions.map(([id, texture, create, kind]) => ({ id, texture, skin: create(), kind }));
const requiredBones = new Map(skins.map(({skin})=>{
    const indices=skin.geometry.attributes.skinIndex.array,weights=skin.geometry.attributes.skinWeight.array;
    const bones=new Set();
    for(let i=0;i<weights.length;i++) if(weights[i]>0) {
        let bone=skin.skeleton.bones[indices[i]];
        while(bone?.isBone) { bones.add(bone);bone=bone.parent; }
    }
    return [skin,bones];
}));
const rigs = skins.map(({ id, texture, skin }) => {
    const g = skin.geometry;
    return { id, texture,
        positions: Array.from(g.attributes.position.array, round), uv: Array.from(g.attributes.uv.array, round),
        triangles: Array.from(g.index.array), indices: Array.from(g.attributes.skinIndex.array),
        weights: Array.from(g.attributes.skinWeight.array, round),
        bones: skin.skeleton.bones.map(b => ({ id: b.name,
            parent: skin.skeleton.bones.indexOf(b.parent), position: b.position.toArray().map(round) })) };
});
function reduce(times, values, tolerance) {
    const keep = new Set([0, values.length - 1]);
    function visit(a, b) {
        let worst = tolerance, index = -1;
        for (let i = a + 1; i < b; i++) {
            const predicted = values[a] + (values[b] - values[a]) * (times[i] - times[a]) / (times[b] - times[a]);
            const error = Math.abs(values[i] - predicted);
            if (error > worst) { worst = error; index = i; }
        }
        if (index >= 0) { keep.add(index); visit(a, index); visit(index, b); }
    }
    visit(0, values.length - 1);
    const indices = [...keep].sort((a,b) => a-b);
    return { times: indices.map(i => round(times[i])), values: indices.map(i => round(values[i])) };
}
function bake(action, duration) {
    const count = Math.ceil(duration * 60), times = Array.from({length:count+1}, (_,i) => duration*i/count);
    const tracks = new Map();
    const record = (p, property, value, step = false) => {
        const key = `${p}:${property}`;
        if (!tracks.has(key)) tracks.set(key, {path:p, property, step, values:[]});
        tracks.get(key).values.push(value);
    };
    for (const t of times) {
        const pose = sampleMotion(t, action, .8);
        record('', 'm_LocalPosition.x', pose.x * UNIT); record('', 'm_LocalPosition.y', -pose.down * UNIT);
        record('', 'm_LocalPosition.z', 0);
        record('', 'm_LocalScale.x', pose.mirror ? -1 : 1, true);
        record('', 'm_LocalScale.y', 1); record('', 'm_LocalScale.z', 1);
        for (const {id,skin,kind} of skins) {
            if (action === 'sit' || action === 'side') {
                if (id !== action) continue;
                (id === 'sit' ? applySitRest : applySideRest)(skin, pose);
            } else {
                if (id === 'sit' || id === 'side') continue;
                if (id === 'body') applyMotionBody(skin, pose);
                else applyMotionLimb(skin, pose, pose.feet[id], kind);
            }
            for (const bone of skin.skeleton.bones) {
                // Offline CCD targets have no skin influence or weighted descendants in Unity.
                if (!requiredBones.get(skin).has(bone)) continue;
                const names = [bone.name]; let parent = bone.parent;
                while (parent?.isBone) { names.unshift(parent.name); parent = parent.parent; }
                const p = `${id === 'sit' || id === 'side' ? '' : 'Stand/'}${id}/${names.join('/')}`;
                for (const axis of ['x','y','z']) {
                    record(p, `m_LocalPosition.${axis}`, bone.position[axis]);
                    record(p, `m_LocalScale.${axis}`, bone.scale[axis]);
                }
                record(p, 'localEulerAnglesRaw.z', bone.rotation.z * 180/Math.PI);
            }
        }
    }
    const curves = [];
    const family = t => `${t.path}:${t.property.split('.')[0]}`;
    const animatedFamilies = new Set([...tracks.values()].filter(t => t.values.some(v => Math.abs(v-t.values[0])>1e-7)).map(family));
    for (const track of tracks.values()) {
        const first = track.values[0];
        // Unity samples a position/scale vector as a group: retain constant sibling axes as well.
        if (track.values.every(v => Math.abs(v-first) < 1e-7) && !animatedFamilies.has(family(track))) continue;
        // Only close a take within the SAME skeleton. This is not a stand/sit/side transition.
        if (!track.step) for (let i = 0; i < times.length; i++) {
            const a = Math.max(0, (times[i] - duration + .15) / .15);
            track.values[i] += (first - track.values[i]) * a*a*(3-2*a);
        }
        const tolerance = track.property.includes('Scale') ? .0002 : track.property.includes('Euler') ? .08 : .05;
        const reduced = track.step ? {times, values: track.values} : reduce(times,track.values,tolerance);
        if (track.step) {
            const keep = reduced.values.map((v,i) => i === 0 || v !== reduced.values[i-1] || i === count ? i : -1).filter(i=>i>=0);
            reduced.times = keep.map(i=>round(times[i])); reduced.values = keep.map(i=>track.values[i]);
        }
        curves.push({ path:track.path, property:track.property, step:track.step, ...reduced });
    }
    return { id:action, duration:round(duration), curves };
}
const takeSpecs = [['idle',20*Math.PI], ['look',20*Math.PI], ['tread',22],
    ['walk',MOTIONS.walk*2], ['sit',MOTIONS.sit], ['side',MOTIONS.side]];
const atlas = JSON.parse(fs.readFileSync(path.join(root,'art-source/pet-motion/atlas.json'),'utf8'));
const optimizedRigs = rigs.map(r=>trimRig(r,atlas));
const data = { schema:2, sampleRate:60, amount:.8, loopClosureSeconds:.15,
    reductionTolerance:{position:.05,rotationDegrees:.08,scale:.0002},
    rigs:optimizedRigs, sources:['original','sit-rest','side-rest'].map(texture=>trimRig({...rigs[2],id:texture,texture},atlas)),
    takes:takeSpecs.map(([action,duration]) => bake(action,duration)) };
for (const take of data.takes) {
    const expected = takeSpecs.find(([id])=>id===take.id)[1];
    if (Math.abs(take.duration-expected)>.000001) throw new Error('Incorrect take duration');
    for (const c of take.curves) if (Math.abs(c.times.at(-1)-expected)>.000001) throw new Error('Incorrect curve duration');
}
const out = path.join(root,'art-source/pet-motion/unity-export.json');
const content = JSON.stringify(data) + '\n';
if (process.argv.includes('--check')) {
    if (fs.readFileSync(out,'utf8') !== content) throw new Error('Stale Unity animal motion bake');
} else fs.writeFileSync(out,content);
console.log(JSON.stringify({bytes:Buffer.byteLength(content), rigs:rigs.length,
    takes:data.takes.map(t=>({id:t.id, duration:t.duration, curves:t.curves.length, keys:t.curves.reduce((n,c)=>n+c.times.length,0)}))},null,2));
const fixtures = [];
for (const action of ['idle','look','tread','walk','sit','side']) for (const time of [.02,.15,.29,.85,1.36,2.63,4.81,5.2]) {
    const pose = sampleMotion(time,action,.8);
    for (const {id,skin,kind} of skins) {
        if (action === 'sit' || action === 'side') {
            if (id !== action) continue;
            (id === 'sit' ? applySitRest : applySideRest)(skin,pose);
        } else {
            if (id === 'sit' || id === 'side') continue;
            if (id === 'body') applyMotionBody(skin,pose);
            else applyMotionLimb(skin,pose,pose.feet[id],kind);
        }
        const retained = optimizedRigs.find(r=>r.id===id).sourceVertices;
        const indices = [1,2,3,4,5].map(i=>Math.floor(retained.length*i/6));
        const vertices = indices.map(index => {
            index = retained[index];
            const v = skin.mesh.getVertexPosition(index, skin.mesh.position.clone());
            return [(v.x * (pose.mirror ? -1 : 1) + pose.x * UNIT), v.y - pose.down * UNIT, v.z].map(round);
        }).flat();
        fixtures.push({action,time,id,indices,vertices});
    }
}
const fixturePath = path.join(root,'unity/HomelessPetHelper/Assets/EditorScenes/PetMotion/PetMotionFixtures.json');
const fixtureContent = JSON.stringify({fixtures,meshes:optimizedRigs.map(({id,sourceVertices})=>({id,sourceVertices}))})+'\n';
if (process.argv.includes('--check')) {
    if (fs.readFileSync(fixturePath,'utf8') !== fixtureContent) throw new Error('Stale Unity pet fixtures');
} else fs.writeFileSync(fixturePath,fixtureContent);
for (const {skin} of skins) { skin.geometry.dispose(); skin.skeleton.dispose(); }
material.dispose();
