import {createReadStream} from 'node:fs';
import {createHash} from 'node:crypto';
import {readFile,writeFile,stat,readdir} from 'node:fs/promises';
import {join} from 'node:path';
import sharp from 'sharp';
const base='art-source/pet-transition-20261123';
const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
if(process.argv.includes('--snapshot')||process.argv.includes('--verify-snapshot')) {
    const files=['pet-transition.html','tools/pet-transition-art.mjs','tools/pet-transition-process.mjs','tools/pet-transition-audit.mjs','tests/pet-transition.test.mjs','tests/pet-transition-browser.mjs','docs/动物同骨架过渡制作与调研.md'];
    async function visit(directory){for(const entry of await readdir(directory,{withFileTypes:true})){const path=`${directory}/${entry.name}`;if(entry.isDirectory())await visit(path);else if(path!==`${base}/verification-inputs.json`)files.push(path);}}
    for(const directory of ['src/pet-transition','public/pet-transition',base])await visit(directory);
    const inputs=[];for(const path of files.sort())inputs.push({path,sha256:digest(await readFile(path))});
    if(process.argv.includes('--verify-snapshot')){const prior=JSON.parse(await readFile(`${base}/verification-inputs.json`,'utf8'));if(JSON.stringify(prior.inputs)!==JSON.stringify(inputs))throw new Error('Verification input snapshot changed');console.log(`PASS ${inputs.length} verification inputs unchanged`);}
    else {await writeFile(`${base}/verification-inputs.json`,JSON.stringify({schemaVersion:1,recordedAt:new Date().toISOString(),scope:'Standalone preview, generation provenance and tests only; no Unity snapshot claim.',inputs},null,2));console.log(`Recorded ${inputs.length} inputs`);}
    process.exit(0);
}
const records=[];
for(const name of ['torso-a','torso-repeat']) {
    const folder=`${base}/${name}`,data=await sharp(`${folder}/master.png`).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const history=JSON.parse(await readFile(`${folder}/history.json`,'utf8'));
    const workflow=JSON.parse(await readFile(`${folder}/submitted-workflow.json`,'utf8'));
    const start=history.status.messages.find(([id])=>id==='execution_start')?.[1].timestamp;
    const end=history.status.messages.find(([id])=>id==='execution_success')?.[1].timestamp;
    records.push({name,promptId:history.prompt[1],status:history.status.status_str,seed:workflow.prompt['7'].inputs.seed,dimensions:[data.info.width,data.info.height],rgbaSha256:digest(data.data),fileSha256:digest(await readFile(`${folder}/master.png`)),executionMs:end-start});
}
const server=process.env.COMFY_URL||'http://127.0.0.1:8193';
if(!['127.0.0.1','localhost','[::1]'].includes(new URL(server).hostname))throw new Error('Local ComfyUI only');
const system=(await (await fetch(`${server}/system_stats`)).json());
const models=[];
const modelRoot=process.env.COMFY_MODEL_ROOT;
if(!modelRoot)throw new Error('Set COMFY_MODEL_ROOT to the existing model directory before auditing models');
for(const [kind,file] of [['diffusion_models','qwen_image_2.1_int8_convrot.safetensors'],['text_encoders','qwen3vl_8b_int8_convrot.safetensors'],['vae','qwen_image_2.1_vae_bf16.safetensors']]) {
    const path=join(modelRoot,kind,file),hash=createHash('sha256');
    for await(const chunk of createReadStream(path))hash.update(chunk);
    models.push({kind,file,bytes:(await stat(path)).size,sha256:hash.digest('hex')});
}
const result={schemaVersion:1,recordedAt:new Date().toISOString(),runtime:{comfyui:system.system.comfyui_version,python:system.system.python_version,pytorch:system.system.pytorch_version,device:system.devices[0].name},models,records,
    identicalDecodedPixels:records[0].rgbaSha256===records[1].rgbaSha256,scope:'Same machine/model files/seed/prompt, two actual submissions. No cross-seed, machine, device, restart or other-animal stability claim.'};
await writeFile(`${base}/reproducibility.json`,JSON.stringify(result,null,2));console.log(JSON.stringify(result,null,2));
