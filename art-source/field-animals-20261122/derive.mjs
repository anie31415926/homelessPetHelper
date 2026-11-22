import fs from 'node:fs/promises';
import sharp from 'sharp';
const base = 'art-source/field-animals-20261122';
const records = [];
for (const animal of ['dog','cat']) {
    const input = `${base}/${animal}/master.png`;
    const {data,info} = await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const pixels = info.width * info.height;
    const mask = new Uint8Array(pixels), queue = new Int32Array(pixels);
    let read = 0, write = 0;
    function visit(index) {
        if (index < 0 || index >= pixels || mask[index]) return;
        const at = index * 4;
        if (data[at+1]-data[at] < 12 || data[at+2]-data[at] < 15) return;
        mask[index] = 1; queue[write++] = index;
    }
    for (let x=0;x<info.width;x++) { visit(x); visit((info.height-1)*info.width+x); }
    for (let y=0;y<info.height;y++) { visit(y*info.width); visit(y*info.width+info.width-1); }
    while (read<write) {
        const index=queue[read++], col=index%info.width;
        if(col>0)visit(index-1); if(col+1<info.width)visit(index+1);
        visit(index-info.width); visit(index+info.width);
    }
    for(let i=0;i<pixels;i++) if(mask[i]) data[i*4+3]=0;
    const target=`unity/HomelessPetHelper/Assets/Art/Outing/T_Outing_${animal === 'dog' ? 'Dog' : 'Cat'}Natural.png`;
    const crop=await sharp(data,{raw:info}).trim({background:'#00000000',threshold:1}).png().toBuffer();
    await sharp(crop).resize(252,252,{fit:'inside',withoutEnlargement:true}).extend({top:2,bottom:2,left:2,right:2,background:'#00000000'}).png({palette:true,colours:256,dither:.5}).toFile(target);
    records.push({animal,input,target,removedBackgroundPixels:write,output:await sharp(target).metadata()});
}
await fs.writeFile(`${base}/derivation.json`,JSON.stringify(records,null,2)+'\n');
const path='art-source/resource-catalog.json';
const catalog=JSON.parse(await fs.readFile(path,'utf8'));
for(const record of records){
    const item=catalog.assets.find(item=>item.id===`field-animal.${record.animal}`);
    item.status='temporary';item.loadTiming='first-play';
    item.availability='Main小院的清路与救助静态绑定，猫狗均成为实际随包依赖；不代表按需加载、正式美术或微信下载验收。';
    item.files=item.files.filter(file=>file.role!=='runtime');
    for(const file of item.files) if(file.role==='candidate') file.role='reference';
    item.files.push({role:'runtime',path:record.target},{role:'workflow',path:`${base}/derivation.json`});
}
await fs.writeFile(path,JSON.stringify(catalog,null,2)+'\n');
console.log(records.map(record=>({animal:record.animal,width:record.output.width,height:record.output.height})));
