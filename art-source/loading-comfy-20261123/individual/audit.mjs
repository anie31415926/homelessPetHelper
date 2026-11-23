import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const base = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(base, '../../..');
const task = await fs.readFile(path.join(root, 'docs/协作任务/20261123-Loading场景-01a20769.md'), 'utf8');
const scope = JSON.parse(task.match(/```collaboration\s*([\s\S]*?)```/)[1]);
async function files(name) {
  const target = path.join(root, name);
  const stat = await fs.stat(target);
  if (!stat.isDirectory()) return [name];
  const result = [];
  for (const item of await fs.readdir(target, { withFileTypes: true })) {
    const next = `${name}${item.name}`;
    result.push(...(item.isDirectory() ? await files(`${next}/`) : [next]));
  }
  return result;
}
const all = [...new Set((await Promise.all(scope.writePaths.filter(p => p.startsWith('unity/'))
  .map(files))).flat())].sort();
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const inputs = [];
for (const file of all) inputs.push({ path: file, sha256: hash(await fs.readFile(path.join(root, file))) });
const comparison = [];
for (const snapshot of process.argv.slice(2)) {
  const changed = [];
  for (const item of inputs) {
    const file = path.join(root, snapshot, item.path.replace('unity/HomelessPetHelper/', ''));
    if (hash(await fs.readFile(file)) !== item.sha256) changed.push(item.path);
  }
  comparison.push({ snapshot, changed });
}
if (comparison.some(item => item.changed.length)) throw Error(JSON.stringify(comparison));
const report = { scope: 'Loading task Unity paths only; not a whole-project fingerprint',
  count: inputs.length, fingerprint: hash(JSON.stringify(inputs)), inputs, comparison };
const out = path.join(root, 'test-results/loading-comfy/individual');
await fs.mkdir(out, { recursive: true });
await fs.writeFile(path.join(out, 'source-comparison.json'), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify({ count: inputs.length, fingerprint: report.fingerprint,
  snapshots: comparison.length, changed: 0 }));
