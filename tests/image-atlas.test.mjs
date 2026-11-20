import test from 'node:test';
import assert from 'node:assert/strict';
import sharp from 'sharp';
import { packAtlasRegions, composeAtlas } from '../tools/lib/image-atlas.mjs';

test('packing is deterministic, bounded, aligned and does not mutate or rotate input',()=>{
    const inputs=[{id:'large',width:31,height:67,offset:[7,9]},{id:'small',width:13,height:11},
        {id:'wide',width:70,height:28},{id:'tall',width:19,height:81}];
    const snapshot=structuredClone(inputs),layout=packAtlasRegions(inputs,{maxSize:128});
    assert.deepEqual(layout,packAtlasRegions(inputs,{maxSize:128}));
    assert.deepEqual(inputs,snapshot);
    assert.equal(layout.width%4,0); assert.equal(layout.height%4,0);
    assert.ok(layout.width<=128 && layout.height<=128);
    for(const r of layout.regions) {
        assert.equal(r.width,inputs.find(x=>x.id===r.id).width);
        assert.equal(r.height,inputs.find(x=>x.id===r.id).height);
        for(const s of layout.regions) if(r!==s)
            assert.ok(r.x>=s.x+s.width || s.x>=r.x+r.width || r.y>=s.y+s.height || s.y>=r.y+r.height);
    }
    assert.deepEqual(layout.regions[0].offset,[7,9]);
});

test('packing rejects invalid, duplicate, empty, oversized and non-fitting regions',()=>{
    for(const regions of [[],[{id:1,width:1,height:1}],[{id:'x',width:0,height:1}],[{id:'x',width:1.1,height:2}],
        [{id:'x',width:129,height:1}],[{id:'x',width:1,height:1},{id:'x',width:1,height:1}],
        [{id:'a',width:100,height:100},{id:'b',width:100,height:100}]])
        assert.throws(()=>packAtlasRegions(regions,{maxSize:128}));
    assert.throws(()=>packAtlasRegions([{id:'a',width:1,height:1}],{alignment:0}));
});

const encode=async bytes=>sharp(Buffer.from(bytes),{raw:{width:2,height:1,channels:4}}).png().toBuffer();
test('shared compositor preserves RGBA exactly including alpha=0/1/127 and fixed grid coordinates',async()=>{
    const a=[43,217,69,0,97,23,199,1],b=[189,71,51,127,11,31,47,255];
    const images=new Map([['a',await encode(a)],['b',await encode(b)]]);
    const layout={width:5,height:3,regions:[{id:'a',x:0,y:0,width:2,height:1},{id:'b',x:3,y:2,width:2,height:1}]};
    const output=await composeAtlas(sharp,layout,images);
    const pixels=await sharp(output).raw().toBuffer();
    assert.deepEqual([...pixels.subarray(0,8)],a);
    assert.deepEqual([...pixels.subarray((2*5+3)*4)],b);
    assert.ok(pixels.subarray(8,(2*5+3)*4).every(v=>v===0));
    assert.deepEqual(output,await composeAtlas(sharp,layout,images));
});

test('compositor rejects missing images, mismatched sizes, overlaps and out-of-page layouts',async()=>{
    const images=new Map([['a',await encode([1,2,3,255,4,5,6,255])]]);
    const region={id:'a',x:0,y:0,width:2,height:1};
    await assert.rejects(composeAtlas(sharp,{width:4,height:4,regions:[{...region,id:'missing'}]},images),/Missing/);
    await assert.rejects(composeAtlas(sharp,{width:4,height:4,regions:[{...region,width:1}]},images),/size mismatch/);
    await assert.rejects(composeAtlas(sharp,{width:4,height:4,regions:[region,{...region,id:'b'}]},images),/Overlapping/);
    await assert.rejects(composeAtlas(sharp,{width:4,height:4,regions:[{...region,x:3}]},images),/outside/);
});
