import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('.', import.meta.url));
const [work] = process.argv.slice(2);
const generation = path.join(work, 'generation');
const archived = path.join(root, 'comfy');
const masters = path.join(work, 'comfy/output/map-valley');
await mkdir(archived, { recursive: true });
for (const round of ['initial', 'extra']) {
    const filename = round + '-submissions.json';
    const jobs = JSON.parse(await readFile(path.join(generation, filename), 'utf8'));
    for (const { name, id } of jobs) {
        const response = await fetch('http://127.0.0.1:8197/history/' + id);
        assert(response.ok, 'ComfyUI history request failed');
        const history = (await response.json())[id];
        assert.equal(history?.status.status_str, 'success', 'Job did not finish: ' + name);
        await writeFile(path.join(archived, name + '-history.json'), JSON.stringify(history, null, 2) + '\n');
        await copyFile(path.join(generation, name + '-workflow.json'), path.join(archived, name + '-workflow.json'));
        await copyFile(path.join(masters, name + '_00001_.png'), path.join(archived, name + '_00001_.png'));
    }
    await copyFile(path.join(generation, filename), path.join(archived, filename));
}
const sprites = path.join(work, 'sprites');
const copy = ['AncientGate', 'ValleyTrail', 'MysticForest', 'SnowInn',
    'CandyGate', 'CrystalPortal', 'SkyIslands'];
const preview = path.join(root, 'preview');
await mkdir(preview, { recursive: true });
for (const name of copy) {
    const file = 'T_Valley_' + name + '.png';
    await copyFile(path.join(sprites, file), path.join(preview, file));
}
await copyFile(path.join(sprites, 'source-record.json'), path.join(root, 'source-record.json'));
