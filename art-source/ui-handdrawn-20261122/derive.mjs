import fs from 'node:fs/promises';
import crypto from 'node:crypto';
import sharp from 'sharp';
const base='art-source/ui-handdrawn-20261122', runtime=base+'/candidate';
await fs.mkdir(runtime,{recursive:true});
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
 const cut=await extract('symbols-v2',cell);
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
const file='art-source/resource-catalog.json', cat=JSON.parse(await fs.readFile(file,'utf8'));
let asset=cat.assets.find(a=>a.id==='ui.handdrawn.controls');
if(!asset){asset={id:'ui.handdrawn.controls',name:'手绘UI14切片候选页',module:'CommonUI',plannedBytes:73728};cat.assets.push(asset);}
Object.assign(asset,{status:'candidate',loadTiming:'preview-only',availability:'用户要求尝试手绘版，独立候选，不覆盖清新UI',use:'512×360/14切片，与当前版相同切片布局；只在独立副本进行实际UI比较',files:[{role:'candidate',path:`${runtime}/T_UI_LevelControls.png`},{role:'workflow',path:`${runtime}/LevelControlsSlices.json`},{role:'workflow',path:`${base}/derivation.json`},{role:'reference',path:`${base}/edge-review.jpg`}],generation:{tool:'本机ComfyUI Qwen Image 2.1 INT8 ConvRot + Sharp派生',record:`${base}/README.md`,notes:'本批5项生成母图独立派生；清新版运行页未写入。'},review:{visual:'pending',rights:'pending',target:'pending'}});
await fs.writeFile(file,JSON.stringify(cat,null,2)+'\n');console.log({bytes:atlas.length,sprites:descriptors.length,sha256:digest(atlas)});
