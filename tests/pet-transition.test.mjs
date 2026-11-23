import test from 'node:test';
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import sharp from 'sharp';
import {Vector3} from 'three';
import {controlsAt,createTransitionRig,DURATION,sampleTransition,SPEC} from '../src/pet-transition/rig.js';

const manifest=JSON.parse(await readFile('public/pet-transition/atlas.json','utf8'));
const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
test('Original identity source has not changed',async()=>assert.equal(digest(await readFile(manifest.source)),manifest.sourceSha256));
test('Generation rerun has distinct prompt and identical decoded RGBA',async()=>{
    const folders=['torso-a','torso-repeat'].map(n=>`art-source/pet-transition-20261123/${n}`);
    const jobs=await Promise.all(folders.map(async p=>JSON.parse(await readFile(`${p}/submission.json`,'utf8'))));
    assert.notEqual(jobs[0].prompt_id,jobs[1].prompt_id);
    const images=await Promise.all(folders.map(p=>sharp(`${p}/master.png`).raw().toBuffer()));
    assert.equal(digest(images[0]),digest(images[1]));
    for(const folder of folders){const history=JSON.parse(await readFile(`${folder}/history.json`,'utf8'));assert.equal(history.status.status_str,'success');}
});
test('Shared head uses exact original RGBA in visible face',async()=>{
    const original=await sharp(manifest.source).ensureAlpha().raw().toBuffer();
    const head=await sharp('art-source/pet-transition-20261123/layers/head.png').ensureAlpha().raw().toBuffer();
    assert.deepEqual(head.subarray(0,244*452*4),original.subarray(0,244*452*4));
});
test('One skeleton is shared, head size and all opacity stay fixed',()=>{
    const rig=createTransitionRig(manifest,null);
    try {
        assert.equal(rig.skeleton.bones.length,SPEC.length);assert.equal(new Set(rig.meshes.map(m=>m.skeleton)).size,1);
        for(const p of [0,.2,.5,.8,1]){rig.apply(p);assert.ok(rig.meshes.every(m=>m.material.opacity===1));assert.equal(rig.bones[3].scale.x,1);assert.equal(rig.bones[3].rotation.z,0);}
    } finally {rig.dispose();}
});
test('Weights are finite non-negative and normalized',()=>{
    const rig=createTransitionRig(manifest,null);
    try{for(const mesh of rig.meshes){const w=mesh.geometry.attributes.skinWeight;for(let i=0;i<w.count;i++){let sum=0;for(let k=0;k<4;k++){const v=w.array[i*4+k];assert.ok(Number.isFinite(v)&&v>=0);sum+=v;}assert.ok(Math.abs(sum-1)<1e-6);}}}finally{rig.dispose();}
});
test('101 intermediate postures have no flipped triangles',()=>{
    const rig=createTransitionRig(manifest,null);
    try{for(let frame=0;frame<=100;frame++){
        rig.apply(frame/100);
        for(const mesh of rig.meshes){const g=mesh.geometry,v=g.attributes.position,index=g.index;
            const points=Array.from({length:v.count},(_,i)=>mesh.applyBoneTransform(i,new Vector3().fromBufferAttribute(v,i)));
            for(let i=0;i<index.count;i+=3){const [a,b,c]=[0,1,2].map(k=>points[index.getX(i+k)]);const area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);assert.ok(area>0,`${mesh.name} frame=${frame} triangle=${i/3} area=${area}`);}
        }
    }}finally{rig.dispose();}
});
test('Paw contact vertices do not move during stand-sit',()=>{
    const rig=createTransitionRig(manifest,null);let checked=0;
    try{for(const mesh of rig.meshes.filter(m=>/Front|Hind/.test(m.name))){const v=mesh.geometry.attributes.position;
        for(let i=0;i<v.count;i++){const original=new Vector3().fromBufferAttribute(v,i);if(256-original.y<487)continue;
            for(const p of [0,.5,1]){rig.apply(p);const current=mesh.applyBoneTransform(i,original.clone());assert.ok(current.distanceTo(original)<1e-4);checked++;}
        }}assert.ok(checked>20);}finally{rig.dispose();}
});
test('Sampling is periodic, deterministic, reversible and rejects nonfinite input',()=>{
    for(const t of [0,.3,1.2,2.5,4.2]){assert.ok(Math.abs(sampleTransition(t).progress-sampleTransition(t+DURATION).progress)<1e-10);}
    assert.equal(sampleTransition(0).progress,0);assert.equal(sampleTransition(2.5).progress,1);
    assert.deepEqual(controlsAt(.5),controlsAt(.5));assert.throws(()=>controlsAt(NaN));assert.throws(()=>sampleTransition(Infinity));
});

