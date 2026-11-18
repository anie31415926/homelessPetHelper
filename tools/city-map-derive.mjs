import { createRequire } from 'node:module';
import { writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
  { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/city-map-neighborhood';
const source = `${folder}/simplified-master.png`;
// The reference subject leaked into the generated map. Replace only its grass footprint
// with clean grass from the same generated image; never redraw or move the landmarks.
const raw = await sharp(source).removeAlpha().raw().toBuffer();
const patchPixels = await sharp(source).extract({ left: 300, top: 0, width: 194, height: 255 })
  .removeAlpha().raw().toBuffer();
// Match each row to undisturbed nearby grass, so cloning does not leave a tinted rectangle.
for (let y = 0; y < 255; y++) {
  for (let channel = 0; channel < 3; channel++) {
    let expected = 0;
    let observed = 0;
    for (let x = 0; x < 194; x++) observed += patchPixels[(y * 194 + x) * 3 + channel];
    for (let x = 495; x < 520; x++) expected += raw[((y + 580) * 768 + x) * 3 + channel];
    const offset = expected / 25 - observed / 194;
    for (let x = 0; x < 194; x++) {
      const index = (y * 194 + x) * 3 + channel;
      patchPixels[index] = Math.max(0, Math.min(255, Math.round(patchPixels[index] + offset)));
    }
  }
}
const patch = await sharp(patchPixels, { raw: { width: 194, height: 255, channels: 3 } }).png().toBuffer();
const pixels = Buffer.alloc(194 * 255, 255);
for (let y = 0; y < 255; y++) {
  for (let x = 0; x < 194; x++) {
    const edge = Math.min(x, y, 193 - x, 254 - y);
    pixels[y * 194 + x] = Math.round(255 * Math.min(1, edge / 10));
  }
}
const alpha = await sharp(pixels, { raw: { width: 194, height: 255, channels: 1 } }).png().toBuffer();
const masked = await sharp(patch).removeAlpha().joinChannel(await sharp(alpha).raw().toBuffer(),
  { raw: { width: 194, height: 255, channels: 1 } }).png().toBuffer();
const result = await sharp(source).composite([{ input: masked, left: 294, top: 580 }]).png().toBuffer();
await writeFile(`${folder}/selected-master.png`, result);
await sharp(result).flatten({ background: '#ffffff' }).jpeg({ quality: 82, mozjpeg: true })
  .toFile('unity/HomelessPetHelper/Assets/EditorScenes/CityMapArt/T_CityMap_Neighborhood.jpg');
await writeFile(`${folder}/postprocess.json`, JSON.stringify({ source,
  reason: 'Remove leaked reference cat, retain all generated landmarks and paths.',
  grassSource: { left: 300, top: 0, width: 194, height: 255 },
  destination: { left: 294, top: 580 }, featherPixels: 10,
  colorMatch: 'Per-row RGB mean matched to unchanged grass x495..519 at the destination y.',
  runtime: { width: 768, height: 1344, format: 'jpeg', quality: 82, mozjpeg: true }
}, null, 2));
