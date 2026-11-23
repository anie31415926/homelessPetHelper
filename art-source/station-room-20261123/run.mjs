import fs from 'node:fs/promises';
import path from 'node:path';
import sharp from 'sharp';

const base = 'art-source/station-room-20261123';
const server = 'http://127.0.0.1:8193';
const [action, slug] = process.argv.slice(2);
const jobs = {
  clean: 'Remove only the tiny white watermark lettering in the bottom right corner, seamlessly restore the existing flowers and rug there. Keep the whole room, furniture, composition, colors and texture identical. Do not add or change anything else.',
  background: 'Remove the large round pink pet bed in the center, the tall cat climbing tree at left, the small potted plant at the base of the tree, the leaning pink pillow and the small wooden cabinet with its potted plant at right. Seamlessly reconstruct the empty wall, wooden floor and fluffy rug behind them. Keep the window, curtains, wall art, hanging brush, existing bottom flowers and original watercolor style and camera EXACTLY. Remove the tiny watermark at bottom right. No new furniture, no animals.',
  bed: 'Extract ONLY the big round cream and pink cushioned pet bed in the center as a transparent RGBA layer. Keep its exact original position, scale, perspective, colors, texture, complete outline and soft contact shadow. Everything else must be fully transparent. No room, no floor, no wall, no text, no other furniture. Do NOT center or resize the object.',
  tree: 'Extract ONLY the tall cat climbing tree at the left AND the small potted plant beside its base as one transparent RGBA layer. Keep the exact original position, scale, perspective, colors and outline. The openings between posts and shelves must be transparent. Everything else fully transparent, no room, rug, bed or floor. Do NOT center or resize the objects.',
  rear: 'Extract ONLY the leaning pink pillow at upper right AND the small wooden nightstand with its potted plant at the right edge as a transparent RGBA layer. Keep their exact original positions, scale, perspective, colors and outlines. Everything else must be fully transparent, no room, floor, bed or text. Do NOT center or resize the objects.',
  plant: 'Extract ONLY the COMPLETE small potted plant standing on the floor at the lower right of the tall cat tree, including ALL its green leaves AND its whole pink flowerpot. Keep its exact original position and scale. Do NOT extract the plant on the windowsill or on the nightstand. Everything else fully transparent RGBA. No floor, bed, tree, wall or text. Preserve the original drawing exactly.',
  empty: 'Remove ONLY the small wooden cabinet and the potted plant sitting on it at the right edge. Reconstruct the empty wall and wooden floor behind them. Leave the window, curtain, windowsill plant, hanging brush, art, rug and flowers EXACTLY unchanged. No furniture, no watermark, no new objects.',
  'plant-local': 'Remove the wall and floor background from this small potted plant. Preserve the COMPLETE plant, all leaves and the whole pink flowerpot, exactly as drawn and at exactly the same position and size. Everything else fully transparent RGBA. Do not redraw or move the plant. No text.',
};
if (!jobs[slug] || !['run', 'collect'].includes(action)) throw Error('Use run|collect clean|background|bed|tree|rear');
const dir = `${base}/${slug}`;
await fs.mkdir(dir, { recursive: true });
const exists = f => fs.access(f).then(() => true, () => false);
async function request(endpoint, options = {}) {
  const r = await fetch(`${server}/${endpoint}`, { ...options, signal: AbortSignal.timeout(30000) });
  if (!r.ok) throw Error(`${r.status} ${await r.text()}`);
  return r;
}
async function submit() {
  if (await exists(`${dir}/submission.json`)) throw Error('Already submitted: collect existing job.');
  const queue = await (await request('queue')).json();
  if (queue.queue_running.length || queue.queue_pending.length) throw Error('Queue is busy.');
  if (!await exists(`${base}/work-reference.png`)) {
    await sharp(`${base}/reference.png`).extract({ left: 8, top: 9, width: 336, height: 600 })
      .resize(352, 608).png().toFile(`${base}/work-reference.png`);
  }
  const form = new FormData();
  if (slug === 'plant-local') await sharp(`${base}/reference.png`)
    .extract({ left: 69, top: 234, width: 54, height: 78 }).resize(224, 320)
    .png().toFile(`${dir}/reference.png`);
  const reference = slug === 'empty' ? `${base}/background/master.png` :
    slug === 'plant-local' ? `${dir}/reference.png` : `${base}/work-reference.png`;
  form.append('image', new Blob([await fs.readFile(reference)]), `station-room-${slug}.png`);
  form.append('overwrite', 'false');
  const uploaded = await (await request('upload/image', { method: 'POST', body: form })).json();
  const graph = {
    '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
    '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
    '4': { class_type: 'LoadImage', inputs: { image: uploaded.name } },
    '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], vae: ['3', 0], prompt: jobs[slug],
      negative_prompt: 'new objects, changed perspective, changed composition, redesigned furniture, text, watermark, checkerboard background, extra legs, people, animals', resolution: 0, 'images.image_1': ['4', 0] } },
    '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1],
      latent_image: ['5', 2], seed: 2026112301 + Object.keys(jobs).indexOf(slug), steps: 24,
      cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
    '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
    '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `station-room/${slug}` } },
  };
  await fs.writeFile(`${dir}/workflow.json`, JSON.stringify({ prompt: graph, source: reference,
    workSize: slug === 'plant-local' ? [224, 320] : [352, 608] }, null, 2));
  const submission = await (await request('prompt', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ prompt: graph, client_id: 'station-room-20261123' }) })).json();
  await fs.writeFile(`${dir}/submission.json`, JSON.stringify(submission, null, 2));
  console.log(JSON.stringify({ slug, promptId: submission.prompt_id }));
  return submission;
}
const submission = action === 'run' ? await submit() : JSON.parse(await fs.readFile(`${dir}/submission.json`, 'utf8'));
const deadline = Date.now() + 25 * 60 * 1000;
while (Date.now() < deadline) {
  const h = await (await request(`history/${submission.prompt_id}`)).json();
  const result = h[submission.prompt_id];
  if (result) {
    await fs.writeFile(`${dir}/history.json`, JSON.stringify(result, null, 2));
    if (result.status?.status_str === 'error') throw Error(JSON.stringify(result.status));
    const output = result.outputs?.['9']?.images?.[0];
    if (output) {
      await fs.writeFile(`${dir}/master.png`, Buffer.from(await (await request(`view?${new URLSearchParams(output)}`)).arrayBuffer()));
      console.log(JSON.stringify({ saved: slug, metadata: await sharp(`${dir}/master.png`).metadata() }));
      process.exit(0);
    }
  }
  await new Promise(r => setTimeout(r, 10000));
}
throw Error('Timeout: use collect, never resubmit.');
