import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
const base='art-source/ui-watercolor-20261122';
const runtime='unity/HomelessPetHelper/Assets/Art/UI';
await fs.mkdir(runtime,{recursive:true});
const hash=data=>crypto.createHash('sha256').update(data).digest('hex');
const descriptors=[],composites=[],records=[];
async function removeWhite(input, glyph=false){
 const {data,info}=await sharp(input).removeAlpha().raw().toBuffer({resolveWithObject:true});
 const {width:w,height:h}=info;
 const rgba=Buffer.alloc(w*h*4),outside=new Uint8Array(w*h),queue=new Int32Array(w*h);
 if(!glyph){
  let head=0,tail=0;
  function visit(i){if(outside[i]||Math.min(data[i*3],data[i*3+1],data[i*3+2])<244)return;outside[i]=1;queue[tail++]=i;}
  for(let x=0;x<w;x++){visit(x);visit((h-1)*w+x);}
  for(let y=0;y<h;y++){visit(y*w);visit(y*w+w-1);}
  while(head<tail){const i=queue[head++],x=i%w,y=Math.floor(i/w);if(x>0)visit(i-1);if(x+1<w)visit(i+1);if(y>0)visit(i-w);if(y+1<h)visit(i+w);}
 }
 for(let i=0;i<w*h;i++){
  const min=Math.min(data[i*3],data[i*3+1],data[i*3+2]);
  for(let c=0;c<3;c++)rgba[i*4+c]=glyph?255:data[i*3+c];
  const alpha=Math.round(Math.max(0,Math.min(1,(246-min)/180))*255);
  rgba[i*4+3]=glyph?(alpha<24?0:alpha):outside[i]?0:255;
 }
 const image=sharp(rgba,{raw:{width:w,height:h,channels:4}});
 const trimmed=await image.trim({background:'#00000000',threshold:0}).png().toBuffer({resolveWithObject:true});
 return {buffer:trimmed.data,crop:{x:-(trimmed.info.trimOffsetLeft??0),y:-(trimmed.info.trimOffsetTop??0),width:trimmed.info.width,height:trimmed.info.height}};
}
async function add(name,slug,x,y,w,h,border,extract){
 const source=await fs.readFile(`${base}/${slug}/master.png`);
 const input=extract?await sharp(source).extract(extract).png().toBuffer():source;
 const cut=await removeWhite(input,!!extract);
 const buffer=await sharp(cut.buffer).resize(w,h,{fit:'contain',background:'#00000000',kernel:'lanczos3'}).png().toBuffer();
 composites.push({input:buffer,left:x,top:y});
 const [a,b,c,d]=border;
 descriptors.push({Name:name,X:x,Y:y,Width:w,Height:h,Border:{x:a,y:b,z:c,w:d}});
 records.push({name,source:`${base}/${slug}/master.png`,sourceSha256:hash(source),inputCell:extract??null,crop:cut.crop,output:{x,y,width:w,height:h},monochromeTintMask:!!extract});
}
await add('Paper','paper',4,4,252,252,[36,36,36,36]);
await add('Action','primary',264,4,240,64,[22,18,22,18]);
await add('Secondary','secondary',264,76,240,64,[22,18,22,18]);
const icons=['Pause','Objectives','Settings','Home','Paw','Play','Music','Speaker'];
for(let i=0;i<icons.length;i++)await add(icons[i],'symbols',264+(i%4)*60,152+Math.floor(i/4)*60,56,56,[0,0,0,0],{left:(i%4)*256,top:Math.floor(i/4)*256,width:256,height:256});
const atlas=await sharp({create:{width:512,height:272,channels:4,background:'#00000000'}}).composite(composites).png({palette:true,colours:256,effort:10,dither:0}).toBuffer();
await fs.writeFile(`${runtime}/T_UI_LevelControls.png`,atlas);
const manifest={Width:512,Height:272,Sprites:descriptors};
await fs.writeFile(`${runtime}/LevelControlsSlices.json`,JSON.stringify(manifest,null,2)+'\n');
const provenance={atlas:`${runtime}/T_UI_LevelControls.png`,sha256:hash(atlas),bytes:atlas.length,width:512,height:272,padding:4,processing:'Connected exterior white removal (min channel >=244) for surfaces; white removal also inside glyphs, glyph alpha normalized from ink, coverage below 24/255 removed; trim nonzero alpha; Lanczos contain resize; one 256-color PNG page, no baked labels.',sprites:records};
await fs.writeFile(`${base}/derivation.json`,JSON.stringify(provenance,null,2)+'\n');
await sharp(atlas).flatten({background:'#3c3b34'}).resize(1024,544,{kernel:'nearest'}).jpeg({quality:90}).toFile(`${base}/edge-review.jpg`);
const file='art-source/resource-catalog.json',catalog=JSON.parse(await fs.readFile(file,'utf8'));
const a=catalog.assets.find(a=>a.id==='ui.level.controls');
a.status='temporary';a.use='512×272透明共享控件页，11个切片（3张九宫格/8图标）；4px间隔，鼠尾草绿/奶油纸/暖褐墨。纸底板252档、按钮240档、图标56档；素材文字独立，静态引用计first-play，未接动态加载。';
a.files=[{role:'runtime',path:provenance.atlas},{role:'workflow',path:`${runtime}/LevelControlsSlices.json`},{role:'workflow',path:`${base}/derivation.json`},{role:'reference',path:`${base}/edge-review.jpg`}];
a.generation.notes='由同批4项实际ComfyUI母图派生；母图、提示、模型、参考和实际执行history各见ui.level.paper/primary/secondary/symbols。去白/单色图标蒙版/裁切/缩放/调色板/切片全部见derive.mjs与derivation.json。不代表许可或微信验证。';
await fs.writeFile(file,JSON.stringify(catalog,null,2)+'\n');
console.log(JSON.stringify({atlasBytes:atlas.length,sprites:descriptors.length,dimensions:[512,272],sha256:hash(atlas)}));
