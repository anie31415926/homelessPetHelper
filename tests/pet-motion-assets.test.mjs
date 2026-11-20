import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import sharp from 'sharp';
import { MeshBasicMaterial } from 'three';
import { inputs, trimRig } from '../tools/pet-motion-atlas.mjs';
import { createMotionBody, createMotionLimb } from '../src/pet-layers/motion.js';
import { createSitRest } from '../src/pet-layers/sit-rest.js';
import { createSideRest } from '../src/pet-layers/side-rest.js';
const atlas=JSON.parse(await fs.readFile('art-source/pet-motion/atlas.json','utf8'));
const data=JSON.parse(await fs.readFile('art-source/pet-motion/unity-export.json','utf8'));
const folder='unity/HomelessPetHelper/Assets/Art/PetMotion';
const round=v=>Math.round(v*1e6)/1e6 || 0;

test('atlas is lossless, padded, reversible by original offset and keeps original source files intact',async()=>{
    const pixels=await sharp(`${folder}/T_Pet_Atlas.png`).raw().toBuffer();
    assert.equal(atlas.regions.length,8);
    for(const r of atlas.regions) {
        const bytes=await fs.readFile(inputs[r.id]);
        assert.equal(createHash('sha256').update(bytes).digest('hex'),r.sha256);
        assert.deepEqual(bytes,await fs.readFile(`${folder}/T_Pet_${r.id}.png`));
        const original=await sharp(bytes).ensureAlpha().raw().toBuffer();
        for(let y=0;y<r.sourceHeight;y++) for(let x=0;x<r.sourceWidth;x++) {
            const inside=x>=r.left && x<r.left+r.width && y>=r.top && y<r.top+r.height;
            const from=(y*r.sourceWidth+x)*4;
            if(!inside) { assert.equal(original[from+3],0); continue; }
            const to=((r.y+y-r.top)*atlas.width+r.x+x-r.left)*4;
            assert.deepEqual(pixels.subarray(to,to+4),original.subarray(from,from+4),r.id);
            if(x-r.left<2 || y-r.top<2 || r.left+r.width-x<=2 || r.top+r.height-y<=2)
                assert.equal(original[from+3],0,`transparent gutter / ${r.id}`);
        }
    }
    assert.ok(atlas.width*atlas.height*4<8*452*512*4*.45);
});

test('compacted meshes retain exact coordinates, weights, bone pivots and atlas UV round trips',()=>{
    const material=new MeshBasicMaterial();
    const definitions=[['farHind',()=>createMotionLimb(material,'hind','far')],
        ['farFront',()=>createMotionLimb(material,'front','far')],['body',()=>createMotionBody(material)],
        ['nearHind',()=>createMotionLimb(material,'hind','near')],['nearFront',()=>createMotionLimb(material,'front','near')],
        ['sit',()=>createSitRest(material)],['side',()=>createSideRest(material)]];
    try {
        for(const [id,create] of definitions) {
            const skin=create(),rig=data.rigs.find(r=>r.id===id),g=skin.geometry;
            const texture={farHind:'far-hind-copy',farFront:'far-front-copy',body:'body',nearHind:'near-hind',
                nearFront:'near-front',sit:'sit-rest',side:'side-rest'}[id];
            const original={id,texture,positions:Array.from(g.attributes.position.array,round),
                uv:Array.from(g.attributes.uv.array,round),weights:Array.from(g.attributes.skinWeight.array,round),
                indices:Array.from(g.attributes.skinIndex.array),triangles:Array.from(g.index.array)};
            const region=atlas.regions.find(r=>r.id===texture);
            const trimmed=trimRig(original,atlas);
            for(const field of ['positions','uv','cropUV','weights','indices','triangles','sourceVertices'])
                assert.deepEqual(rig[field],trimmed[field],`${id}/${field}`);
            const used=[...new Set(rig.triangles)].sort((a,b)=>a-b);
            assert.equal(used.length,rig.positions.length/3);
            for(let i=0;i<rig.sourceVertices.length;i++) {
                const old=rig.sourceVertices[i];
                assert.deepEqual(rig.positions.slice(i*3,i*3+3),original.positions.slice(old*3,old*3+3));
                assert.deepEqual(rig.weights.slice(i*4,i*4+4),original.weights.slice(old*4,old*4+4));
                const x=rig.uv[i*2]*atlas.width-region.x+region.left;
                const y=rig.uv[i*2+1]*atlas.height-(atlas.height-region.y-region.height)+
                    region.sourceHeight-region.top-region.height;
                assert.ok(Math.abs(x-original.uv[old*2]*region.sourceWidth)<.00006,`${id}/canvas x`);
                assert.ok(Math.abs(y-original.uv[old*2+1]*region.sourceHeight)<.00006,`${id}/canvas y`);
            }
            assert.deepEqual(rig.bones,skin.skeleton.bones.map(b=>({id:b.name,parent:skin.skeleton.bones.indexOf(b.parent),
                position:b.position.toArray().map(round)})));
            skin.geometry.dispose();skin.skeleton.dispose();
        }
    } finally { material.dispose(); }
    assert.ok(data.rigs.reduce((n,r)=>n+r.positions.length/3,0)<14063*.5);
});

test('animation keeps vector siblings, step mirrors, durations and same-rig closure with fewer keys',()=>{
    assert.equal(data.schema,2);
    const durations=[20*Math.PI,20*Math.PI,22,10.44,6.4,7];
    let keys=0;
    for(const [i,take] of data.takes.entries()) {
        assert.ok(Math.abs(take.duration-durations[i])<1e-6);
        for(const curve of take.curves) {
            keys+=curve.times.length;
            assert.ok(!curve.path.endsWith('/target'),'offline-only IK targets have no runtime curves');
            assert.equal(curve.times.length,curve.values.length);
            assert.equal(curve.times[0],0);
            assert.ok(Math.abs(curve.times.at(-1)-take.duration)<1e-6);
            assert.ok(curve.values.every(Number.isFinite));
            if(curve.step) assert.ok(curve.values.every(v=>v===1 || v===-1));
            else assert.ok(Math.abs(curve.values.at(-1)-curve.values[0])<1e-5);
            if(curve.property.startsWith('m_LocalPosition') || curve.property.startsWith('m_LocalScale'))
                for(const axis of ['x','y','z']) assert.ok(take.curves.some(c=>c.path===curve.path &&
                    c.property===curve.property.slice(0,-1)+axis),'constant vector sibling retained');
        }
    }
    assert.ok(keys<32424*.8,`key budget: ${keys}`);
});
