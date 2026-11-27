import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import sharp from 'sharp';
import { tailArt, tailHinge } from '../src/pet-affection/rig.js';

const input = new URL('../public/pet-affection/', import.meta.url);
const target = new URL('../unity/HomelessPetHelper/Assets/Art/Station/Doudou/', import.meta.url);
const provenance = new URL('../art-source/pet-affection-20261127/unity-derivation.json', import.meta.url);
const digest = bytes => createHash('sha256').update(bytes).digest('hex');
const names = ['shiba.webp', 'shiba-tail-layers.webp', 'faces.webp'];
const source = await Promise.all(names.map(name => readFile(new URL(name, input))));
const png = bytes => sharp(bytes).png({ compressionLevel: 9 }).toBuffer();
const [stance, layers, faces] = await Promise.all(source.map(png));
const atlasWidth = 1024, atlasHeight = 512;
const atlas = await sharp({ create: { width: atlasWidth, height: atlasHeight, channels: 4, background: '#0000' } })
    .composite([{ input: layers, left: 0, top: 0 }, { input: faces, left: 658, top: 0 },
        { input: Buffer.from('<svg width="4" height="4"><rect width="4" height="4" fill="white"/></svg>'), left: 1020, top: 508 }])
    .png({ compressionLevel: 9 }).toBuffer();
const alpha = await sharp(stance).resize(128, 128).extractChannel(3).raw().toBuffer();
const mask = Buffer.alloc(128 * 128);
for (let y = 2; y < 126; y++) {
    for (let x = 2; x < 126; x++) {
        const face = ((x / 128 - .388) / .185) ** 2 + ((y / 128 - .33) / .13) ** 2 < 1;
        const inside = [-1, 0, 1].every(dy => [-1, 0, 1].every(dx => alpha[(y + dy) * 128 + x + dx] > 240));
        mask[(127 - y) * 128 + x] = !face && inside ? 255 : 0;
    }
}
const layout = {
    size: tailArt.size,
    tail: [tailArt.crop.x, tailArt.crop.y, tailArt.crop.width, tailArt.crop.height],
    tailCell: [tailArt.atlas.tailX, 0, tailArt.crop.width, tailArt.crop.height],
    rootCell: [tailArt.atlas.tailX, tailArt.atlas.rootY, tailArt.crop.width, tailArt.crop.height],
    face: [108, 107, 181, 133],
    smileCell: [658, 0, 181, 133],
    laughCell: [839, 0, 181, 133],
    hingeFrom: [tailHinge.from.x, tailHinge.from.y],
    hingeTo: [tailHinge.to.x, tailHinge.to.y],
    restTilt: tailHinge.restTilt,
    swingLimit: tailHinge.swingLimit,
    liftLimit: tailHinge.liftLimit,
};
const outputs = {
    'T_Station_Doudou.png': stance,
    'T_Station_DoudouAffection.png': atlas,
    'T_Station_DoudouCareMask.bytes': mask,
    'DoudouLayout.json': Buffer.from(JSON.stringify(layout, null, 2) + '\n'),
};
await mkdir(target, { recursive: true });
for (const [name, bytes] of Object.entries(outputs)) {
    const path = new URL(name, target);
    if (process.argv.includes('--check')) {
        if (!(await readFile(path)).equals(bytes)) throw new Error(`Unity artifact differs: ${name}`);
    } else await writeFile(path, bytes);
}
if (!process.argv.includes('--check')) {
    await writeFile(provenance, JSON.stringify({
        inputs: names.map((name, i) => ({ name, sha256: digest(source[i]) })),
        outputs: Object.entries(outputs).map(([name, bytes]) => ({ name, bytes: bytes.length, sha256: digest(bytes) })),
        layout,
    }, null, 2) + '\n');
}
console.log('Doudou Unity art:', Object.entries(outputs).map(([name, bytes]) => `${name} ${bytes.length}`).join(', '));
