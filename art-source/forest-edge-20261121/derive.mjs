import fs from 'node:fs/promises';
import sharp from 'sharp';

const base = 'art-source/forest-edge-20261121';
const plan = JSON.parse(await fs.readFile(`${base}/derivation.json`, 'utf8'));

function polygonSvg(points, width, height) {
    return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><polygon points="${points.map(p => p.join(',')).join(' ')}" fill="white"/></svg>`);
}

async function layerFromPolygon(layer) {
    const [left, top, width, height] = layer.bounds;
    const polygon = layer.polygon.map(([x, y]) => [x - left, y - top]);
    let mask = await sharp(polygonSvg(polygon, width, height)).blur(0.6).png().toBuffer();
    const source = await sharp(plan.source).extract({ left, top, width, height }).ensureAlpha().png().toBuffer();
    if (layer.groundFeather) {
        const rgba = await sharp(source).raw().toBuffer();
        const original = await sharp(mask).ensureAlpha().raw().toBuffer();
        const softened = await sharp(mask).blur(layer.groundFeather.radius).ensureAlpha().raw().toBuffer();
        for (let i = 0; i < width * height; i++) {
            const y = Math.floor(i / width) + top;
            const r = rgba[i * 4], g = rgba[i * 4 + 1], b = rgba[i * 4 + 2];
            const grass = y >= layer.groundFeather.fromY && g > 150 && b < 190 &&
                r - g > -10 && r - g < 30 && g - b > 45;
            if (grass) original[i * 4 + 3] = softened[i * 4 + 3];
        }
        mask = await sharp(original, { raw: { width, height, channels: 4 } }).png().toBuffer();
    }
    return sharp(source)
        .composite([{ input: mask, blend: 'dest-in' }]).png({ compressionLevel: 9 }).toBuffer();
}

async function shelterCutout() {
    const { data, info } = await sharp(plan.shelter.source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const { width, height } = info;
    const visited = new Uint8Array(width * height);
    const queue = [];
    function visit(i) {
        if (visited[i]) return;
        const rgb = [data[i * 4], data[i * 4 + 1], data[i * 4 + 2]];
        if (Math.min(...rgb) < plan.shelter.edgeConnectedWhite.minChannel ||
            Math.max(...rgb) - Math.min(...rgb) > plan.shelter.edgeConnectedWhite.maxChannelSpread) return;
        visited[i] = 1;
        queue.push(i);
    }
    for (let x = 0; x < width; x++) { visit(x); visit((height - 1) * width + x); }
    for (let y = 0; y < height; y++) { visit(y * width); visit(y * width + width - 1); }
    for (let head = 0; head < queue.length; head++) {
        const i = queue[head], x = i % width, y = Math.floor(i / width);
        data[i * 4 + 3] = 0;
        if (x > 0) visit(i - 1);
        if (x < width - 1) visit(i + 1);
        if (y > 0) visit(i - width);
        if (y < height - 1) visit(i + width);
    }
    let minX = width, minY = height, maxX = 0, maxY = 0;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        if (data[(y * width + x) * 4 + 3] < 128) continue;
        minX = Math.min(minX, x); maxX = Math.max(maxX, x);
        minY = Math.min(minY, y); maxY = Math.max(maxY, y);
    }
    const bounds = { left: Math.max(0, minX - 2), top: Math.max(0, minY - 2),
        width: Math.min(width - minX, maxX - minX + 5), height: Math.min(height - minY, maxY - minY + 5) };
    const png = await sharp(data, { raw: info }).extract(bounds)
        .resize({ width: plan.shelter.resizeLongEdge, withoutEnlargement: true })
        .png({ compressionLevel: 9 }).toBuffer();
    return { png, bounds };
}

const layers = [];
for (const layer of plan.layers) layers.push({ ...layer, png: await layerFromPolygon(layer) });
const shelter = await shelterCutout();
layers.push({ slug: 'shelter', png: shelter.png, position: plan.shelter.position,
    sourceBounds: shelter.bounds });
const catalog = JSON.parse(await fs.readFile('art-source/resource-catalog.json', 'utf8'));
for (const layer of layers) {
    const dir = `${base}/landmark-${layer.slug}`;
    await fs.mkdir(dir, { recursive: true });
    const unquantized = layer.png;
    await fs.writeFile(`${dir}/master.png`, unquantized);
    const palette = plan.pngBudget.palette;
    const quantized = unquantized.length > plan.pngBudget.maxBytes;
    if (quantized) layer.png = await sharp(unquantized).png({ palette: true, colours: palette.colours,
        dither: palette.dither, compressionLevel: 9 }).toBuffer();
    await fs.writeFile(`${dir}/candidate.png`, layer.png);
    const metadata = await sharp(layer.png).metadata();
    const asset = catalog.assets.find(item => item.id === `city-map.forest-edge.place.${layer.slug}`);
    asset.status = 'candidate';
    asset.files = asset.files.map(file => ['candidate', 'runtime'].includes(file.role) ? { ...file, role: 'reference' } : file);
    asset.files = asset.files.filter(file => file.path !== `${dir}/candidate.png`);
    asset.files.push({ role: 'candidate', path: `${dir}/candidate.png` });
    if (!asset.files.some(file => file.path === `${dir}/master.png`)) asset.files.push({ role: 'master', path: `${dir}/master.png` });
    asset.generation.notes = `ComfyUI母图同源派生；完整轮廓/位置/抠白/缩放规则见${base}/derivation.json，执行derive.mjs。地点保持源像素、不放大；救助站单独模型生成后抠白缩小。超60KiB者转256色PNG，原RGBA保留master；候选透明边缘待用户/目标验收。`;
    await fs.writeFile(`${dir}/placement.json`, JSON.stringify({ source: layer.slug === 'shelter' ? plan.shelter.source : plan.source,
        sourceBounds: layer.bounds ?? layer.sourceBounds, polygon: layer.polygon,
        left: layer.position[0], top: layer.position[1], width: metadata.width, height: metadata.height,
        quantized, originalBytes: unquantized.length, candidateBytes: layer.png.length }, null, 2));
}
const assembled = await sharp(plan.terrain).composite(layers.map(layer => ({
    input: layer.png, left: layer.position[0], top: layer.position[1],
}))).png().toBuffer();
await fs.writeFile(`${base}/assembled.png`, assembled);
await sharp(assembled).jpeg({ quality: 88, mozjpeg: true }).toFile(`${base}/assembled.jpg`);
const overview = catalog.assets.find(item => item.id === 'city-map.forest-edge.overview');
overview.files = overview.files.map(file => file.role === 'candidate' ? { ...file, role: 'reference' } : file);
overview.files = overview.files.filter(file => !file.path.endsWith('/assembled.jpg') && !file.path.endsWith('/assembled.png'));
overview.files.push({ role: 'candidate', path: `${base}/assembled.jpg` }, { role: 'reference', path: `${base}/assembled.png` });
if (!process.argv.includes('--images-only')) {
    await fs.writeFile('art-source/resource-catalog.json', `${JSON.stringify(catalog, null, 2)}\n`);
}
const previews = [];
for (let i = 0; i < layers.length; i++) {
    for (let dark = 0; dark < 2; dark++) {
        const tile = await sharp({ create: { width: 384, height: 256, channels: 3,
            background: dark ? '#364548' : '#fffaf0' } })
            .composite([{ input: layers[i].png, gravity: 'center' }]).png().toBuffer();
        previews.push({ input: tile, left: dark * 384, top: i * 256 });
    }
}
await sharp({ create: { width: 768, height: layers.length * 256, channels: 3, background: '#fffaf0' } })
    .composite(previews).jpeg({ quality: 90, mozjpeg: true }).toFile(`${base}/edge-review.jpg`);
console.log(JSON.stringify({ derived: layers.map(layer => layer.slug), shelterCrop: shelter.bounds }));
