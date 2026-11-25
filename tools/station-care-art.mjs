import { createRequire } from 'node:module';
import { access, mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

// Explicit local ComfyUI workflow; never calls a cloud image service.
const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
  { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const server = process.env.COMFY_URL || 'http://127.0.0.1:8188';
const sourceFolder = 'art-source/station-care-temporary';
const outputFolder = 'unity/HomelessPetHelper/Assets/Art/Station';
if (!['127.0.0.1', 'localhost', '[::1]'].includes(new URL(server).hostname)) {
  throw new Error('This preview generator only accepts a local ComfyUI endpoint.');
}
for (const kind of ['Dog', 'Cat']) {
  for (const file of [`${sourceFolder}/${kind}-master.png`, `${outputFolder}/T_Station_${kind}Care.png`]) {
    const exists = await access(file).then(() => true, () => false);
    if (exists) throw new Error(`Refusing to overwrite existing art; choose a new reviewed batch path: ${file}`);
  }
}
await mkdir(sourceFolder, { recursive: true });
await mkdir(outputFolder, { recursive: true });
for (const [kind, reference, seed] of [
  ['Dog', 'DogWhite', 2026111801], ['Cat', 'CatOrange', 2026111802],
]) {
  const input = `unity/HomelessPetHelper/Assets/Art/CityMap/Animals/T_Animal_${reference}.png`;
  const form = new FormData();
  form.append('image', new Blob([await readFile(input)]), `station-reference-${kind}.png`);
  form.append('overwrite', 'false');
  const uploadResponse = await fetch(`${server}/upload/image`, { method: 'POST', body: form });
  if (!uploadResponse.ok) throw new Error(await uploadResponse.text());
  const upload = await uploadResponse.json();
  const prompt = `Create one full body ${kind === 'Dog' ? 'small pale yellow floppy-eared puppy' : 'small pale orange tabby kitten'} sitting, front view, for a gentle mobile animal shelter game. Use the attached animal only as a visual style reference: simple healing doodle illustration, slightly irregular thick charcoal outlines, extremely simple small dot eyes and tiny mouth, large round head and small body, soft pastel flat colors, almost no shading, no realistic fur texture. Preserve the charming minimal hand drawn style. Center the whole animal with all ears, paws and tail visible and 10 percent empty margin. Clean healthy coat, no dirt, no tools, no accessories, no lettering, no UI. Isolated cutout on a fully transparent background, true alpha, no floor shadow, no checkerboard.`;
  const graph = {
    '1': { class_type: 'UNETLoader', inputs: { unet_name: 'qwen_image_2.1_int8_convrot.safetensors', weight_dtype: 'default' } },
    '2': { class_type: 'CLIPLoader', inputs: { clip_name: 'qwen3vl_8b_int8_convrot.safetensors', type: 'qwen_image', device: 'default' } },
    '3': { class_type: 'VAELoader', inputs: { vae_name: 'qwen_image_2.1_vae_bf16.safetensors' } },
    '4': { class_type: 'LoadImage', inputs: { image: upload.name } },
    '5': { class_type: 'TextEncodeQwenImage21', inputs: { clip: ['2', 0], prompt,
      negative_prompt: 'photorealism, 3d, complex fur, gradients, text, watermark, checkerboard, background scene, cropped paws, extra limbs',
      resolution: 768, 'images.image_1': ['4', 0] } },
    '6': { class_type: 'ModelSamplingAuraFlow', inputs: { model: ['1', 0], shift: 3.1 } },
    '7': { class_type: 'KSampler', inputs: { model: ['6', 0], positive: ['5', 0], negative: ['5', 1],
      latent_image: ['5', 2], seed, steps: 24, cfg: 4, sampler_name: 'euler', scheduler: 'simple', denoise: 1 } },
    '8': { class_type: 'VAEDecode', inputs: { samples: ['7', 0], vae: ['3', 0] } },
    '9': { class_type: 'SaveImage', inputs: { images: ['8', 0], filename_prefix: `station-care-temporary/${kind}` } },
  };
  await writeFile(path.join(sourceFolder, `${kind}-workflow.json`), JSON.stringify({ reference: input, prompt: graph }, null, 2));
  const response = await fetch(`${server}/prompt`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ prompt: graph }) });
  const submitted = await response.json();
  if (!response.ok) throw new Error(JSON.stringify(submitted));
  console.log(kind, 'queued', submitted.prompt_id);
  let result;
  const deadline = Date.now() + 20 * 60 * 1000;
  while (Date.now() < deadline) {
    await new Promise(resolve => setTimeout(resolve, 4000));
    const history = await (await fetch(`${server}/history/${submitted.prompt_id}`)).json();
    result = history[submitted.prompt_id];
    if (result?.status?.status_str === 'error') throw new Error(JSON.stringify(result.status));
    if (result?.outputs?.['9']?.images?.length) break;
  }
  if (!result?.outputs?.['9']?.images?.length) throw new Error('Local generation timed out.');
  const file = result.outputs['9'].images[0];
  const params = new URLSearchParams(file);
  const master = Buffer.from(await (await fetch(`${server}/view?${params}`)).arrayBuffer());
  await writeFile(path.join(sourceFolder, `${kind}-master.png`), master);
  await sharp(master).resize(512, 512, { fit: 'inside', withoutEnlargement: true }).png({ compressionLevel: 9 })
    .toFile(path.join(outputFolder, `T_Station_${kind}Care.png`));
  await writeFile(path.join(sourceFolder, `${kind}-history.json`), JSON.stringify(result, null, 2));
  console.log(kind, 'saved for visual review');
}
