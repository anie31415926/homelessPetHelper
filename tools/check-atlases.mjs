import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { inputs } from './pet-motion-atlas.mjs';
import { hashMesh, snapshotFiles } from './lib/atlas-manifest.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const project = 'unity/HomelessPetHelper/';
const folder = `${project}Assets/Art/PetMotion/`;
const manifestPath = `${project}Assets/Settings/Atlas/PetAtlasManifest.json`;

// Never issue a new manifest until both deterministic generators accept their outputs.
for (const script of ['pet-motion-atlas.mjs', 'pet-motion-unity.mjs']) {
    const result = spawnSync(process.execPath, [`tools/${script}`, '--check'], {cwd: root, encoding: 'utf8'});
    if (result.status !== 0) throw new Error(`${script} failed:\n${result.stderr}\n${result.stdout}`);
}
const paths = ['package.json', 'package-lock.json', 'tools/pet-motion-atlas.mjs', 'tools/pet-motion-unity.mjs',
    'tools/check-atlases.mjs', 'art-source/pet-motion/atlas.json', 'art-source/pet-motion/unity-export.json',
    `${folder}T_Pet_Atlas.png`, `${folder}Sh_PetMotion.shader`,
    `${project}Assets/EditorScenes/PetMotion/PetMotionFixtures.json`, ...Object.values(inputs),
    ...Object.keys(inputs).map(id => `${folder}T_Pet_${id}.png`)];
paths.push('tools/lib/image-atlas.mjs', 'tools/lib/atlas-padding.mjs', 'tools/lib/atlas-manifest.mjs',
    'src/pet-mesh/skin.js', ...['motion', 'limbs', 'rest-skin', 'sit-rest', 'side-rest']
        .map(name => `src/pet-layers/${name}.js`));
const data = JSON.parse(await fs.readFile(path.join(root, 'art-source/pet-motion/unity-export.json'), 'utf8'));
const meshes = [...data.rigs, data.sources[0]].map(rig => ({
    id: rig.id, path: `Assets/Art/PetMotion/Mesh_${rig.id}.asset`, sha256: hashMesh(rig),
}));
const manifest = {schema: 1, files: await snapshotFiles(root, paths), meshes};
const content = JSON.stringify(manifest, null, 2) + '\n';
if (process.argv.includes('--write')) {
    await fs.mkdir(path.dirname(path.join(root, manifestPath)), {recursive: true});
    await fs.writeFile(path.join(root, manifestPath), content);
} else if (await fs.readFile(path.join(root, manifestPath), 'utf8') !== content) {
    throw new Error('Stale atlas manifest; run npm run atlas:generate, then Unity Atlas/Validate All.');
}
console.log(`Atlas generation consistent: ${manifest.files.length} file hashes, ${meshes.length} expected Unity meshes.`);
