import { createReadStream } from 'node:fs';
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const server = 'http://127.0.0.1:8196';
const plan = JSON.parse(await readFile(path.join(root, 'plan.json'), 'utf8'));
const required = ['CheckpointLoaderSimple', 'CLIPLoader', 'CLIPTextEncode',
  'ConditioningStableAudio', 'EmptyLatentAudio', 'KSampler', 'VAEDecodeAudio',
  'AudioAdjustVolume', 'SaveAudioAdvanced'];
const schemas = await waitForServer();
for (const name of required) if (!schemas[name]) throw new Error(`Missing native node ${name}`);
for (const dir of ['raw', 'workflows', 'history', 'provenance']) {
  await mkdir(path.join(root, dir), { recursive: true });
}
await saveJson('provenance/node-schemas.json', Object.fromEntries(required.map(k => [k, schemas[k]])));
await saveJson('provenance/system-stats.json', await request('/system_stats'));
const source = path.join(root, '../audio-basic-20261120/provenance/models.json');
const previous = JSON.parse(await readFile(source, 'utf8'));
const models = [];
for (const model of previous.models) {
  const localPath = path.join('C:/Users/Admin/ComfyUI-Shared/models', model.folder, model.file);
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(localPath)) hash.update(chunk);
  const actual = hash.digest('hex');
  if (actual !== model.sha256) throw new Error(`Existing model changed: ${model.file}`);
  models.push({ ...model, localPath, reverifiedSha256: actual });
  console.log(`Verified local model ${model.file}`);
}
await saveJson('provenance/models.json', { recordedAt: new Date().toISOString(),
  inheritedSource: '../audio-basic-20261120/provenance/models.json', models,
  rights: 'Pending review. Evaluation placeholders only; not approved for commercial release.' });

for (const cue of plan.cues) {
  const raw = path.join(root, 'raw', `${cue.id}.flac`);
  if (await exists(raw)) { console.log(`Saved ${cue.id}; not resubmitting.`); continue; }
  const graph = graphFor(cue);
  const workflow = `workflows/${cue.id}-api.json`;
  const receipt = `history/${cue.id}-submission.json`;
  let submission;
  if (await exists(path.join(root, receipt))) {
    submission = JSON.parse(await readFile(path.join(root, receipt), 'utf8'));
    const archived = await readFile(path.join(root, workflow));
    if (sha(archived) !== submission.workflowSha256) throw new Error('Archived workflow changed');
    console.log(`Resuming ${cue.id}: ${submission.prompt_id}`);
  } else {
    const content = JSON.stringify(graph, null, 2);
    await writeFile(path.join(root, workflow), content, { flag: 'wx' });
    submission = { ...await request('/prompt', { prompt: graph, client_id: 'bgm-placeholder-20261123' }),
      submittedAt: new Date().toISOString(), workflowSha256: sha(content) };
    await saveJson(receipt, submission, 'wx');
    console.log(`Queued ${cue.id}: ${submission.prompt_id}`);
  }
  const deadline = Date.now() + 20 * 60 * 1000;
  let result;
  while (Date.now() < deadline) {
    result = (await request(`/history/${submission.prompt_id}`))[submission.prompt_id];
    if (result?.status?.status_str === 'error') {
      await saveJson(`history/${cue.id}-error.json`, result);
      throw new Error(`Failed accepted job ${cue.id}; error saved, do not resubmit`);
    }
    if (result?.outputs?.['9']?.audio?.length) break;
    await new Promise(resolve => setTimeout(resolve, 5000));
  }
  const output = result?.outputs?.['9']?.audio?.[0];
  if (!output) throw new Error(`Timeout: resume prompt ${submission.prompt_id}; do not resubmit`);
  await saveJson(`history/${cue.id}-history.json`, result);
  const response = await fetch(`${server}/view?${new URLSearchParams(output)}`);
  if (!response.ok) throw new Error(`Output retrieval failed: ${cue.id}`);
  await writeFile(raw, Buffer.from(await response.arrayBuffer()), { flag: 'wx' });
  console.log(`Saved ${cue.id}`);
}

function graphFor(cue) {
  return {
    '1': { class_type: 'CheckpointLoaderSimple', inputs: { ckpt_name: plan.model } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: plan.textEncoder, type: 'stable_audio', device: 'default' } },
    '3': { class_type: 'CLIPTextEncode', inputs: { clip: ['2', 0], text: cue.prompt } },
    '4': { class_type: 'CLIPTextEncode', inputs: { clip: ['2', 0], text: plan.negative } },
    '5': { class_type: 'ConditioningStableAudio', inputs: { positive: ['3', 0], negative: ['4', 0],
      seconds_start: 0, seconds_total: plan.generationSeconds } },
    '6': { class_type: 'EmptyLatentAudio', inputs: { seconds: plan.generationSeconds, batch_size: 1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['1', 0], positive: ['5', 0], negative: ['5', 1],
      latent_image: ['6', 0], seed: cue.seed, steps: plan.steps, cfg: plan.cfg,
      sampler_name: plan.sampler, scheduler: plan.scheduler, denoise: 1 } },
    '8': { class_type: 'VAEDecodeAudio', inputs: { samples: ['7', 0], vae: ['1', 2] } },
    '10': { class_type: 'AudioAdjustVolume', inputs: { audio: ['8', 0], volume: plan.exportGainDb } },
    '9': { class_type: 'SaveAudioAdvanced', inputs: { audio: ['10', 0],
      filename_prefix: `bgm-placeholder-20261123/${cue.id}`, format: 'flac' } },
  };
}

async function request(endpoint, body) {
  const response = await fetch(`${server}${endpoint}`, body ? { method: 'POST',
    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : undefined);
  const result = await response.json();
  if (!response.ok) throw new Error(JSON.stringify(result));
  return result;
}
async function waitForServer() {
  const deadline = Date.now() + 120000;
  while (true) {
    try { return await request('/object_info'); }
    catch (error) {
      if (Date.now() >= deadline) throw error;
      console.log('Waiting for owned ComfyUI server...');
      await new Promise(resolve => setTimeout(resolve, 5000));
    }
  }
}
async function saveJson(file, data, flag = 'w') {
  await writeFile(path.join(root, file), JSON.stringify(data, null, 2), { flag });
}
function sha(content) { return createHash('sha256').update(content).digest('hex'); }
async function exists(file) { return access(file).then(() => true, () => false); }
