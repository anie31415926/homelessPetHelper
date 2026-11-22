import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
const base='art-source/ui-soft-20261122', runtime='unity/HomelessPetHelper/Assets/Art/UI';
const digest=b=>crypto.createHash('sha256').update(b).digest('hex');
const descriptors=[], layers=[], records=[], cuts={};
async function extract(slug,cell){
 const source=await fs.readFile(`${base}/${slug}/master.png`);
 const input=cell?await sharp(source).extract(cell).png().toBuffer():source;
 const {data,info}=await sharp(input).removeAlpha().raw().toBuffer({resolveWithObject:true});
 const {width:w,height:h}=info, mask=new Uint8Array(w*h), q=new Int32Array(w*h);
 const purple=[(10*w+10)*3,(10*w+w-11)*3,((h-11)*w+10)*3,((h-11)*w+w-11)*3].some(i=>data[i]>data[i+1]+30&&data[i+2]>data[i+1]+30);
 const background=i=>purple?(data[i*3]>data[i*3+1]+14&&data[i*3+2]>data[i*3+1]+14):Math.min(...data.subarray(i*3,i*3+3))>=245;
 if(cell){for(let i=0;i<w*h;i++)mask[i]=background(i)?0:1;}
 else {
  let head=0,tail=0;
  function visit(i){if(mask[i]||!background(i))return;mask[i]=2;q[tail++]=i;}
  for(let x=0;x<w;x++){visit(x);visit((h-1)*w+x);}
  for(let y=0;y<h;y++){visit(y*w);visit(y*w+w-1);}
  while(head<tail){const i=q[head++],x=i%w,y=Math.floor(i/w);if(x)visit(i-1);if(x+1<w)visit(i+1);if(y)visit(i-w);if(y+1<h)visit(i+w);}
  for(let i=0;i<w*h;i++)mask[i]=mask[i]===2?0:1;
 }
 // Keep the main silhouette, removing isolated key-color noise and disconnected marks.
 const visited=new Uint8Array(w*h);let largest=[];const pieces=[];
 for(let start=0;start<w*h;start++){
  if(!mask[start]||visited[start])continue;
  let head=0,tail=1;q[0]=start;visited[start]=1;
  while(head<tail){const i=q[head++],x=i%w,y=Math.floor(i/w);
   for(const n of [x?i-1:-1,x+1<w?i+1:-1,y?i-w:-1,y+1<h?i+w:-1])
    if(n>=0&&mask[n]&&!visited[n]){visited[n]=1;q[tail++]=n;}
  }
  if(tail>largest.length)largest=Array.from(q.subarray(0,tail));
  if(cell&&tail>=40)pieces.push(Array.from(q.subarray(0,tail)));
 }
 // Separated icon pieces (paw toes, pause bars, sound waves) must all remain.
 mask.fill(0);for(const i of (cell?pieces.flat():largest))mask[i]=1;
 const rgba=Buffer.alloc(w*h*4);
 for(let i=0;i<w*h;i++){
  data.copy(rgba,i*4,i*3,i*3+3);rgba[i*4+3]=mask[i]?255:0;
  if(purple&&mask[i]&&data[i*3+2]>data[i*3+1]+8&&data[i*3]>data[i*3+1]+6)
   rgba[i*4]=rgba[i*4+1]=rgba[i*4+2]=250;
 }
 const result=await sharp(rgba,{raw:{width:w,height:h,channels:4}}).trim({background:'#00000000',threshold:0}).png().toBuffer({resolveWithObject:true});
 return {buffer:result.data,record:{source:`${base}/${slug}/master.png`,sourceSha256:digest(source),cell:cell??null,
  exterior:purple?'connected purple-key removal; pale magenta fringe decontamination':'connected near-white >=245 removal',
  separatedPartsRetained:!!cell,crop:{x:-(result.info.trimOffsetLeft??0),y:-(result.info.trimOffsetTop??0),width:result.info.width,height:result.info.height}}};
}
function place(name,buffer,x,y,w,h,border,record){layers.push({input:buffer,left:x,top:y});const [a,b,c,d]=border;
 descriptors.push({Name:name,X:x,Y:y,Width:w,Height:h,Border:{x:a,y:b,z:c,w:d}});records.push({name,...record,output:{x,y,width:w,height:h}});}
async function add(name,slug,x,y,w,h,border,cell){const cut=await extract(slug,cell);cuts[slug]=cut;
 const buf=await sharp(cut.buffer).resize(w,h,{fit:'fill',kernel:'lanczos3'}).png().toBuffer();place(name,buf,x,y,w,h,border,cut.record);}
