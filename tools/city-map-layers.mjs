import { createRequire } from 'node:module';
import { mkdir, readFile, writeFile } from 'node:fs/promises';

const require = createRequire(import.meta.url);
const sharp = require(require.resolve('sharp', process.env.CODEX_NODE_MODULES ?
  { paths: [process.env.CODEX_NODE_MODULES] } : undefined));
const folder = 'art-source/city-map-layered';
const output = 'unity/HomelessPetHelper/Assets/Art/CityMap';
const plan = JSON.parse(await readFile(`${folder}/derivation.json`, 'utf8'));
const { width, height } = plan;
const pixels = await sharp(plan.source).removeAlpha().raw().toBuffer();
const terrain = Buffer.from(pixels);
await mkdir(folder, { recursive: true });

function isGrass(index) {
  const r = pixels[index], g = pixels[index + 1], b = pixels[index + 2];
  return g > 209 && r > 145 && b > 130 && g - r > 30 && g - b > 36 && Math.abs(r - b) < 28;
}

const grassRows = Array.from({ length: height }, (_, y) => {
  const samples = [[], [], []];
  for (let x = 0; x < width; x++) {
    const i = (y * width + x) * 3;
    if (isGrass(i) && pixels[i] > 145 && pixels[i + 2] > 130) {
      for (let c = 0; c < 3; c++) samples[c].push(pixels[i + c]);
    }
  }
  return samples.map(values => values.sort((a, b) => a - b)[Math.floor(values.length / 2)]);
});

function contains(x, y, polygon) {
  let inside = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const [xi, yi] = polygon[i];
    const [xj, yj] = polygon[j];
    if ((yi > y) !== (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
}

const layout = { width, height, layers: [] };
const previews = [];
for (const layer of plan.layers) {
  const [left, top, w, h] = layer.bounds;
  const rgba = Buffer.alloc(w * h * 4);
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const src = ((top + y) * width + left + x) * 3;
      const dst = (y * w + x) * 4;
      pixels.copy(rgba, dst, src, src + 3);
      let coverage = 0;
      for (const dy of [0.125, 0.375, 0.625, 0.875]) {
        for (const dx of [0.125, 0.375, 0.625, 0.875]) {
          if (contains(left + x + dx, top + y + dy, layer.polygon)) coverage++;
        }
      }
      rgba[dst + 3] = Math.round(coverage * 255 / 16);
      if (layer.name === 'Porch' && left + x < 557 && top + y < 974 &&
        Math.abs(pixels[src] - pixels[src + 1]) < 8 && pixels[src] > 185) rgba[dst + 3] = 0;
    }
  }

  // Flood only edge-connected grass; enclosed green courtyard flooring stays intact.
  const queue = [];
  const cleared = new Uint8Array(w * h);
  for (let i = 0; i < w * h; i++) {
    if (rgba[i * 4 + 3] === 0 || i < w || i >= w * (h - 1) || i % w === 0 || i % w === w - 1) {
      cleared[i] = 1;
      queue.push(i);
    }
  }
  for (let head = 0; head < queue.length; head++) {
    const i = queue[head], x = i % w, y = Math.floor(i / w);
    rgba[i * 4 + 3] = 0;
    for (const [dx, dy] of [[0, 1], [0, -1], [1, 0], [-1, 0]]) {
      const nx = x + dx, ny = y + dy, n = ny * w + nx;
      if (nx < 0 || nx >= w || ny < 0 || ny >= h || cleared[n]) continue;
      if (!isGrass(((top + ny) * width + left + nx) * 3)) continue;
      cleared[n] = 1;
      queue.push(n);
    }
  }
  const sprite = await sharp(rgba, { raw: { width: w, height: h, channels: 4 } })
    .png({ palette: true, colours: 256, dither: 0, compressionLevel: 9 }).toBuffer();
  await writeFile(`${output}/${layer.asset}`, sprite);
  previews.push({ input: sprite, left, top });
  layout.layers.push({ name: layer.name, path: `${output.replace('unity/HomelessPetHelper/', '')}/${layer.asset}`,
    x: left, y: top, width: w, height: h });

  // Fill a margin beyond the silhouette, including the old painted ground shadow.
  // Only same-height grass is cloned; the visible footpath remains outside these footprints.
  const margin = 14;
  for (let y = Math.max(0, top - margin); y < Math.min(height, top + h + margin); y++) {
    for (let x = Math.max(0, left - margin); x < Math.min(width, left + w + margin); x++) {
      let distance = Infinity;
      for (let i = 0; i < layer.polygon.length; i++) {
        const [ax, ay] = layer.polygon[i];
        const [bx, by] = layer.polygon[(i + 1) % layer.polygon.length];
        const dx = bx - ax, dy = by - ay;
        const t = Math.max(0, Math.min(1, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy)));
        distance = Math.min(distance, Math.hypot(x - ax - t * dx, y - ay - t * dy));
      }
      const inside = contains(x + 0.5, y + 0.5, layer.polygon);
      const opacity = inside ? 1 : Math.max(0, Math.min(1, (margin - distance) / 6));
      if (!opacity) continue;
      const dst = (y * width + x) * 3;
      if (layer.name === 'Porch' && x < 555 && y < 970 &&
        Math.abs(pixels[dst] - pixels[dst + 1]) < 8 && pixels[dst] > 185) continue;
      for (let c = 0; c < 3; c++) {
        const color = grassRows[y][c];
        terrain[dst + c] = Math.round(color * opacity + terrain[dst + c] * (1 - opacity));
      }
    }
  }
}
const ground = await sharp(terrain, { raw: { width, height, channels: 3 } }).png().toBuffer();
await writeFile(`${folder}/terrain-master.png`, ground);
await sharp(ground).jpeg({ quality: 82, mozjpeg: true }).toFile(plan.background);
await sharp(ground).composite(previews).png().toFile(`${folder}/assembled.png`);
const edgePreviews = [];
for (let i = 0; i < previews.length; i++) {
  for (let side = 0; side < 2; side++) {
    const tile = await sharp({ create: { width: 384, height: 272, channels: 3,
      background: side ? '#293a43' : '#ffffff' } }).composite([{ input: previews[i].input, gravity: 'center' }]).png().toBuffer();
    edgePreviews.push({ input: tile, left: side * 384, top: i * 272 });
  }
}
await sharp({ create: { width: 768, height: 1088, channels: 3, background: '#ffffff' } })
  .composite(edgePreviews).png().toFile(`${folder}/edge-review.png`);
await writeFile(`${output}/CityMapLayers.json`, JSON.stringify(layout, null, 2) + '\n');
console.log(JSON.stringify(layout, null, 2));
