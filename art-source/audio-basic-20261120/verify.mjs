import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const current = path.join(root, 'v2');
const manifest = JSON.parse(await readFile(path.join(current, 'manifest.json')));
const plan = JSON.parse(await readFile(path.join(current, 'plan.json')));
assert.equal(manifest.assets.length, 6);
assert.equal(new Set(manifest.assets.map(asset => asset.id)).size, 6);
const hashes = {};
for (const asset of manifest.assets) {
  for (const file of [asset.source, asset.candidate]) {
    const data = await readFile(path.join(current, file.file));
    assert.equal(hash(data), file.sha256, `Changed audio: ${file.file}`);
    hashes[`v2/${file.file}`] = file.sha256;
  }
  const graph = JSON.parse(await readFile(path.join(current, asset.workflow)));
  const history = JSON.parse(await readFile(path.join(current, asset.history)));
  const submission = JSON.parse(await readFile(path.join(current, 'history', `${asset.id}-submission.json`)));
  const cue = plan.cues.find(item => item.id === asset.id);
  assert.equal(graph['3'].inputs.text, cue.prompt);
  assert.equal(graph['7'].inputs.seed, cue.seed);
  assert.deepEqual(history.prompt[2], graph, 'History does not match archived workflow');
  assert.equal(history.prompt[1], submission.prompt_id);
  assert.equal(history.status.status_str, 'success');
  assert.equal(history.status.completed, true);
  assert.equal(asset.source.clippedSamples, 0);
  assert.equal(asset.candidate.clippedSamples, 0);
  assert.ok(asset.candidate.seconds > 0.02 && asset.candidate.seconds <= cue.maxSeconds + 0.001);
  assert.ok(asset.candidate.rmsDbfs > -50);
  assert.ok(Math.abs(asset.candidate.peakDbfs - cue.peakDbfs) < 0.05);
  assert.equal(asset.review.listening, 'pending');
}
assert.equal(hash(await readFile(path.join(current, manifest.audition.file))), manifest.audition.sha256);
for (const file of ['prepare.mjs', 'generate.mjs', 'process.py', 'v2/plan.json', 'v2/manifest.json', 'provenance/models.json']) {
  hashes[file] = hash(await readFile(path.join(root, file)));
}
await writeFile(path.join(root, 'verification.json'), JSON.stringify({
  checkedAt: new Date().toISOString(), result: 'pass', currentManifest: 'v2/manifest.json',
  checks: ['6 unique cues', 'source/candidate/preview SHA-256', 'completed ComfyUI job receipts',
    'prompt and seed correspondence', 'no source or candidate clipping', 'duration/peak/non-silence'],
  notChecked: ['manual listening', 'speech/music detection', 'commercial clearance', 'Unity import', 'WeChat device'],
  hashes,
}, null, 2) + '\n');
console.log('PASS: 6 candidate cues, provenance and signal checks. Listening/rights/device review pending.');

function hash(data) { return createHash('sha256').update(data).digest('hex'); }
