import {spawnSync} from 'node:child_process';
for(const slug of ['primary','secondary','symbols','knob']){
 const result=spawnSync(process.execPath,['art-source/ui-handdrawn-20261122/run.mjs','run',slug],{stdio:'inherit'});
 if(result.status!==0)process.exit(result.status??1);
}
