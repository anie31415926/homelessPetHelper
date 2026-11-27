import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import sharp from 'sharp';
import { buildEdgeTexture, edgeRows, edgeWidth } from '../public/pet-fur-flaps/fur-edge.js';
import { distribute, validateProfile } from '../public/pet-fur-flaps/profile.js';

const profileUrl = new URL('../public/pet-fur-flaps/doudou.json', import.meta.url);
const source = await readFile(profileUrl);
const profile = validateProfile(JSON.parse(source));
const portrait = await readFile(new URL(profile.portrait.image, profileUrl));
const output = new URL('../unity/HomelessPetHelper/Assets/EditorScenes/PetFur/', import.meta.url);
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const { width, height, crop } = profile.portrait;
const baked = {
    profile: profile.id,
    profileSha256: hash(source),
    portraitSha256: hash(portrait),
    width, height, crop,
    light: profile.light,
    edge: { width: edgeWidth, rows: edgeRows, data: Buffer.from(buildEdgeTexture()).toString('base64') },
    patches: distribute(profile.regions).map(({ warmthRange, ...patch }) => ({ ...patch,
        minWarmth: warmthRange?.[0] ?? -1.6, maxWarmth: warmthRange?.[1] ?? 1.6 })),
};

await mkdir(output, { recursive: true });
await writeFile(new URL('DoudouFurPreview.json', output), JSON.stringify(baked, null, 2) + '\n');
await writeFile(new URL('T_DoudouFurPreview.png', output), await sharp(portrait).png().toBuffer());
console.log(`Baked ${baked.patches.length} tufts from ${profile.id}`);
