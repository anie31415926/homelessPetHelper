import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { transform } from 'esbuild';
import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';

// Rasterize this repository's existing prototype art; no gameplay is imported.
const require = createRequire(import.meta.url);
const modules = process.env.CODEX_NODE_MODULES;
const sharp = require(require.resolve('sharp', modules ? { paths: [modules] } : undefined));
const source = await readFile('src/Pet.jsx', 'utf8');
const result = await transform(source, { loader: 'jsx', format: 'cjs', jsx: 'automatic' });
const module = { exports: {} };
new Function('require', 'module', 'exports', result.code)(require, module, module.exports);
const folder = 'unity/HomelessPetHelper/Assets/EditorScenes/StationArt';
await mkdir(folder, { recursive: true });
for (const kind of ['cat', 'dog']) {
  const svg = renderToStaticMarkup(React.createElement(module.exports.Pet, { kind, dirty: 0 }));
  const input = Buffer.from(svg);
  await sharp(input).resize(600, 640).png().toFile(path.join(folder, `T_Station_${kind}.png`));
  const { data } = await sharp(input).resize(128, 128, { fit: 'fill' })
    .ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const mask = Buffer.alloc(128 * 128);
  for (let y = 0; y < 128; y++) {
    for (let x = 0; x < 128; x++) {
      // Exclude transparent edges/shadow and eyes/nose from the cleanable coat.
      const sx = x / 128 * 300;
      const sy = y / 128 * 320;
      const face = ((sx - 112) / 15) ** 2 + ((sy - 144) / 18) ** 2 < 1 ||
        ((sx - 188) / 15) ** 2 + ((sy - 144) / 18) ** 2 < 1 ||
        ((sx - 150) / 20) ** 2 + ((sy - 164) / 16) ** 2 < 1;
      const inside = x > 0 && x < 127 && y > 0 && y < 127 &&
        [-1, 0, 1].every(dy => [-1, 0, 1].every(dx => data[((y + dy) * 128 + x + dx) * 4 + 3] > 240));
      mask[(127 - y) * 128 + x] = inside && !face ? 255 : 0;
    }
  }
  await writeFile(path.join(folder, `T_Station_${kind}Mask.bytes`), mask);
}