test('Hind chains retain anatomical hierarchy, fixed lengths and unit scales through 101 poses',()=>{
    const rig=createTransitionRig(manifest,null),world=b=>b.getWorldPosition(new Vector3());
    try {
        const chains=['near','far'].map(side=>['Upper','Lower','Hock','Foot'].map(j=>rig.bones.find(b=>b.name===`${side}Hind${j}`)));
        rig.apply(0);
        const lengths=chains.map(chain=>chain.slice(1).map((b,i)=>world(b).distanceTo(world(chain[i]))));
        for(let frame=0;frame<=100;frame++) {
            rig.apply(frame/100);
            assert.ok(rig.bones.every(b=>b.scale.equals(new Vector3(1,1,1))));
            for(const [side,chain] of chains.entries()) {
                for(let i=1;i<chain.length;i++) {
                    assert.equal(chain[i].parent,chain[i-1]);
                    assert.ok(Math.abs(world(chain[i]).distanceTo(world(chain[i-1]))-lengths[side][i-1])<1e-9);
                }
                const target=rig.bones.find(b=>b.name===`${side===0?'near':'far'}HindTarget`);
                assert.ok(world(chain[3]).distanceTo(world(target))<.002,'IK endpoint must meet the fixed paw within 0.002 source pixels');
                assert.ok(chain[0].rotation.z<=0&&chain[1].rotation.z>=0&&chain[2].rotation.z>=0);
                assert.ok(world(chain[1]).x<world(chain[0]).x,'knee remains in front of the hip');
                assert.ok(world(chain[2]).x>world(chain[1]).x,'hock folds behind the knee');
            }
        }
        assert.ok(chains.every(c=>c[0].rotation.z<-.6&&c[1].rotation.z>1&&c[2].rotation.z>.7));
    } finally {rig.dispose();}
});

test('Thigh attachments preserve every edge length and triangle area instead of squashing',()=>{
    const rig=createTransitionRig(manifest,null);
    const points=mesh=>Array.from({length:mesh.geometry.attributes.position.count},(_,i)=>mesh.applyBoneTransform(i,new Vector3().fromBufferAttribute(mesh.geometry.attributes.position,i)));
    const area=(a,b,c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
    try {
        rig.apply(0);
        const thighs=rig.meshes.filter(m=>m.name.endsWith('Thigh')),original=thighs.map(points);
        assert.equal(thighs.length,2);
        for(let frame=0;frame<=100;frame++) {
            rig.apply(frame/100);
            for(const [n,mesh] of thighs.entries()) {
                const p=points(mesh),rest=original[n],indices=mesh.geometry.index;
                for(let i=0;i<indices.count;i+=3) {
                    const [a,b,c]=[0,1,2].map(k=>indices.getX(i+k));
                    assert.ok(Math.abs(area(p[a],p[b],p[c])-area(rest[a],rest[b],rest[c]))<1e-8);
                    assert.ok(Math.abs(p[a].distanceTo(p[b])-rest[a].distanceTo(rest[b]))<1e-9);
                }
            }
        }
    } finally {rig.dispose();}
});

test('IK seeking resets to a deterministic pose, including after reverse and repeated playback',()=>{
    const rig=createTransitionRig(manifest,null),snapshot=()=>Array.from(rig.skeleton.boneMatrices);
    try {
        rig.apply(.5);const expected=snapshot();
        for(let i=0;i<=100;i++)rig.apply(i/100);
        for(let i=100;i>=0;i--)rig.apply(i/100);
        rig.apply(.5);assert.deepEqual(snapshot(),expected);
        rig.apply(.5);assert.deepEqual(snapshot(),expected);
        assert.throws(()=>rig.apply(-.1));assert.throws(()=>rig.apply(1.1));
    } finally {rig.dispose();}
});
