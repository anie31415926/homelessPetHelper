import assert from 'node:assert/strict';
import { copyFile, mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const [work, fullReference] = process.argv.slice(2);
const archive = fileURLToPath(new URL('comfy/', import.meta.url));
await mkdir(archive, { recursive: true });
for (const name of ['storybook', 'miniature', 'refined']) {
    const saved = suffix => path.join(work, 'generation', name + '-' + suffix + '.json');
    const { prompt_id: id } = JSON.parse(await readFile(saved('submission'), 'utf8'));
    const response = await fetch('http://127.0.0.1:8198/history/' + id);
    assert(response.ok);
    const history = (await response.json())[id];
    assert.equal(history?.status.status_str, 'success', name);
    await copyFile(saved('submission'), path.join(archive, name + '-submission.json'));
    await copyFile(saved('workflow'), path.join(archive, name + '-workflow.json'));
    await writeFile(path.join(archive, name + '-history.json'), JSON.stringify(history, null, 2) + '\n');
    await copyFile(path.join(work, 'comfy/output/storybook', name + '_00001_.png'),
        path.join(archive, name + '_00001_.png'));
}
await copyFile(fullReference, path.join(archive, 'user-reference.png'));
