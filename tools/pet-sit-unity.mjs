// Native Unity skin/curve bake of the accepted chibi skeleton. No runtime IK or pose images.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {bakeAppearance} from '../src/pet-sit-chibi/bake.js';
import {defineAppearance} from '../src/pet-sit-chibi/profile.js';
if (!process.execArgv.includes('--jitless')) {
    const child = spawnSync(process.execPath, ['--jitless', fileURLToPath(import.meta.url), ...process.argv.slice(2)], {stdio: 'inherit'});
    process.exit(child.status ?? 1);
}
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const profileIndex = args.indexOf('--profile');
if (args.filter(arg => arg === '--profile').length > 1 ||
    args.some((arg, i) => arg !== '--check' && arg !== '--profile' && !(profileIndex >= 0 && i === profileIndex + 1))) throw new Error('Use --profile <repository JSON path> [--check]');
if (profileIndex >= 0 && (!args[profileIndex + 1] || args[profileIndex + 1].startsWith('--'))) throw new Error('Missing --profile path');
const profilePath = path.resolve(root, profileIndex >= 0 ? args[profileIndex + 1] : 'src/pet-sit-chibi/appearances/cat-orange-side.json');
if (profilePath === path.join(root, 'src/pet-sit-chibi/appearances/cat-orange-side.json')) {
    const child = spawnSync(process.execPath, ['--jitless', path.join(root, 'tools/pet-sit-accepted-unity.mjs'),
        ...args.filter(arg => arg === '--check')], {cwd: root, stdio: 'inherit'});
    process.exit(child.status ?? 1);
}
const profile = defineAppearance(JSON.parse(fs.readFileSync(profilePath, 'utf8')));
const manifest = JSON.parse(fs.readFileSync(path.join(root, profile.art.atlasManifest), 'utf8'));
const data = bakeAppearance(profile, manifest), fixtures = data.fixtures;
const output = path.join(root, profile.export.bakePath), content = JSON.stringify(data) + '\n';
if (process.argv.includes('--check')) {
    if (fs.readFileSync(output, 'utf8') !== content) throw new Error('Stale chibi Unity bake');
} else {
    if (fs.existsSync(output)) {
        const previous = JSON.parse(fs.readFileSync(output, 'utf8'));
        if (previous.appearanceId && previous.appearanceId !== profile.id) throw new Error('Refusing to overwrite another animal appearance');
    }
    fs.mkdirSync(path.dirname(output), {recursive: true}); fs.writeFileSync(output, content);
}
const fixturePath = path.join(root, profile.export.fixturePath);
const fixtureContent = JSON.stringify({fixtures}) + '\n';
if (process.argv.includes('--check')) {
    if (fs.readFileSync(fixturePath, 'utf8') !== fixtureContent) throw new Error('Stale chibi fixtures');
} else {fs.mkdirSync(path.dirname(fixturePath), {recursive: true}); fs.writeFileSync(fixturePath, fixtureContent);}
console.log(JSON.stringify({bytes: Buffer.byteLength(content), appearanceId: profile.id, bones: data.bones.length, meshes: data.meshes.length, vertices: data.meshes.reduce((sum, mesh) => sum + mesh.positions.length / 3, 0), takes: data.takes.map(({id,duration}) => ({id,duration}))}));
