import fs from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';
const base='art-source/field-props-20261122', url='http://127.0.0.1:8188';
const jobs=JSON.parse(await fs.readFile(`${base}/jobs.json`,'utf8'));
const json=async(route,options)=>{const response=await fetch(url+route,options);if(!response.ok)throw Error(`${route} ${response.status}: ${await response.text()}`);return response.json();};
await sharp({create:{width:768,height:768,channels:3,background:'#ffffff'}}).png().toFile(`${base}/grid-guide.png`);
const catalog=JSON.parse(await fs.readFile('art-source/resource-catalog.json','utf8'));
const guide=catalog.assets.find(a=>a.id==='field-props.guide');guide.status='candidate';guide.files=[{role:'candidate',path:`${base}/grid-guide.png`}];
await fs.writeFile('art-source/resource-catalog.json',JSON.stringify(catalog,null,2)+'\n');
await fs.writeFile(`${base}/system_stats.json`,JSON.stringify(await json('/system_stats'),null,2)+'\n');
const queue=await json('/queue');if(queue.queue_running.length||queue.queue_pending.length)throw Error('Queue was not empty; inspect before submitting.');
const inputFolder=`${base}/references`;await fs.mkdir(inputFolder,{recursive:true});
const referenceNames=new Map();
for(const reference of [...new Set(jobs.flatMap(j=>j.references))]){
 const file=path.basename(reference),form=new FormData();form.set('image',new Blob([await fs.readFile(reference)]),file);form.set('subfolder','field-props-20261122');form.set('overwrite','false');
 const uploaded=await json('/upload/image',{method:'POST',body:form});referenceNames.set(reference,`${uploaded.subfolder}/${uploaded.name}`);
}
const template=JSON.parse(await fs.readFile('art-source/ui-handdrawn-20261122/symbols-v2/workflow.json','utf8')).prompt;
for(const job of jobs){
 const folder=`${base}/${job.slug}`;await fs.mkdir(folder,{recursive:true});
 try{await fs.access(`${folder}/history.json`);console.log(`Already generated ${job.slug}; preserved.`);continue;}catch{}
 const prompt=structuredClone(template);
 prompt['5'].inputs.prompt=job.prompt;prompt['5'].inputs.negative_prompt=job.negative;
 prompt['7'].inputs.seed=job.seed;prompt['7'].inputs.steps=job.steps;
 prompt['9'].inputs.filename_prefix=`field-props-20261122/${job.slug}`;
 prompt['20'].inputs.image=referenceNames.get(job.references[0]);prompt['21'].inputs.image=referenceNames.get(job.references[1]);
 await fs.writeFile(`${folder}/workflow.json`,JSON.stringify({job,prompt},null,2)+'\n');
 const submitted=await json('/prompt',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({prompt,client_id:'field-props-20261122'})});
 await fs.writeFile(`${folder}/submission.json`,JSON.stringify(submitted,null,2)+'\n');
 console.log(`Submitted ${job.slug}: ${submitted.prompt_id}`);
 let history;
 while(true){await new Promise(r=>setTimeout(r,3000));const records=await json(`/history/${submitted.prompt_id}`);history=records[submitted.prompt_id];if(history?.status?.completed)break;if(history?.status?.status_str==='error')throw Error(JSON.stringify(history.status));}
 await fs.writeFile(`${folder}/history.json`,JSON.stringify(history,null,2)+'\n');
 const img=history.outputs['9'].images[0],params=new URLSearchParams(img),response=await fetch(`${url}/view?${params}`);if(!response.ok)throw Error('Image download failed');
 const bytes=Buffer.from(await response.arrayBuffer());await fs.writeFile(`${folder}/master.png`,bytes);await sharp(bytes).resize({width:768,withoutEnlargement:true}).jpeg({quality:88}).toFile(`${folder}/candidate.jpg`);
 // Only this task owns the catalog. Update after each real job so failures remain inspectable.
 const current=JSON.parse(await fs.readFile('art-source/resource-catalog.json','utf8')),entry=current.assets.find(a=>a.id===job.assetId);
 entry.status='candidate';entry.files=[{role:'candidate',path:`${folder}/candidate.jpg`},{role:'master',path:`${folder}/master.png`},{role:'workflow',path:`${folder}/workflow.json`},{role:'generation-record',path:`${folder}/submission.json`},{role:'generation-record',path:`${folder}/history.json`}];
 entry.generation.notes=`Real local ComfyUI job ${submitted.prompt_id}; full prompt, model, references, seed and parameters in workflow.json. White background, not native alpha.`;
 await fs.writeFile('art-source/resource-catalog.json',JSON.stringify(current,null,2)+'\n');
 console.log(`Completed ${job.slug}: ${bytes.length} bytes`);
}
