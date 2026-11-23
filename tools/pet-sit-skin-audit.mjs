import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {mkdir, readFile, readdir, writeFile} from 'node:fs/promises';
const output = 'test-results/pet-sit-skin/verification-inputs.json';
const files = ['pet-sit-skin.html', 'src/pet-sit-study/rig.js', 'src/pet-sit-study/style.css', 'package.json', 'package-lock.json',
    'tests/pet-sit-skin.test.mjs', 'tests/pet-sit-skin-browser.mjs', 'tools/pet-sit-skin-process.mjs', 'tools/pet-sit-skin-audit.mjs', 'tools/lib/image-atlas.mjs',
    'public/pet-sit-skin/atlas.png', 'public/pet-sit-skin/atlas.json', 'art-source/pet-sit-skin-20261123/plan.json', 'art-source/pet-sit-skin-20261123/derivation.json',
    'node_modules/three/package.json', 'node_modules/three/build/three.core.js', 'node_modules/three/build/three.module.js', 'node_modules/three/examples/jsm/animation/CCDIKSolver.js'];
const manifest = JSON.parse(await readFile('public/pet-sit-skin/atlas.json', 'utf8'));
files.push(...manifest.inputs.map(input => input.path));
for (const file of await readdir('src/pet-sit-skin')) files.push(`src/pet-sit-skin/${file}`);
const inputs = await Promise.all([...new Set(files)].sort().map(async path => ({path, sha256: createHash('sha256').update(await readFile(path)).digest('hex')})));
if (process.argv.includes('--freeze')) {await mkdir('test-results/pet-sit-skin', {recursive: true}); await writeFile(output, JSON.stringify({createdAt: new Date().toISOString(), inputs}, null, 2)); console.log(`Frozen ${inputs.length} skin inputs`);}
else if (process.argv.includes('--verify')) {assert.deepEqual(inputs, JSON.parse(await readFile(output, 'utf8')).inputs); console.log(`PASS ${inputs.length} frozen skin inputs unchanged`);}
else throw new Error('Specify --freeze or --verify');
