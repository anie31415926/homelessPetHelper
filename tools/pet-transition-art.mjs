import { createHash } from 'node:crypto';
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
import sharp from 'sharp';

const base = 'art-source/pet-transition-20261123';
const server = process.env.COMFY_URL || 'http://127.0.0.1:8193';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) throw new Error('Local ComfyUI only');
const command = process.argv[2] || 'prepare';
const name = process.argv[3] || 'torso-a';
if (!/^[a-z0-9-]+$/.test(name)) throw new Error('Invalid job name');
const folder = `${base}/${name}`;
const source = 'public/pet-mesh/cat-side.png';
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const exists = path => access(path).then(() => true, () => false);
const plan=JSON.parse(await readFile(`${base}/plan.json`,'utf8'));
if(hash(await readFile(source))!==plan.referenceSha256)throw new Error('Identity reference changed; review first');
await mkdir(folder, {recursive:true});

if (command === 'prepare') {
    if (await exists(`${folder}/workflow.json`)) throw new Error('Refusing to overwrite prepared job');
    const crop = {left:74, top:270, width:330, height:242};
    await sharp(source).extract(crop).resize(768, 568).flatten({background:'#b4e1eb'}).png().toFile(`${folder}/guide.png`);
    const graph = structuredClone(JSON.parse(await readFile('art-source/pet-motion/sit-rest/workflow.json','utf8')).prompt);
    graph['10'] = {class_type:'LoadImage', inputs:{image:'REFERENCE_PENDING'}};
    graph['4'].inputs.image = 'GUIDE_PENDING';
    graph['5'].inputs.vae = ['3',0];
    graph['5'].inputs['images.image_2'] = ['10',0];
    graph['5'].inputs.prompt = 'Image 1 is a crop of the torso of the kitten in image 2. Edit image 1 ONLY: remove all four legs below the belly, and remove the tail at the right. Reconstruct a single soft rounded legless torso with a smooth closed ivory belly contour. Keep the exact existing shoulder, back, rump width, placement, pale apricot coat, sparse orange stripes and delicate peach watercolor outline. Retain the original torso scale and horizontal standing orientation. No head, no ears, no eyes, no face, no scarf, no legs, no paws, no tail. This is one isolated body sprite to overlap behind the ORIGINAL unchanged head and original limbs in a 2D skeletal animation. Fill missing hidden shoulder and hip surfaces with matching fur, smooth rounded roots, no internal leg-root lines. Do not redesign or change the style. Flat solid pale cyan RGB180 225 235 background, no floor, no shadow, no text, no checkerboard.';
    graph['5'].inputs.negative_prompt = 'head, face, eyes, ears, scarf, paw, legs, feet, tail, whole cat, multiple objects, photorealistic, realistic fur, black outline, text, watermark, checkerboard, shadow, changed torso size';
    graph['5'].inputs.resolution = 768;
    graph['7'].inputs.seed = Number(process.env.PET_SEED || 2026112301);
    if(!Number.isSafeInteger(graph['7'].inputs.seed)||graph['7'].inputs.seed<0)throw new Error('Invalid seed');
    graph['9'].inputs.filename_prefix = `pet-transition-20261123/${name}`;
    await writeFile(`${folder}/workflow.json`,JSON.stringify({reference:source,referenceSha256:hash(await readFile(source)),guide:`${folder}/guide.png`,crop,guideSize:[768,568],prompt:graph,method:'Two-image edit, not latent masked inpaint. Final original-pixel composite is controlled separately.'},null,2));
    console.log('prepared',folder);
} else if (command === 'submit') {
    if (await exists(`${folder}/submission.json`)) throw new Error('Job already submitted; use fetch, never resubmit');
    const record = JSON.parse(await readFile(`${folder}/workflow.json`,'utf8'));
    const graph = structuredClone(record.prompt);
    for(const type of new Set(Object.values(graph).map(node=>node.class_type))) {
        const response=await fetch(`${server}/object_info/${type}`);
        if(!response.ok||!(await response.json())[type])throw new Error(`Required ComfyUI node unavailable: ${type}`);
    }
    async function upload(path,filename) {
        const form = new FormData();
        form.append('image',new Blob([await readFile(path)]),filename);
        form.append('subfolder','pet-transition-20261123'); form.append('overwrite','false');
        const response = await fetch(`${server}/upload/image`,{method:'POST',body:form});
        if (!response.ok) throw new Error(await response.text());
        const file = await response.json();
        return file.subfolder ? `${file.subfolder}/${file.name}` : file.name;
    }
    graph['4'].inputs.image = await upload(record.guide,`${name}-guide.png`);
    graph['10'].inputs.image = await upload(source,`${name}-reference.png`);
    const submitted = await fetch(`${server}/prompt`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({prompt:graph,client_id:'pet-transition-01a2076c'})});
    const result = await submitted.json();
    if (!submitted.ok) throw new Error(JSON.stringify(result));
    await writeFile(`${folder}/submitted-workflow.json`,JSON.stringify({server,prompt:graph},null,2));
    await writeFile(`${folder}/submission.json`,JSON.stringify(result,null,2));
    console.log('queued',result.prompt_id);
} else if (command === 'fetch') {
    const {prompt_id} = JSON.parse(await readFile(`${folder}/submission.json`,'utf8'));
    for (let i=0;i<600;i++) {
        const response = await fetch(`${server}/history/${prompt_id}`);
        if (!response.ok) throw new Error(await response.text());
        const record = (await response.json())[prompt_id];
        if (record?.status?.status_str === 'error') {
            await writeFile(`${folder}/history.json`,JSON.stringify(record,null,2));
            throw new Error(JSON.stringify(record.status));
        }
        const file = record?.outputs?.['9']?.images?.[0];
        if (file) {
            const image = await fetch(`${server}/view?${new URLSearchParams(file)}`);
            if (!image.ok) throw new Error(await image.text());
            const bytes = Buffer.from(await image.arrayBuffer());
            if (await exists(`${folder}/master.png`) && hash(await readFile(`${folder}/master.png`)) !== hash(bytes)) throw new Error('Existing output differs');
            await writeFile(`${folder}/master.png`,bytes);
            await writeFile(`${folder}/history.json`,JSON.stringify(record,null,2));
            console.log('saved',folder,hash(bytes)); break;
        }
        if (i===599) throw new Error('Timed out; resume same prompt_id with fetch');
        await new Promise(resolve=>setTimeout(resolve,3000));
    }
} else throw new Error('Use prepare, submit or fetch');
