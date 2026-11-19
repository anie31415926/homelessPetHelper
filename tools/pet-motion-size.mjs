import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ? {paths:[process.env.CODEX_NODE_MODULES]} : undefined));
const inputs = {
    original:'public/pet-mesh/cat-side.png', body:'public/pet-motion/body.png',
    'near-front':'public/pet-motion/near-front.png', 'near-hind':'public/pet-motion/near-hind.png',
    'far-front-copy':'public/pet-limbs/far-front-copy.png', 'far-hind-copy':'public/pet-limbs/far-hind-copy.png',
    'sit-rest':'public/pet-motion/sit-rest.png', 'side-rest':'public/pet-motion/side-rest.png',
};
const rows = [];
for (const [id,input] of Object.entries(inputs)) {
    const bytes = await fs.readFile(input), imported = await fs.readFile(`unity/HomelessPetHelper/Assets/EditorScenes/PetMotion/T_Pet_${id}.png`);
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
const report = {scope:'8 current preview textures, originals untouched; trim is analytical only, no UV changes',rows,
    totals: rows.reduce((t,r)=>{for(const k of ['bytes','rgba32Bytes','croppedRgba32Bytes']) t[k]+=r[k];return t;},
        {bytes:0,rgba32Bytes:0,croppedRgba32Bytes:0})};
await fs.mkdir('test-results/pet-motion',{recursive:true});
await fs.writeFile('test-results/pet-motion/source-size.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