await add('Paper','paper',4,4,252,252,[52,52,52,52]);
await add('Action','primary',264,4,240,72,[36,32,36,32]);
await add('Secondary','secondary',264,84,240,72,[36,32,36,32]);
for(const [i,name] of ['Pause','Objectives','Settings','Home','Paw','Play','Music','Speaker'].entries()){
 const cell={left:i%4*256,top:Math.floor(i/4)*256,width:256,height:256};
 const cut=await extract('symbols',cell);
 const buf=await sharp(cut.buffer).resize(56,56,{fit:'contain',background:'#00000000',kernel:'lanczos3'}).png().toBuffer();
 place(name,buf,264+i%4*60,168+Math.floor(i/4)*64,56,56,[0,0,0,0],cut.record);
}
await add('VolumeThumb','knob',4,264,72,72,[0,0,0,0]);
for(const [name,y,saturation,brightness] of [['VolumeTrack',296,.2,.92],['VolumeFill',328,1.1,.84]]){
 const cut=cuts.primary,meta=await sharp(cut.buffer).metadata();
 const center=await sharp(cut.buffer).extract({left:Math.floor(meta.width*.2),top:Math.floor(meta.height*.28),width:Math.floor(meta.width*.6),height:Math.floor(meta.height*.44)}).resize(240,24,{fit:'fill'}).modulate({saturation,brightness}).png().toBuffer();
 const capsule=Buffer.from('<svg width="240" height="24"><rect width="240" height="24" rx="12" fill="white"/></svg>');
 const buf=await sharp(center).composite([{input:capsule,blend:'dest-in'}]).png().toBuffer();
 place(name,buf,84,y,240,24,[12,12,12,12],{...cut.record,derivative:'center texture cropped from generated primary, resized to dedicated capsule rail; SVG alpha silhouette only',modulate:{saturation,brightness}});
}
const atlas=await sharp({create:{width:512,height:360,channels:4,background:'#00000000'}}).composite(layers).png({palette:true,colours:256,effort:10,dither:0}).toBuffer();
await fs.writeFile(`${runtime}/T_UI_LevelControls.png`,atlas);
await fs.writeFile(`${runtime}/LevelControlsSlices.json`,JSON.stringify({Width:512,Height:360,Sprites:descriptors},null,2)+'\n');
await fs.writeFile(`${base}/derivation.json`,JSON.stringify({atlas:`${runtime}/T_UI_LevelControls.png`,width:512,height:360,bytes:atlas.length,sha256:digest(atlas),processing:'Background extraction, trim, full-color preservation, Lanczos resize and 256-color shared page; 14 independent slices, 4px minimum gutters; no baked text.',sprites:records},null,2)+'\n');
await sharp(atlas).flatten({background:'#647468'}).resize(1024,720,{kernel:'nearest'}).jpeg({quality:90}).toFile(`${base}/edge-review.jpg`);
const file='art-source/resource-catalog.json',cat=JSON.parse(await fs.readFile(file,'utf8'));
const asset=cat.assets.find(a=>a.id==='ui.level.controls');
asset.status='temporary';asset.use='清新可爱14切片控件页512×360：奶白纸卡、嫩绿/浅杏圆角按钮、8个全彩图标、专用轨道/填充/圆形爪印滑钮；保留原GUID，九宫格与独立文字，替换旧11切片页。';
asset.files=[{role:'runtime',path:`${runtime}/T_UI_LevelControls.png`},{role:'workflow',path:`${runtime}/LevelControlsSlices.json`},{role:'workflow',path:`${base}/derivation.json`},{role:'reference',path:`${base}/edge-review.jpg`},{role:'reference',path:'art-source/ui-watercolor-20261122/edge-review.jpg'},{role:'workflow',path:'art-source/ui-watercolor-20261122/derivation.json'}];
asset.generation={tool:'本机ComfyUI Qwen Image 2.1 INT8 ConvRot + Sharp派生',record:`${base}/README.md`,notes:'用户要求重新生成；5次真实作业母图及完整提示/参考/参数/历史见本批ui.soft条目；derive.mjs记录去底、裁切、保持彩色、专用轨道派生、切片和纹理哈希。旧母图在ui-watercolor-20261122保留。'};
asset.review={visual:'pending',rights:'pending',target:'pending'};delete asset.reviewedSha256;
if(!cat.assets.some(a=>a.id==='ui.soft.edge-review'))cat.assets.push({id:'ui.soft.edge-review',name:'清新UI深底边缘预览',module:'CommonUI',status:'candidate',loadTiming:'preview-only',availability:'仅检查抠图边缘，非运行素材',use:'512×360共享页2倍预览，单独记录派生来源',plannedBytes:131072,files:[{role:'reference',path:`${base}/edge-review.jpg`}],generation:{tool:'Sharp',record:`${base}/derivation.json`},review:{visual:'pending',rights:'pending',target:'pending'}});
await fs.writeFile(file,JSON.stringify(cat,null,2)+'\n');console.log({bytes:atlas.length,sprites:descriptors.length,sha256:digest(atlas)});
