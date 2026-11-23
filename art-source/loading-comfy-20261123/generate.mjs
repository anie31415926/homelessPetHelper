import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const base = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(base, '../..');
const endpoint = 'http://127.0.0.1:8195';
const [action, slug] = process.argv.slice(2);
const style = 'Use image 1 for EXACT sheet layout, object positions and silhouettes ONLY. Image 2 is the original illustration for art direction. Redraw fresh high-resolution clean 2D storybook watercolor and colored-pencil game assets: soft peach, butter yellow, muted sage and warm gray, delicate continuous dark warm-gray pencil outlines, matte pigment and fine paper grain INSIDE the painted objects. Keep the original cute round proportions and faces. The drawing must be genuinely flat and hand painted, with no volumetric rendering, no bevel, no gloss, no lighting highlight, no embossed rim, no hard drop shadow, no plastic or candy material. Outside every object use perfectly uniform pure WHITE #ffffff, no paper noise or shadow outside silhouettes, no white sticker border. All silhouettes closed, clean and anti-aliased; no torn furry alpha edge. No text, labels, digits, logos or watermarks. Do not add a scene or rearrange cells. ';
const prompts = {
  animals: style + 'Redraw all SIXTEEN separate animal assets in their original 4 by 4 cells. Keep each animal in its own cell with broad blank white gutters. Preserve the species and coat patterns in each cell, including the paired bunnies as ONE paired asset. Complete and tidy each body outline. No background, no props, no platforms, no house. Keep each individual at its existing angle and center. Do not print a grid or cell boundaries. Preserve eye shape and the warm innocent expressions; do not make plush 3D toys.',
  house: style + 'Draw ONLY the one little front-facing yellow wooden animal shelter with the orange gabled roof and brown arched door. The house is empty with NO animals anywhere. Repair the lower wall corners so both lower walls are complete and the two little side windows are intact. Thin pencil outlines, light vertical watercolor wood grain, a matte orange roof, warm flat brown door; a simple cozy illustration, not a dimensional game icon. No ground or floor. Same silhouette as the guide.',
  controls: style + 'Redraw exactly THREE separate UI pieces, one per horizontal row, centered where image 1 puts them. Top row: a blank little pale-yellow hand-painted paper label, thin warm-gray pencil outline, softly imperfect corners, clean empty interior. Middle row: a long thin EMPTY progress track with simple warm-gray pencil line and pale watercolor-paper interior, flat corners gently rounded, no raised rim or inset depth. Bottom row: one long thin BUTTER-YELLOW watercolor progress-fill strip with delicate soft orange diagonal pencil hatching, continuous and flat, no capsule, no frame, no gradient. Make these look like marks on paper, NOT plastic widgets. Keep all three fully separate with large pure-white gaps. No text, numbers, cat, icons, shines or cast shadows.',
  decorations: style + 'Redraw ONLY the twelve small hearts, four-point stars and five-petal flowers in precisely their separate guide cells. Delicate coral and lemon watercolor doodles, small thin pencil accents, no glow, no dimensional shading, no extra scattered dots. No cell borders.',
  background: 'Use image 1 as the base and image 2 for its original watercolor-paper mood. Create a clean high-quality PORTRAIT watercolor paper background, pale peach and blush at the top, soft buttery yellow through the middle, warm apricot near the bottom, subtle physical cotton-paper grain, gentle watercolor pigment variations. Background ONLY. Remove all stars, hearts, dots, sparkles, animals, house, lettering, progress bar and objects. No blurry orbs, no glossy gradient, no 3D, no vignette, no hard bands, no stains. Keep the light quiet flat storybook paper atmosphere and bright empty center. No white border, no text or watermark.',
};
if (!prompts[slug] || !['run','collect'].includes(action)) throw Error('Use run|collect animals|house|controls|decorations|background');
async function request(url, options={}) {
  const response = await fetch(`${endpoint}/${url}`,{...options,signal:AbortSignal.timeout(30000)});
  if (!response.ok) throw Error(`${response.status}: ${await response.text()}`);
  return response;
}
const dir=path.join(base,slug);
async function upload(file, name) {
  const form=new FormData();
  form.append('image',new Blob([await fs.readFile(file)]),name);
  form.append('subfolder','loading-comfy-20261123');
  form.append('overwrite','false');
  const r=await (await request('upload/image',{method:'POST',body:form})).json();
  return `${r.subfolder}/${r.name}`;
}
let submission;
if(action==='run') {
  if(await fs.access(path.join(dir,'submission.json')).then(()=>true,()=>false)) throw Error('Already submitted; collect only.');
  const queue=await (await request('queue')).json();
  if(queue.queue_running.length||queue.queue_pending.length) throw Error('Owned service queue is busy; do not replace jobs.');
  const guide=await upload(path.join(dir,'guide.png'),`${slug}-guide.png`);
  const reference=await upload(path.join(root,'art-source/loading-20261123/reference.png'),`${slug}-reference.png`);
  const graph={
    '1':{class_type:'UNETLoader',inputs:{unet_name:'qwen_image_2.1_int8_convrot.safetensors',weight_dtype:'default'}},
    '2':{class_type:'CLIPLoader',inputs:{clip_name:'qwen3vl_8b_int8_convrot.safetensors',type:'qwen_image',device:'default'}},
    '3':{class_type:'VAELoader',inputs:{vae_name:'qwen_image_2.1_vae_bf16.safetensors'}},
    '4':{class_type:'LoadImage',inputs:{image:guide}},
    '10':{class_type:'LoadImage',inputs:{image:reference}},
    '5':{class_type:'TextEncodeQwenImage21',inputs:{clip:['2',0],vae:['3',0],prompt:prompts[slug],
      negative_prompt:'plastic, 3d, glossy, bevel, embossed, shiny, airbrush, fake fur edges, torn edges, thick white sticker border, jagged pixel outline, text, words, watermark, cell borders, extra objects',
      resolution:0,'images.image_1':['4',0],'images.image_2':['10',0]}},
    '6':{class_type:'ModelSamplingAuraFlow',inputs:{model:['1',0],shift:3.1}},
    '7':{class_type:'KSampler',inputs:{model:['6',0],positive:['5',0],negative:['5',1],latent_image:['5',2],
      seed:2026112360+Object.keys(prompts).indexOf(slug),steps:24,cfg:4,sampler_name:'euler',scheduler:'simple',denoise:1}},
    '8':{class_type:'VAEDecode',inputs:{samples:['7',0],vae:['3',0]}},
    '9':{class_type:'SaveImage',inputs:{images:['8',0],filename_prefix:`loading-comfy-20261123/${slug}`}},
  };
  await fs.writeFile(path.join(dir,'workflow.json'),JSON.stringify({prompt:graph,references:[guide,reference]},null,2));
  submission=await (await request('prompt',{method:'POST',headers:{'Content-Type':'application/json'},
    body:JSON.stringify({prompt:graph,client_id:'loading-comfy-01a20769'})})).json();
  await fs.writeFile(path.join(dir,'submission.json'),JSON.stringify(submission,null,2));
  console.log(JSON.stringify({slug,submitted:submission.prompt_id}));
} else submission=JSON.parse(await fs.readFile(path.join(dir,'submission.json'),'utf8'));
const deadline=Date.now()+30*60*1000;
while(Date.now()<deadline) {
  const all=await (await request(`history/${submission.prompt_id}`)).json();
  const result=all[submission.prompt_id];
  if(result) {
    await fs.writeFile(path.join(dir,'history.json'),JSON.stringify(result,null,2));
    if(result.status?.status_str==='error') throw Error(JSON.stringify(result.status));
    const output=result.outputs?.['9']?.images?.[0];
    if(output) {
      await fs.writeFile(path.join(dir,'master.png'),Buffer.from(await (await request(`view?${new URLSearchParams(output)}`)).arrayBuffer()));
      console.log(JSON.stringify({slug,saved:'master.png',output}));
      process.exit(0);
    }
  }
  await new Promise(r=>setTimeout(r,10000));
}
throw Error('Generation timeout. Collect by saved prompt id; never resubmit.');
