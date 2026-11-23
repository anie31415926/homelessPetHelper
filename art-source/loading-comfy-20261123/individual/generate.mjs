import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const base = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(base, '../../..');
const plan = JSON.parse(await fs.readFile(path.join(base, 'plan.json'), 'utf8'));
const endpoint = 'http://127.0.0.1:8195';
const [action, ...ids] = process.argv.slice(2);
const revision = process.env.LOADING_REVISION || 'v2';
if (!['run', 'collect'].includes(action) || !ids.length) throw Error('Use run|collect <ids>');
const style = 'Change the animal in image 1 into the subject described below. Preserve EXACTLY image 1 cute tiny storybook game-sprite drawing language: oversized ROUND head, very compact little body, stubby paws, tiny simple dark DOT eyes, tiny nose and mouth, simplified flat pastel watercolor shapes, clean continuous warm-gray pencil contour. NOT a realistic animal study, no detailed fur, no realistic eyes, no realistic musculature. Image 2 is only the original warm kawaii art direction, NOT a pose guide or scene to reproduce. No house, scenery or decorations. Head, body and limbs must still connect plausibly, with no extra or fused parts. White #ffffff background perfectly uniform outside subject, no shadows. Whole subject centered, occupying about 70 percent of canvas, 12 percent blank margin all sides. Matte pigment and slight paper grain INSIDE silhouette only. No glossy material, 3D, airbrush, sticker border, words, labels, collar, clothing, ground, props, extra fragments. ';
async function request(url, options = {}) {
  const response = await fetch(`${endpoint}/${url}`, { ...options, signal: AbortSignal.timeout(30000) });
  if (!response.ok) throw Error(`${response.status}: ${await response.text()}`);
  return response;
}
async function upload(file, name) {
  const form = new FormData();
  form.append('image', new Blob([await fs.readFile(file)]), name);
  form.append('subfolder', 'loading-individual-20261123');
  form.append('overwrite', 'false');
  const result = await (await request('upload/image', { method: 'POST', body: form })).json();
  return `${result.subfolder}/${result.name}`;
}
for (const id of ids) {
  const job = plan.jobs.find(item => item.id === id);
  if (!job) throw Error(`Unknown job ${id}`);
  const dir = path.join(base, revision, id);
  await fs.mkdir(dir, { recursive: true });
  let submission;
  const submitted = await fs.access(path.join(dir, 'submission.json')).then(() => true, () => false);
  if (action === 'run' && !submitted) {
    const queue = await (await request('queue')).json();
    if (queue.queue_running.length || queue.queue_pending.length) throw Error('Owned queue is busy.');
    const guideFile = process.env.LOADING_GUIDE || (revision === 'v3' ? `${id}-style.png` : 'style.png');
    const guide = await upload(path.join(base, guideFile), `${revision}-${id}-style.png`);
    const reference = await upload(path.join(root, 'art-source/loading-20261123/reference.png'), `${id}-style.png`);
    const graph = {
      '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
      '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
      '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
      '4': { class_type: 'LoadImage', inputs: { image: guide } },
      '10': { class_type: 'LoadImage', inputs: { image: reference } },
      '5': { class_type: 'TextEncodeQwenImage21', inputs: {
        clip: ['2', 0], vae: ['3', 0], prompt: style + job.subject + (process.env.LOADING_PROMPT_SUFFIX || ''),
        negative_prompt: 'deformed anatomy, extra limbs, missing paws, fused animals, disconnected head, twisted spine, ambiguous species, extra tail, upside down, 3d, plastic, gloss, bevel, thick white sticker edge, shadow, text, props, fragments',
        resolution: 0, 'images.image_1': ['4', 0], 'images.image_2': ['10', 0],
      } },
      '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
      '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1], latent_image: ['5', 2], seed: Number(process.env.LOADING_SEED_BASE || 2026112480) + plan.jobs.indexOf(job), steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
      '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
      '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `loading-individual-20261123/${revision}/${id}` } },
    };
    await fs.writeFile(path.join(dir, 'workflow.json'), JSON.stringify({ prompt: graph, references: [guide, reference], job }, null, 2));
    submission = await (await request('prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph, client_id: 'loading-individual-01a20769' }) })).json();
    await fs.writeFile(path.join(dir, 'submission.json'), JSON.stringify(submission, null, 2));
    console.log(JSON.stringify({ id, submitted: submission.prompt_id }));
  } else {
    submission = JSON.parse(await fs.readFile(path.join(dir, 'submission.json'), 'utf8'));
    if (await fs.access(path.join(dir, 'master.png')).then(() => true, () => false)) {
      console.log(JSON.stringify({ id, skipped: 'Already collected; no submission' }));
      continue;
    }
  }
  const deadline = Date.now() + 30 * 60 * 1000;
  let completed = false;
  while (Date.now() < deadline) {
    const history = await (await request(`history/${submission.prompt_id}`)).json();
    const result = history[submission.prompt_id];
    if (result) {
      await fs.writeFile(path.join(dir, 'history.json'), JSON.stringify(result, null, 2));
      if (result.status?.status_str === 'error') throw Error(JSON.stringify(result.status));
      const output = result.outputs?.['9']?.images?.[0];
      if (output) {
        await fs.writeFile(path.join(dir, 'master.png'), Buffer.from(await (await request(`view?${new URLSearchParams(output)}`)).arrayBuffer()));
        console.log(JSON.stringify({ id, saved: 'master.png', output }));
        completed = true;
        break;
      }
    }
    await new Promise(resolve => setTimeout(resolve, 10000));
  }
  if (!completed) throw Error(`Timeout ${id}; collect saved prompt ID, do not resubmit.`);
}
