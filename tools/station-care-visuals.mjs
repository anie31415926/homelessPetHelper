import sharp from 'sharp';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const art = new URL('../unity/HomelessPetHelper/Assets/Art/', import.meta.url);
const size = 128;
const atlas = `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<defs>
 <linearGradient id="rim" x1="0" y1="0" x2="1" y2="1" gradientUnits="objectBoundingBox">
  <stop stop-color="#fffef5"/><stop offset=".32" stop-color="#e4fcfb"/><stop offset=".58" stop-color="#84bdc9"/><stop offset=".83" stop-color="#e6bfd9"/><stop offset="1" stop-color="#fff7d6"/>
 </linearGradient>
 <radialGradient id="glass" cx=".38" cy=".32" r=".65" gradientUnits="objectBoundingBox">
  <stop stop-color="#effaf1" stop-opacity=".12"/><stop offset=".68" stop-color="#c2edf2" stop-opacity=".14"/><stop offset=".87" stop-color="#b0dde4" stop-opacity=".55"/><stop offset="1" stop-color="#eac9e8" stop-opacity=".8"/>
 </radialGradient>
 <linearGradient id="tuft" x2=".8" y2="1" gradientUnits="objectBoundingBox">
  <stop stop-color="#fff2cc"/><stop offset=".55" stop-color="#dca572"/><stop offset="1" stop-color="#a96950"/>
 </linearGradient>
</defs>
<circle cx="64" cy="64" r="52" fill="url(#glass)" stroke="#70a3ae" stroke-opacity=".32" stroke-width="5"/>
<circle cx="64" cy="64" r="50" fill="none" stroke="url(#rim)" stroke-width="4"/>
<path d="M26 48q7-16 23-20" fill="none" stroke="#fffef4" stroke-width="6" stroke-linecap="round"/>
<ellipse cx="53" cy="24" rx="3" ry="2" fill="#fff"/>
<path d="M99 76q-5 15-19 22" fill="none" stroke="#fff4ed" stroke-opacity=".82" stroke-width="4" stroke-linecap="round"/>
<path d="M192 21Q198 57 235 64Q198 69 192 107Q185 71 149 64Q185 57 192 21Z" fill="#fff5d6" stroke="#e8ba89" stroke-width="3" stroke-linejoin="round"/>
<path d="M192 45q4 16 19 19q-15 4-19 19q-3-15-18-19q15-3 18-19Z" fill="#fff"/>
<path d="M11 209q20-35 51-39q22-2 39-24q4 16-8 28q13-4 24-17q-1 19-17 32l16-6q-20 34-61 33q-20 0-35 11q5-10 15-16q-14 1-24 6Z" fill="url(#tuft)" stroke="#ae795b" stroke-width="2" stroke-linejoin="round"/>
<g fill="none" stroke-linecap="round">
 <path d="M29 207q22-18 45-20q17-2 26-15M43 214q25-12 42-11q13-3 19-10" stroke="#965d48" stroke-opacity=".64" stroke-width="2.2"/>
 <path d="M32 199q20-19 42-20q12-4 20-13M52 201q19-9 31-8" stroke="#fff2d7" stroke-width="3"/>
</g>
</svg>`;

const alphaAt = (rgba, index) => rgba[index * 4 + 3];
const mix = (a, b, t) => Math.round(a + (b - a) * t);
const clamp = (x, min, max) => Math.max(min, Math.min(max, x));

async function project(kind) {
  const old = await readFile(new URL(`Station/T_Station_${kind}CareMask.bytes`, art)).catch(error => {
    if (error.code !== 'ENOENT') throw error;
    return readFile(new URL(`Station/T_Station_${kind}TemporaryMask.bytes`, art));
  });
  if (old.length !== size * size) throw new Error('Unexpected legacy mask dimensions');
  const sprite = new URL(kind === 'Cat' ? 'PetMotion/T_Pet_original.png' : 'PetSit/T_Station_ShibaMotion.png', art);
  const sourceImage = sharp(await readFile(sprite));
  if (kind === 'Dog') sourceImage.extract({ left: 0, top: 0, width: size, height: size });
  const raster = await sourceImage.resize(size, size, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
  const rows = values => Array.from({ length: size }, (_, y) =>
    Array.from({ length: size }, (_, x) => y * size + x).filter(values));
  const srcRows = rows(index => old[index] !== 0).filter(row => row.length > 0);
  const dstRows = rows(index => alphaAt(raster, (size - 1 - Math.floor(index / size)) * size +
    index % size) >= 80).filter(row => row.length > 0);
  if (srcRows.length === 0 || dstRows.length === 0) throw new Error('Empty source or target silhouette');

  // RG offset is a bottom-left source index, B is new silhouette alpha, A is painted fur opacity.
  const out = Buffer.alloc(size * size * 4);
  dstRows.forEach((row, y) => {
    const source = srcRows[mix(0, srcRows.length - 1, y / Math.max(1, dstRows.length - 1))];
    row.forEach((index, x) => {
      const offset = index * 4;
      const column = index % size;
      const line = Math.floor(index / size);
      const rasterIndex = (size - 1 - line) * size + column;
      const furRow = Math.floor(line / 8);
      const shift = (furRow * 11 + 5) % 17;
      const phase = ((column + shift) % 17) / 17;
      const center = 2 + 3 * phase * phase;
      const stroke = Math.max(0, 1 - Math.abs(line % 8 - center - 0.4 * Math.sin(phase * 7)) / 1.4);
      const taper = Math.sin(Math.PI * phase) ** 0.7;
      out.writeUInt16LE(source[mix(0, source.length - 1, x / Math.max(1, row.length - 1))], offset);
      out[offset + 2] = alphaAt(raster, rasterIndex);
      out[offset + 3] = clamp(Math.round(stroke * taper * 225), 0, 255);
    });
  });
  await writeFile(new URL(`Station/T_Station_${kind}CareProjection.bytes`, art), out);
}

await project('Dog');
if (!process.argv.includes('--dog-only')) {
  await project('Cat');
  await sharp(Buffer.from(atlas)).png({ palette: true, quality: 92, effort: 10 }).toFile(
    fileURLToPath(new URL('Station/T_Station_CareEffects.png', art)));
}
