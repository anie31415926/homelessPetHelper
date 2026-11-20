import fs from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { createRequire } from 'node:module';
import { packAtlasRegions, composeAtlas } from './lib/image-atlas.mjs';
import { prepareAtlasImage } from './lib/atlas-padding.mjs';

const require = createRequire(import.meta.url);
export const inputs = {
    original:'public/pet-mesh/cat-side.png', body:'public/pet-motion/body.png',
    'near-front':'public/pet-motion/near-front.png', 'near-hind':'public/pet-motion/near-hind.png',
    'far-front-copy':'public/pet-limbs/far-front-copy.png', 'far-hind-copy':'public/pet-limbs/far-hind-copy.png',
    'sit-rest':'public/pet-motion/sit-rest.png', 'side-rest':'public/pet-motion/side-rest.png',
};
const folder = 'unity/HomelessPetHelper/Assets/Art/PetMotion';
const metadataPath = 'art-source/pet-motion/atlas.json';
const atlasPath = `${folder}/T_Pet_Atlas.png`;

export async function buildAtlas() {
    const sharp = require('sharp');
    const regions = [], buffers = new Map();
    for (const [id,input] of Object.entries(inputs)) {
        const bytes = await fs.readFile(input);
        const {image, sourceWidth, sourceHeight, left, top, width, height} = await prepareAtlasImage(sharp, bytes);
        regions.push({id,input,sourceWidth,sourceHeight,left,top,width,height,
            sha256:createHash('sha256').update(bytes).digest('hex')});
        buffers.set(id, image);
    }
    const best=packAtlasRegions(regions);
    const bytes=await composeAtlas(sharp,best,buffers);
    const metadata={schema:1,padding:2,texture:'Atlas',...best};
    const json=JSON.stringify(metadata,null,2)+'\n';
    if(process.argv.includes('--check')) {
        if(!bytes.equals(await fs.readFile(atlasPath)) || json!==await fs.readFile(metadataPath,'utf8'))
            throw new Error('Stale pet atlas; run tools/pet-motion-atlas.mjs');
    } else {
        await fs.writeFile(atlasPath,bytes);
        await fs.writeFile(metadataPath,json);
    }
    console.log(JSON.stringify({atlasPath,width:best.width,height:best.height,bytes:bytes.length,
        rgba32Bytes:best.width*best.height*4,occupancy:regions.reduce((n,r)=>n+r.width*r.height,0)/(best.width*best.height)}));
    return metadata;
}

// Keep source pixel coordinates and all retained skin weights; only UVs and indices change.
export function trimRig(rig, atlas) {
    const region=atlas.regions.find(r=>r.id===rig.texture);
    if(!region) throw new Error(`Missing atlas region: ${rig.texture}`);
    const localUV=[];
    for(let i=0;i<rig.uv.length;i+=2) {
        localUV.push((rig.uv[i]*region.sourceWidth-region.left)/region.width,
            (rig.uv[i+1]*region.sourceHeight-(region.sourceHeight-region.top-region.height))/region.height);
    }
    const triangles=[];
    for(let i=0;i<rig.triangles.length;i+=3) {
        const tri=rig.triangles.slice(i,i+3), xs=tri.map(v=>localUV[v*2]),ys=tri.map(v=>localUV[v*2+1]);
        if(Math.max(...xs)>=0 && Math.min(...xs)<=1 && Math.max(...ys)>=0 && Math.min(...ys)<=1) triangles.push(...tri);
    }
    const sourceVertices=[...new Set(triangles)].sort((a,b)=>a-b);
    const remap=new Map(sourceVertices.map((old,i)=>[old,i]));
    const take=(data,size)=>sourceVertices.flatMap(i=>data.slice(i*size,(i+1)*size));
    const cropUV=take(localUV,2),uv=[];
    for(let i=0;i<cropUV.length;i+=2) uv.push((region.x+cropUV[i]*region.width)/atlas.width,
        (atlas.height-region.y-region.height+cropUV[i+1]*region.height)/atlas.height);
    const round=v=>Math.round(v*1e7)/1e7;
    return {...rig,texture:'Atlas',positions:take(rig.positions,3),uv:uv.map(round),cropUV:cropUV.map(round),
        indices:take(rig.indices,4),weights:take(rig.weights,4),triangles:triangles.map(i=>remap.get(i)),sourceVertices};
}

if(process.argv[1]?.replaceAll('\\','/').endsWith('/pet-motion-atlas.mjs')) await buildAtlas();
