import assert from 'node:assert/strict';
import {mkdir,writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {chromium} from '@playwright/test';
import sharp from 'sharp';
const output='test-results/pet-transition';await mkdir(output,{recursive:true});
const base=process.env.PET_TRANSITION_URL||'http://127.0.0.1:5193';
const browser=await chromium.launch({channel:'msedge',headless:true});
const errors=[],results=[];
try{
    const page=await browser.newPage({reducedMotion:'reduce'});page.on('pageerror',e=>errors.push(e.message));
    page.on('response',r=>{if(r.status()>=400)errors.push(`${r.status()} ${r.url()}`);});
    const hash=async()=>createHash('sha256').update(await page.locator('canvas').evaluate(c=>c.toDataURL())).digest('hex');
    const seek=async(t)=>{await page.getByLabel('进度',{exact:true}).fill(String(t));await page.waitForTimeout(50);};
    for(const [width,height] of [[1280,900],[390,844],[375,667],[320,568],[844,390]]){
        await page.setViewportSize({width,height});await page.goto(`${base}/pet-transition.html`);
        await page.getByRole('button',{name:'播放',exact:true}).waitFor();await page.waitForFunction(()=>!document.querySelector('button[aria-label="播放"]').disabled);
        const hashes=new Set();
        for(const t of [0,.95,1.25,1.55,2.5,4.1]){
            await seek(t);hashes.add(await hash());
            const bounds=await page.locator('canvas').evaluate(canvas=>{
                const c=document.createElement('canvas');c.width=canvas.width;c.height=canvas.height;
                const context=c.getContext('2d');context.drawImage(canvas,0,0);const data=context.getImageData(0,0,c.width,c.height).data;
                let count=0,left=c.width,right=0,top=c.height,bottom=0;
                for(let p=0;p<data.length;p+=4)if(data[p+3]>64){const x=p/4%c.width,y=Math.floor(p/4/c.width);count++;left=Math.min(left,x);right=Math.max(right,x);top=Math.min(top,y);bottom=Math.max(bottom,y);}
                return{count,left,right,top,bottom,width:c.width,height:c.height};
            });
            assert.ok(bounds.count>2000,JSON.stringify(bounds));assert.ok(bounds.left>2&&bounds.right<bounds.width-2&&bounds.top>2&&bounds.bottom<bounds.height-2,JSON.stringify(bounds));
            results.push({viewport:[width,height],t,...bounds});
            if(width===390||width===1280)await page.screenshot({path:`${output}/${width}-${t}.png`,fullPage:true});
        }
        assert.ok(hashes.size>=5);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);
        const clips=await page.evaluate(()=>[...document.querySelectorAll('button,h1,label,output')].filter(el=>el.clientWidth&&(el.scrollWidth>el.clientWidth+2||el.scrollHeight>el.clientHeight+2)).map(el=>el.textContent));assert.deepEqual(clips,[]);
        await seek(1.25);const held=await hash(),heldImage=await page.locator('canvas').screenshot();await page.waitForTimeout(180);assert.equal(await hash(),held);
        await page.getByRole('button',{name:'播放',exact:true}).click();await page.waitForTimeout(180);await page.getByRole('button',{name:'暂停',exact:true}).click();assert.notEqual(await hash(),held);
        await seek(1.25);
        assert.equal(await page.locator('canvas').getAttribute('data-progress'),'0.5');
        const before=await sharp(heldImage).raw().toBuffer(),after=await sharp(await page.locator('canvas').screenshot()).raw().toBuffer();
        let changed=0,maxDifference=0;
        for(let i=0;i<before.length;i++){const d=Math.abs(before[i]-after[i]);if(d)changed++;maxDifference=Math.max(maxDifference,d);}
        // Edge's antialiased redraw can differ by one channel level; the CPU pose is tested bit-exact separately.
        assert.ok(maxDifference<=1&&changed/before.length<.001,JSON.stringify({maxDifference,changed,bytes:before.length}));
        await page.getByLabel('骨骼',{exact:true}).check();assert.notEqual(await hash(),held);
        if(width===1280) {
            for(const t of [0,1.25,2.5]){await seek(t);await page.screenshot({path:`${output}/bones-${t}.png`,fullPage:true});}
        }
        await page.getByLabel('网格',{exact:true}).check();assert.notEqual(await hash(),held);
        await page.getByLabel('骨骼',{exact:true}).uncheck();await page.getByLabel('网格',{exact:true}).uncheck();
        await page.getByRole('button',{name:'深色',exact:true}).click();await page.screenshot({path:`${output}/${width}-dark.png`,fullPage:true});
        await page.getByRole('button',{name:'下一帧',exact:true}).click();await page.getByRole('button',{name:'上一帧',exact:true}).click();
        await page.getByLabel('速度',{exact:true}).selectOption('1');
    }
    assert.deepEqual(errors,[]);
    const frames=[];await page.setViewportSize({width:500,height:700});await page.getByRole('button',{name:'浅色',exact:true}).click();
    for(const t of [0,.95,1.25,1.55,2.5]){await seek(t);const bytes=await page.locator('.stage').screenshot();frames.push(await sharp(bytes).resize(240,300,{fit:'contain',background:'#f3f7f5'}).toBuffer());}
    await sharp({create:{width:1200,height:300,channels:4,background:'#f3f7f5'}}).composite(frames.map((input,i)=>({input,left:i*240,top:0}))).png().toFile(`${output}/contact.png`);
    if(process.argv.includes('--animation')) {
        const animation=[],withBones=[];
        for(let i=0;i<56;i++){
            await seek(i*.1);const normal=await sharp(await page.locator('.stage').screenshot()).resize(420,340,{fit:'contain',background:'#f3f7f5'}).png().toBuffer();animation.push(normal);
            await page.getByLabel('骨骼',{exact:true}).check();const bones=await sharp(await page.locator('.stage').screenshot()).resize(420,340,{fit:'contain',background:'#f3f7f5'}).png().toBuffer();
            withBones.push(await sharp({create:{width:840,height:340,channels:4,background:'#f3f7f5'}}).composite([{input:normal,left:0,top:0},{input:bones,left:420,top:0}]).png().toBuffer());
            await page.getByLabel('骨骼',{exact:true}).uncheck();
        }
        await sharp(animation,{join:{animated:true}}).gif({delay:Array(animation.length).fill(100),loop:0,colours:128}).toFile(`${output}/stand-sit.gif`);
        await sharp(withBones,{join:{animated:true}}).gif({delay:Array(withBones.length).fill(100),loop:0,colours:128}).toFile(`${output}/stand-sit-bones.gif`);
        const metadata=await sharp(`${output}/stand-sit.gif`,{animated:true}).metadata();
        assert.equal(metadata.delay.reduce((sum,n)=>sum+n,0),5600);assert.equal(metadata.loop,0);
        const bonesMetadata=await sharp(`${output}/stand-sit-bones.gif`,{animated:true}).metadata();assert.equal(bonesMetadata.delay.reduce((sum,n)=>sum+n,0),5600);
    }
    await writeFile(`${output}/browser.json`,JSON.stringify({errors,results},null,2));console.log(`PASS ${results.length} rendered frames; 5 viewports; transport, seek, overlays, backgrounds, pixels and text bounds.`);
}finally{await browser.close();}
