import { createRequire } from 'node:module';
import { readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
  { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'unity/HomelessPetHelper/Assets/Art/Station';
for (const kind of ['Dog', 'Cat']) {
  const master = await readFile(`art-source/station-care-temporary/${kind}-master.png`);
  const { data, info } = await sharp(master).resize(512, 512, { fit: 'inside' }).ensureAlpha().raw()
    .toBuffer({ resolveWithObject: true });
  // Remove only near-invisible generation speckles, without cropping or moving the silhouette.
  for (let index = 3; index < data.length; index += 4) {
    if (data[index] < 24) data.fill(0, index - 3, index + 1);
  }
  const png = await sharp(data, { raw: info }).png({ compressionLevel: 9 }).toBuffer();
  await writeFile(`${folder}/T_Station_${kind}Care.png`, png);
  const alpha = await sharp(png).resize(128, 128).ensureAlpha().raw().toBuffer();
  const mask = Buffer.alloc(128 * 128);
  for (let y = 2; y < 126; y++) {
    for (let x = 2; x < 126; x++) {
      const inside = [-2, -1, 0, 1, 2].every(dy => [-2, -1, 0, 1, 2]
        .every(dx => alpha[((y + dy) * 128 + x + dx) * 4 + 3] > 240));
      // Reserve the central face for readable eyes/nose and avoid treating it as washable fur.
      const face = ((x / 128 - 0.5) / 0.22) ** 2 + ((y / 128 - 0.42) / 0.15) ** 2 < 1;
      mask[(127 - y) * 128 + x] = inside && !face ? 255 : 0;
    }
  }
  await writeFile(`${folder}/T_Station_${kind}CareMask.bytes`, mask);
  await sharp(png).flatten({ background: '#f0f7f3' }).png().toFile(`art-source/station-care-temporary/${kind}-light.png`);
  await sharp(png).flatten({ background: '#333a3d' }).png().toFile(`art-source/station-care-temporary/${kind}-dark.png`);
  console.log(kind, info.width, info.height, png.length, 'bytes');
}
