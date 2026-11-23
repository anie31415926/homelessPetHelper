import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {mkdir, readFile, readdir, writeFile} from 'node:fs/promises';

const output = 'test-results/pet-sit-study/verification-inputs.json';
const files = ['pet-sit-study.html', 'package.json', 'package-lock.json', 'tests/pet-sit-study.test.mjs', 'tests/pet-sit-study-browser.mjs', 'tools/pet-sit-study-audit.mjs',
    'node_modules/three/package.json', 'node_modules/three/build/three.core.js', 'node_modules/three/build/three.module.js', 'node_modules/three/examples/jsm/animation/CCDIKSolver.js'];
for (const file of await readdir('src/pet-sit-study')) files.push(`src/pet-sit-study/${file}`);
const inputs = await Promise.all(files.sort().map(async path => ({path, sha256: createHash('sha256').update(await readFile(path)).digest('hex')})));
if (process.argv.includes('--verify')) {
    assert.deepEqual(inputs, JSON.parse(await readFile(output, 'utf8')).inputs); console.log(`PASS ${inputs.length} frozen study inputs unchanged`);
} else if (process.argv.includes('--freeze')) {
    await mkdir('test-results/pet-sit-study', {recursive: true});
    await writeFile(output, JSON.stringify({createdAt: new Date().toISOString(), scope: 'Standalone anatomical blocking, not motion capture or Unity', inputs}, null, 2));
    console.log(`Frozen ${inputs.length} study inputs`);
} else {throw new Error('Specify --freeze before verification or --verify afterward');}
