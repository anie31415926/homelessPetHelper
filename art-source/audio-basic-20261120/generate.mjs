import { mkdir, readFile, writeFile, access } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const batch = path.dirname(fileURLToPath(import.meta.url));
const variant = process.argv[2] || '';
if (variant && !/^[a-z0-9-]+$/.test(variant)) throw new Error('Invalid variant directory');
const root = path.join(batch, variant);
const server = process.env.COMFY_URL || 'http://127.0.0.1:8191';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) {
  throw new Error('This batch only permits local ComfyUI generation.');
}
const plan = JSON.parse(await readFile(path.join(root, 'plan.json'), 'utf8'));
const info = await request('/object_info');
const required = ['CheckpointLoaderSimple', 'CLIPLoader', 'CLIPTextEncode', 'ConditioningStableAudio',
  'EmptyLatentAudio', 'KSampler', 'VAEDecodeAudio', 'SaveAudioAdvanced', 'AudioAdjustVolume'];
for (const name of required) if (!info[name]) throw new Error(`Missing node ${name}`);
await mkdir(path.join(root, 'raw'), { recursive: true });
await mkdir(path.join(root, 'workflows'), { recursive: true });
await mkdir(path.join(root, 'history'), { recursive: true });
await mkdir(path.join(root, 'provenance'), { recursive: true });
await writeFile(path.join(root, 'provenance', 'node-schemas.json'), JSON.stringify(
  Object.fromEntries(required.map(name => [name, info[name]])), null, 2));
await writeFile(path.join(root, 'provenance', 'system-stats.json'), JSON.stringify(await request('/system_stats'), null, 2));
for (const cue of plan.cues) {
  const file = path.join(root, 'raw', `${cue.id}.flac`);
  if (await exists(file)) { console.log(`Already saved ${cue.id}; not resubmitting.`); continue; }
  const graph = graphFor(cue);
  const receiptFile = path.join(root, 'history', `${cue.id}-submission.json`);
  let submission;
  if (await exists(receiptFile)) {
    submission = JSON.parse(await readFile(receiptFile, 'utf8'));
    console.log(`Resuming accepted job ${cue.id}: ${submission.prompt_id}`);
  } else {
    await writeFile(path.join(root, 'workflows', `${cue.id}-api.json`), JSON.stringify(graph, null, 2));
    submission = await request('/prompt', { prompt: graph, client_id: 'audio-basic-20261120' });
    await writeFile(receiptFile, JSON.stringify({ ...submission, submittedAt: new Date().toISOString(),
      workflowSha256: createHash('sha256').update(JSON.stringify(graph)).digest('hex') }, null, 2), { flag: 'wx' });
    console.log(`Queued ${cue.id}: ${submission.prompt_id}`);
  }
  const deadline = Date.now() + 15 * 60 * 1000;
  let result;
  while (Date.now() < deadline) {
    const history = await request(`/history/${submission.prompt_id}`);
    result = history[submission.prompt_id];
    if (result?.status?.status_str === 'error') {
      await writeFile(path.join(root, 'history', `${cue.id}-error.json`), JSON.stringify(result, null, 2));
      throw new Error(`Generation failed; preserved error for ${cue.id}`);
    }
    if (result?.outputs?.['9']?.audio?.length) break;
    await new Promise(resolve => setTimeout(resolve, 3000));
  }
  const output = result?.outputs?.['9']?.audio?.[0];
  if (!output) throw new Error(`Timed out; resume the accepted job, do not resubmit ${cue.id}`);
  await writeFile(path.join(root, 'history', `${cue.id}-history.json`), JSON.stringify(result, null, 2));
  const response = await fetch(`${server}/view?${new URLSearchParams(output)}`);
  if (!response.ok) throw new Error(`Could not retrieve accepted output ${cue.id}`);
  await writeFile(file, Buffer.from(await response.arrayBuffer()), { flag: 'wx' });
  console.log(`Saved ${cue.id}`);
}

function graphFor(cue) {
  return {
    '1': { class_type: 'CheckpointLoaderSimple', inputs: { ckpt_name: 'stable-audio-open-1.0.safetensors' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 't5-base.safetensors', type: 'stable_audio', device: 'default' } },
    '3': { class_type: 'CLIPTextEncode', inputs: { clip: ['2', 0], text: cue.prompt } },
    '4': { class_type: 'CLIPTextEncode', inputs: { clip: ['2', 0], text: plan.negative } },
    '5': { class_type: 'ConditioningStableAudio', inputs: { positive: ['3', 0], negative: ['4', 0], seconds_start: 0, seconds_total: plan.generationSeconds } },
    '6': { class_type: 'EmptyLatentAudio', inputs: { seconds: plan.latentSeconds || plan.generationSeconds, batch_size: 1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['1', 0], positive: ['5', 0], negative: ['5', 1],
      latent_image: ['6', 0], seed: cue.seed, steps: plan.steps, cfg: plan.cfg,
      sampler_name: plan.sampler, scheduler: plan.scheduler, denoise: 1 } },
    '8': { class_type: 'VAEDecodeAudio', inputs: { samples: ['7', 0], vae: ['1', 2] } },
    '10': { class_type: 'AudioAdjustVolume', inputs: { audio: ['8', 0], volume: plan.exportGainDb || 0 } },
    '9': { class_type: 'SaveAudioAdvanced', inputs: { audio: ['10', 0], filename_prefix: `audio-basic-20261120/${variant || 'v1'}/${cue.id}`, format: 'flac' } },
  };
}

async function request(endpoint, body) {
  const response = await fetch(`${server}${endpoint}`, body ? { method: 'POST',
    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : undefined);
  const result = await response.json();
  if (!response.ok) throw new Error(JSON.stringify(result));
  return result;
}

async function exists(file) { return access(file).then(() => true, () => false); }
