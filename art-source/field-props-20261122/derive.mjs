import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
import { crc32 } from 'node:zlib';
import { packAtlasRegions, composeAtlas } from '../../tools/lib/image-atlas.mjs';
const base='art-source/field-props-20261122';
const jobs=JSON.parse(await fs.readFile(`${base}/jobs.json`,'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const records=[],pieces=new Map();
await fs.mkdir(`${base}/derived`,{recursive:true});
for(const job of jobs){
 const file=`${base}/${job.slug}/master.png`,source=await fs.readFile(file);
 const meta=await sharp(source).metadata();if(meta.width!==768||meta.height!==768)throw Error('Unexpected master dimensions.');
 for(const [index,item] of job.items.entries()){
  const cell={left:index%2*384,top:Math.floor(index/2)*384,width:384,height:384};
  const {data,info}=await sharp(source).extract(cell).removeAlpha().raw().toBuffer({resolveWithObject:true});
  const w=info.width,h=info.height,mask=new Uint8Array(w*h),queue=new Int32Array(w*h);let head=0,tail=0;
  function white(i){return Math.min(data[i*3],data[i*3+1],data[i*3+2])>=244;}
  function visit(i){if(mask[i]||!white(i))return;mask[i]=1;queue[tail++]=i;}
  for(let x=0;x<w;x++){visit(x);visit((h-1)*w+x);}for(let y=0;y<h;y++){visit(y*w);visit(y*w+w-1);}
  while(head<tail){const i=queue[head++],x=i%w,y=Math.floor(i/w);if(x)visit(i-1);if(x+1<w)visit(i+1);if(y)visit(i-w);if(y+1<h)visit(i+w);}
  const rgba=Buffer.alloc(w*h*4);let count=0;
  for(let i=0;i<w*h;i++){if(mask[i])continue;data.copy(rgba,i*4,i*3,i*3+3);rgba[i*4+3]=255;count++;}
  // Unmatte the light antialiased fringe against its original white background.
  for(let y=1;y<h-1;y++)for(let x=1;x<w-1;x++){
   const i=y*w+x;if(mask[i])continue;
   let edge=false;for(let dy=-2;dy<=2;dy++)for(let dx=-2;dx<=2;dx++){
    const xx=x+dx,yy=y+dy;if(xx>=0&&xx<w&&yy>=0&&yy<h&&mask[yy*w+xx])edge=true;
   }
   if(!edge)continue;
   const alpha=Math.min(1,(255-Math.min(data[i*3],data[i*3+1],data[i*3+2]))/42);
   rgba[i*4+3]=Math.round(alpha*255);
   for(let c=0;c<3;c++)rgba[i*4+c]=Math.round(Math.max(0,Math.min(255,(data[i*3+c]-255*(1-alpha))/Math.max(alpha,.001))));
  }
  if(count<500)throw Error(`Empty cell ${item.id}`);
  const cut=await sharp(rgba,{raw:{width:w,height:h,channels:4}}).trim({background:'#00000000',threshold:0}).png().toBuffer({resolveWithObject:true});
  const max=job.slug==='garden'||job.slug==='cleanup'?124:108;
  const resized=await sharp(cut.data).resize({width:max,height:max,fit:'inside',withoutEnlargement:true}).extend({top:2,bottom:2,left:2,right:2,background:'#00000000'}).png().toBuffer({resolveWithObject:true});
  const output=`${base}/derived/${item.id}.png`;await fs.writeFile(output,resized.data);
  pieces.set(item.id,{buffer:resized.data,width:resized.info.width,height:resized.info.height});
  records.push({id:item.id,source:file,sourceSha256:hash(source),cell,removedBackground:'Boundary-connected near-white >=244; white fringe within 2px un-matted using contrast alpha; retained disconnected parts and interior highlights.',crop:cut.info.trimOffsetLeft==null?null:{offsetLeft:cut.info.trimOffsetLeft,offsetTop:cut.info.trimOffsetTop,width:cut.info.width,height:cut.info.height},resize:'Lanczos3 fit inside; 2px alpha border',output,width:resized.info.width,height:resized.info.height,sha256:hash(resized.data)});
 }
}
const groups=[{name:'Garden',ids:jobs.slice(0,2).flatMap(j=>j.items.map(i=>i.id)),palette:'Assets/Settings/Game/FieldPropPalette.asset'}, {name:'Siege',ids:jobs.slice(2).flatMap(j=>j.items.map(i=>i.id)).filter(id=>id!=='spare-leaves'),palette:'Assets/Settings/Siege/SiegePropPalette.asset'}];
const pages=[];
for(const group of groups){
 const layout=packAtlasRegions(group.ids.map(id=>({id,width:pieces.get(id).width+4,height:pieces.get(id).height+4})),{maxSize:512,alignment:4});
 const padded=new Map();for(const id of group.ids)padded.set(id,await sharp(pieces.get(id).buffer).extend({top:2,bottom:2,left:2,right:2,background:'#00000000'}).png().toBuffer());
 const composed=await composeAtlas(sharp,layout,padded);
 const height=layout.height+4;
 const page=await sharp(composed).extend({top:0,bottom:4,left:0,right:0,background:'#00000000'}).composite([{input:Buffer.from([255,255,255,255]),raw:{width:1,height:1,channels:4},left:0,top:height-1}]).png().toBuffer();
 // 4x4 opaque white corner is the (0,0) UV source of existing solid-color mesh primitives.
 const output=await sharp(page).composite([{input:await sharp({create:{width:4,height:4,channels:4,background:'#ffffff'}}).png().toBuffer(),left:0,top:height-4}]).png({palette:true,colours:256,effort:10,dither:0}).toBuffer();
 // Quantization can merge the tiny solid texel with ivory. Restore that palette entry to exact white.
 const decoded=await sharp(output).ensureAlpha().raw().toBuffer();
 const corner=Array.from(decoded.subarray((height-1)*layout.width*4,(height-1)*layout.width*4+3));
 for(let offset=8;offset<output.length;){
  const length=output.readUInt32BE(offset),type=output.toString('ascii',offset+4,offset+8);
  if(type==='PLTE'){
   for(let i=offset+8;i<offset+8+length;i+=3)if(output[i]===corner[0]&&output[i+1]===corner[1]&&output[i+2]===corner[2]){
    output[i]=output[i+1]=output[i+2]=255;
   }
   output.writeUInt32BE(crc32(output.subarray(offset+4,offset+8+length)),offset+8+length);
  }
  offset+=length+12;
 }
 const source=`${base}/T_Outing_${group.name}Props.png`;await fs.writeFile(source,output);
 const slices=layout.regions.map(r=>({id:r.id,x:r.x+2,y:height-r.y-2-pieces.get(r.id).height,width:pieces.get(r.id).width,height:pieces.get(r.id).height}));
 pages.push({source,texture:`Assets/Art/Outing/T_Outing_${group.name}Props.png`,palette:group.palette,width:layout.width,height,bytes:output.length,sha256:hash(output),slices});
}
await fs.writeFile(`${base}/derivation.json`,JSON.stringify({records,pages,whiteCorner:'After quantization, matching PLTE color restored to exact white (CRC recalculated). 4x4 opaque white at UV (0,0), outside all Sprite rects; existing colored procedural effects remain visible.'},null,2)+'\n');
await fs.writeFile(`${base}/unity-import.json`,JSON.stringify({pages},null,2)+'\n');
const catalog=JSON.parse(await fs.readFile('art-source/resource-catalog.json','utf8'));
for(const [i,page]of pages.entries()){
 const entry=catalog.assets.find(a=>a.id===['field-props.garden-atlas','field-props.siege-atlas'][i]);
 entry.status='candidate';entry.files=[{role:'candidate',path:page.source},{role:'workflow',path:`${base}/derivation.json`},{role:'workflow',path:`${base}/unity-import.json`},...page.slices.map(s=>({role:'reference',path:`${base}/derived/${s.id}.png`}))];
 entry.generation.notes='Real ComfyUI masters: 7 sheets with saved workflows/history. Near-white flood, white fringe alpha unmatting, 256-color PNG, retain disconnected pieces, trim/resize/pad, shared MaxRects pack and white UV corner; complete source/output hashes in derivation.json.';
}
const extras=catalog.assets.find(a=>a.id==='field-props.source.mechanisms').files;
if(!extras.some(f=>f.path===`${base}/derived/spare-leaves.png`))extras.push({role:'reference',path:`${base}/derived/spare-leaves.png`});
await fs.writeFile('art-source/resource-catalog.json',JSON.stringify(catalog,null,2)+'\n');
console.log(JSON.stringify(pages.map(p=>({source:p.source,width:p.width,height:p.height,bytes:p.bytes,slices:p.slices.length}))));
