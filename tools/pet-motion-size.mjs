import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { inputs } from './pet-motion-atlas.mjs';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? {paths:[process.env.CODEX_NODE_MODULES]} : undefined));
const rows = [];
for (const [id,input] of Object.entries(inputs)) {
    const bytes = await fs.readFile(input), imported = await fs.readFile(`unity/HomelessPetHelper/Assets/Art/PetMotion/T_Pet_${id}.png`);
    if (!bytes.equals(imported)) throw new Error(`Unity source changed: ${id}`);
    const {data,info:{width,height}} = await sharp(bytes).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    let left=width,top=height,right=0,bottom=0,occupied=0;
    for (let y=0;y<height;y++) for (let x=0;x<width;x++) if(data[(y*width+x)*4+3]>0) {
        occupied++;left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);
    }
    const crop={left,top,width:right-left+1,height:bottom-top+1};
    rows.push({id,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex'),width,height,
        rgba32Bytes:width*height*4,occupiedFraction:occupied/(width*height),crop,
        croppedRgba32Bytes:crop.width*crop.height*4});
}
const atlas=JSON.parse(await fs.readFile('art-source/pet-motion/atlas.json','utf8'));
const atlasBytes=await fs.readFile('unity/HomelessPetHelper/Assets/Art/PetMotion/T_Pet_Atlas.png');
const exported=JSON.parse(await fs.readFile('art-source/pet-motion/unity-export.json','utf8'));
const report = {schema:2,scope:'Originals unchanged for HTML/reference; Unity preview references only the cropped shared atlas. Not target memory/download.',rows,
    optimized:{width:atlas.width,height:atlas.height,sourceBytes:atlasBytes.length,rgba32Bytes:atlas.width*atlas.height*4,
        sha256:createHash('sha256').update(atlasBytes).digest('hex'),
        skinVertices:exported.rigs.reduce((n,r)=>n+r.positions.length/3,0),
        skinTriangles:exported.rigs.reduce((n,r)=>n+r.triangles.length/3,0),
        curveCount:exported.takes.reduce((n,t)=>n+t.curves.length,0),
        keyCount:exported.takes.reduce((n,t)=>n+t.curves.reduce((m,c)=>m+c.times.length,0),0)},
    totals: rows.reduce((t,r)=>{for(const k of ['bytes','rgba32Bytes','croppedRgba32Bytes']) t[k]+=r[k];return t;},
        {bytes:0,rgba32Bytes:0,croppedRgba32Bytes:0})};
await fs.mkdir('test-results/pet-motion',{recursive:true});
await fs.writeFile('test-results/pet-motion/source-size.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
