import { createHash } from 'node:crypto';
import { createReadStream, createWriteStream } from 'node:fs';
import { access, mkdir, rename, writeFile } from 'node:fs/promises';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const models = 'C:/Users/Admin/ComfyUI-Shared/models';
const definitions = [
  { repo: 'Comfy-Org/stable-audio-open-1.0', file: 'stable-audio-open-1.0.safetensors', folder: 'checkpoints' },
  { repo: 'ComfyUI-Wiki/t5-base', file: 't5-base.safetensors', folder: 'text_encoders' },
];
await mkdir(path.join(root, 'provenance'), { recursive: true });
const records = [];
for (const definition of definitions) {
  const meta = await getJson(`https://huggingface.co/api/models/${definition.repo}`);
  const tree = await getJson(`https://huggingface.co/api/models/${definition.repo}/tree/${meta.sha}?expand=false`);
  const entry = tree.find(item => item.path === definition.file);
  if (!entry?.lfs?.oid) throw new Error(`Missing trusted file hash: ${definition.file}`);
  const url = `https://huggingface.co/${definition.repo}/resolve/${meta.sha}/${definition.file}?download=true`;
  const target = path.join(models, definition.folder, definition.file);
  await mkdir(path.dirname(target), { recursive: true });
  if (!await access(target).then(() => true, () => false)) {
    const response = await fetch(url);
    if (!response.ok) throw new Error(`Download ${response.status}: ${definition.file}`);
    let downloaded = 0;
    let lastProgress = Date.now();
    const partial = `${target}.audio-basic-download`;
    console.log(`Downloading ${definition.file}: ${entry.size} bytes`);
    const stream = Readable.fromWeb(response.body);
    stream.on('data', chunk => {
      downloaded += chunk.length;
      if (Date.now() - lastProgress > 15000) {
        console.log(`${definition.file}: ${(downloaded / entry.size * 100).toFixed(1)}%`);
        lastProgress = Date.now();
      }
    });
    await pipeline(stream, createWriteStream(partial, { flags: 'wx' }));
    const hash = await sha256(partial);
    if (hash !== entry.lfs.oid) throw new Error(`Downloaded hash mismatch: ${partial}`);
    await rename(partial, target);
  }
  const hash = await sha256(target);
  if (hash !== entry.lfs.oid) throw new Error(`Existing model hash mismatch: ${target}`);
  records.push({ ...definition, revision: meta.sha, sourceUrl: url, bytes: entry.size, sha256: hash,
    localPath: target, licenseMetadata: meta.cardData ?? null });
  await writeFile(path.join(root, 'provenance', `${definition.folder}-repository.json`), JSON.stringify(meta, null, 2));
  console.log(`Verified ${definition.file}: ${hash}`);
}
const source = `https://huggingface.co/${records[0].repo}/raw/${records[0].revision}`;
for (const name of ['LICENSE.md', 'README.md']) {
  const response = await fetch(`${source}/${name}`);
  if (!response.ok) throw new Error(`Metadata download failed: ${name}`);
  await writeFile(path.join(root, 'provenance', `stable-audio-${name}`), await response.text());
}
const templateUrl = 'https://raw.githubusercontent.com/Comfy-Org/workflow_templates/main/templates/audio_stable_audio_example.json';
const template = await (await fetch(templateUrl)).text();
JSON.parse(template);
await writeFile(path.join(root, 'provenance', 'official-workflow-template.json'), template);
await writeFile(path.join(root, 'provenance', 'models.json'), JSON.stringify({
  recordedAt: new Date().toISOString(), models: records, templateUrl,
  templateSha256: createHash('sha256').update(template).digest('hex'),
  review: 'Evaluation candidates only; commercial use conditions require separate review.',
}, null, 2));

async function getJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`Metadata ${response.status}: ${url}`);
  return response.json();
}

async function sha256(file) {
  const hash = createHash('sha256');
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest('hex');
}
