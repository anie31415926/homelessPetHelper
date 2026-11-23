import {createHash} from 'node:crypto';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import sharp from 'sharp';
import {packAtlasRegions, composeAtlas} from './lib/image-atlas.mjs';

const base='art-source/pet-transition-20261123', output='public/pet-transition';
const check=process.argv.includes('--check');
if(!check){await mkdir(output,{recursive:true}); await mkdir(`${base}/layers`,{recursive:true});}
const width=452,height=512,raw={width,height,channels:4};
const source='public/pet-mesh/cat-side.png';
const original=await sharp(source).ensureAlpha().raw().toBuffer();
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
const plan=JSON.parse(await readFile(`${base}/plan.json`,'utf8'));
if(hash(await readFile(source))!==plan.referenceSha256)throw new Error('Identity source changed; review plan before rebuilding');
async function save(path,bytes){if(check){if(!Buffer.from(await readFile(path)).equals(Buffer.from(bytes)))throw new Error(`Generated output is stale: ${path}`);}else await writeFile(path,bytes);}
const paths={
    head:'M0 0 H452 V244 H240 L226 269 L224 290 L92 320 L86 342 H0Z',
    torso:'M95 291 Q168 286 218 277 Q320 272 364 318 Q394 350 393 390 Q370 424 319 432 Q231 451 166 414 Q105 380 95 291Z',
    tail:'M367 344 Q402 366 401 428 Q401 447 429 450 Q452 455 443 481 Q433 508 401 494 Q377 489 364 464 Q352 431 359 398Z',
};
const hindMasks={
    thigh:'M289 318 Q330 320 353 362 Q365 405 342 433 Q329 448 309 449 Q270 448 253 425 Q239 389 256 350 Q269 326 289 318Z',
    shin:'M316 432 Q337 431 344 441 Q346 451 334 465 L311 481 Q294 488 281 479 Q277 468 291 456Z',
};
async function maskFor(path,translate=[0,0]) {
    return sharp(Buffer.from(`<svg width="452" height="512"><path transform="translate(${translate.join(' ')})" fill="white" d="${path}"/></svg>`)).ensureAlpha().raw().toBuffer();
}
const layers=[];
async function layer(id,data,maskPath,sourcePath) {
    const pixels=Buffer.from(data);
    if(id==='nearFront'||id==='nearHindThigh') {
        const start=id==='nearFront'?320:315,end=id==='nearFront'?380:375;
        for(let y=0;y<height;y++) for(let x=0;x<width;x++) {
            const p=(y*width+x)*4,t=Math.max(0,Math.min(1,(y-start)/(end-start)));
            pixels[p+3]=Math.round(pixels[p+3]*t*t*(3-2*t));
        }
    }
    if(maskPath) {
        const mask=await maskFor(maskPath);
        for(let p=0;p<pixels.length;p+=4) pixels[p+3]=Math.round(pixels[p+3]*mask[p+3]/255);
    }
    const full=await sharp(pixels,{raw}).png().toBuffer();
    await save(`${base}/layers/${id}.png`,full);
    let left=width,top=height,right=0,bottom=0;
    for(let y=0;y<height;y++) for(let x=0;x<width;x++) if(pixels[(y*width+x)*4+3]>0) {
        left=Math.min(left,x);top=Math.min(top,y);right=Math.max(right,x);bottom=Math.max(bottom,y);
    }
    const crop={left:Math.max(0,left-2),top:Math.max(0,top-2)};
    crop.width=Math.min(width-1,right+2)-crop.left+1;crop.height=Math.min(height-1,bottom+2)-crop.top+1;
    const png=await sharp(pixels,{raw}).extract(crop).png().toBuffer();
    layers.push({id,png,crop,source:sourcePath,sha256:hash(png)});
}
async function hindLayers(id,sourcePath,offset) {
    const source=await sharp(sourcePath).ensureAlpha().raw().toBuffer();
    const smooth=t=>{t=Math.max(0,Math.min(1,t));return t*t*(3-2*t);};
    const translate=id==='nearHind'?[0,0]:[-30,-offset];
    const thigh=await maskFor(hindMasks.thigh,translate);
    const shin=await maskFor(hindMasks.shin,translate);
    for(const part of ['Shin','Paw','Thigh']) {
        const pixels=Buffer.from(source);
        for(let y=0;y<height;y++)for(let x=0;x<width;x++) {
            const sy=y+offset,p=(y*width+x)*4;
            if(part==='Thigh') {
                // Close the hidden round end with nearby source coat, never stretch the visible thigh.
                if(thigh[p+3]>0&&source[p+3]<240&&sy>410) {
                    let donor=y;while(donor>380-offset&&source[(donor*width+x)*4+3]<240)donor--;
                    const q=(donor*width+x)*4;
                    if(source[q+3]>=240){for(let c=0;c<3;c++)pixels[p+c]=source[q+c];pixels[p+3]=255;}
                }
                pixels[p+3]=Math.round(pixels[p+3]*thigh[p+3]/255);
            } else {
                const alpha=part==='Paw'?smooth((sy-452)/13):shin[p+3]/255;
                pixels[p+3]=Math.round(pixels[p+3]*alpha);
            }
        }
        await layer(`${id}${part}`,pixels,null,sourcePath);
    }
}
await layer('tail',original,paths.tail,source);
await hindLayers('farHind','public/pet-limbs/far-hind-copy.png',6);
await layer('farFront',await sharp('public/pet-limbs/far-front-copy.png').ensureAlpha().raw().toBuffer(),null,'public/pet-limbs/far-front-copy.png');
const torso=await sharp('public/pet-motion/body.png').ensureAlpha().raw().toBuffer();
// A selected completion may replace only the lower torso, never the shared head or original upper coat.
let completion=null;
{
    const selected=JSON.parse(await readFile(`${base}/selection.json`,'utf8'));
    if(hash(await readFile(selected.path))!==selected.sha256)throw new Error('Selected completion changed; review before rebuilding');
    const donor=await sharp(selected.path).resize(330,242,{fit:'fill'}).ensureAlpha().raw().toBuffer();
    for(let y=330;y<445;y++) for(let x=100;x<393;x++) {
        const p=(y*width+x)*4,q=((y-270)*330+x-74)*4;
        const [r,g,b]=donor.subarray(q,q+3);
        if(g-r>12&&b-r>14) continue;
        const t=Math.max(0,Math.min(1,(y-330)/36));
        for(let c=0;c<3;c++) torso[p+c]=Math.round(torso[p+c]*(1-t)+donor[q+c]*t);
        torso[p+3]=255;
    }
    completion=selected;
}
await layer('torso',torso,paths.torso,completion?.path||'public/pet-motion/body.png');
await hindLayers('nearHind','art-source/pet-limbs/donor/hind-source.png',0);
await layer('nearFront',await sharp('art-source/pet-limbs/donor/front-source.png').ensureAlpha().raw().toBuffer(),null,'art-source/pet-limbs/donor/front-source.png');
await layer('head',original,paths.head,source);
const layout=packAtlasRegions(layers.map(l=>({id:l.id,width:l.crop.width,height:l.crop.height})),{maxSize:1024});
const png=await composeAtlas(sharp,layout,new Map(layers.map(l=>[l.id,l.png])),{compressionLevel:9});
await save(`${output}/atlas.png`,png);
const manifest={schemaVersion:1,width:layout.width,height:layout.height,originalSize:[width,height],source,sourceSha256:hash(await readFile(source)),completion,
    layers:layout.regions.map(r=>({...r,...layers.find(l=>l.id===r.id).crop,atlasX:r.x,atlasY:r.y,source:layers.find(l=>l.id===r.id).source})),
    masks:paths,hindAttachments:{masks:hindMasks,farOffset:[-30,-6],hiddenEndFill:'Only inside thigh mask below source y410: copy nearest opaque coat above in the same source column. Paw fades in over y452..465; shin is an overlapping hock attachment.'},
    method:'Same source head/scarf/limbs, rigid thigh/paw attachments, fixed-length hind chains and explicit local torso patch; no full-pose crossfade.'};
await save(`${output}/atlas.json`,JSON.stringify(manifest,null,2));
await save(`${base}/derivation.json`,JSON.stringify({...manifest,atlasSha256:hash(png),layers:layers.map(({png,...record})=>record)},null,2));
console.log(JSON.stringify({atlas:[layout.width,layout.height],bytes:png.length,completion}));
