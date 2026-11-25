import fs from 'node:fs/promises';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath, pathToFileURL} from 'node:url';
import sharp from 'sharp';

const art = 'unity/HomelessPetHelper/Assets/Art/CityMap/';
const inputs = [
    ['CatCalico', 'Animals/T_Animal_CatCalico.png'],
    ['CatOrange', 'Animals/T_Animal_CatOrange.png'],
    ['DogWhite', 'Animals/T_Animal_DogWhite.png'],
    ['Rabbit', 'Animals/T_Animal_Rabbit.png'],
    ['CloudPark', 'StorybookLandmarks/T_Storybook_CloudPark.png'],
    ['FlowerGarden', 'StorybookLandmarks/T_Storybook_FlowerGarden.png'],
    ['Treehouse', 'StorybookLandmarks/T_Storybook_Treehouse.png'],
    ['Shelter', 'StorybookLandmarks/T_Storybook_Shelter.png'],
];
const hash = bytes => createHash('sha256').update(bytes).digest('hex');

export async function resizeVariant(source, longEdge) {
    if (!Number.isInteger(longEdge) || longEdge < 1 || longEdge > 4096)
        throw new Error('Invalid long edge');
    const buffer = await sharp(source).resize({width: longEdge, height: longEdge,
        fit: 'inside', withoutEnlargement: true, kernel: sharp.kernel.lanczos3})
        .png({compressionLevel: 9, palette: false}).toBuffer();
    const {width, height} = await sharp(buffer).metadata();
    return {buffer, width, height, bytes: buffer.length, rgba32Bytes: width * height * 4};
}

export async function renderAtSize(source, longEdge, background, footprint) {
    const {width, height} = footprint ?? await sharp(source).metadata();
    const scale = longEdge / Math.max(width, height);
    // Keep the same display footprint, including when a smaller candidate needs upscaling.
    const resized = await sharp(source).resize({width: Math.round(width * scale),
        height: Math.round(height * scale), kernel: sharp.kernel.linear}).png().toBuffer();
    return sharp(resized).flatten({background}).png().toBuffer();
}

async function displayRmse(reference, candidate, longEdge, background) {
    const footprint = await sharp(reference).metadata();
    const a = await sharp(await renderAtSize(reference, longEdge, background, footprint)).raw().toBuffer();
    const b = await sharp(await renderAtSize(candidate, longEdge, background, footprint)).raw().toBuffer();
    if (a.length !== b.length) throw new Error('Display footprints differ');
    let sum = 0;
    for (let i = 0; i < a.length; i++) sum += (a[i] - b[i]) ** 2;
    return Math.round(Math.sqrt(sum / a.length) * 1000) / 1000;
}

async function contactSheet(rows, displayEdge, destination, palette = false) {
    const edges = [null, 192, 128, 96];
    const cellWidth = Math.max(192, displayEdge + 32);
    const rowHeight = displayEdge + 64;
    const width = cellWidth * edges.length;
    const height = rowHeight * rows.length + 44;
    const labels = edges.map((edge, i) => `<text x="${i * cellWidth + 12}" y="27">${edge ?? 'Original'}${edge === null ? '' : ' px' + (palette ? ' palette' : ' RGBA')}</text>`);
    const images = [];
    for (let y = 0; y < rows.length; y++) {
        for (let x = 0; x < edges.length; x++) {
            const row = rows[y];
            const source = edges[x] === null ? row.source : (palette ? row.palettes : row.variants).get(edges[x]);
            const left = x * cellWidth;
            const top = y * rowHeight + 44;
            const {width: sourceWidth, height: sourceHeight} = await sharp(row.source).metadata();
            labels.push(`<text x="${left + 12}" y="${top + 20}">${row.id} | display ${displayEdge}px</text>`);
            labels.push(`<rect x="${left + 8}" y="${top + 30}" width="${cellWidth / 2 - 8}" height="${displayEdge + 8}" fill="#f6f6f6"/>`);
            labels.push(`<rect x="${left + cellWidth / 2}" y="${top + 30}" width="${cellWidth / 2 - 8}" height="${displayEdge + 8}" fill="#303638"/>`);
            const display = await sharp(source).resize({
                width: Math.round(sourceWidth * displayEdge / Math.max(sourceWidth, sourceHeight)),
                height: Math.round(sourceHeight * displayEdge / Math.max(sourceWidth, sourceHeight)),
                kernel: sharp.kernel.linear,
            }).png().toBuffer();
            const {width: w} = await sharp(display).metadata();
            images.push({input: display, left: left + Math.floor((cellWidth - w) / 2), top: top + 34});
        }
    }
    const overlay = Buffer.from(`<svg width="${width}" height="${height}"><g font-family="Segoe UI, sans-serif" font-size="13" fill="#20282a">${labels.join('')}</g></svg>`);
    await sharp({create: {width, height, channels: 4, background: '#e4e8e9'}})
        .composite([{input: overlay}, ...images]).png().toFile(destination);
}

export async function reviewImages(root) {
    const output = path.join(root, 'test-results/image-size-review');
    await fs.mkdir(output, {recursive: true});
    const report = {schema: 1, createdAt: new Date().toISOString(), sharp: sharp.versions.sharp,
        resizeKernel: 'lanczos3', displayKernel: 'linear',
        warning: 'Preview only. PNG bytes and RGBA32 base pixels are NOT Unity/WeChat package bytes. RMSE is not a visual acceptance threshold.',
        images: []};
    const rows = [];
    for (const [id, filename] of inputs) {
        const sourcePath = art + filename;
        const source = await fs.readFile(path.join(root, sourcePath));
        const {width, height, isPalette} = await sharp(source).metadata();
        const entry = {id, sourcePath, sha256: hash(source), width, height,
            bytes: source.length, isPalette, rgba32Bytes: width * height * 4, variants: []};
        const row = {id, source, variants: new Map(), palettes: new Map()};
        for (const edge of [256, 192, 128, 96]) {
            const {buffer, ...facts} = await resizeVariant(source, edge);
            row.variants.set(edge, buffer);
            const file = `${id}-${edge}.png`;
            await fs.writeFile(path.join(output, file), buffer);
            const palette = await sharp(buffer).png({palette: true, colours: 256,
                dither: 0, effort: 7, compressionLevel: 9}).toBuffer();
            const paletteFile = `${id}-${edge}-palette-candidate.png`;
            row.palettes.set(edge, palette);
            await fs.writeFile(path.join(output, paletteFile), palette);
            const displayRmseBySize = {};
            for (const display of [96, 128, 224]) {
                displayRmseBySize[display] = {};
                for (const [name, color] of [['light', '#f6f6f6'], ['dark', '#303638']])
                    displayRmseBySize[display][name] = await displayRmse(source, buffer, display, color);
            }
            entry.variants.push({edge, file, ...facts, sha256: hash(buffer), displayRmseBySize,
                paletteCandidate: {file: paletteFile, bytes: palette.length, lossy: true}});
        }
        if (hash(await fs.readFile(path.join(root, sourcePath))) !== entry.sha256)
            throw new Error(`Source changed during review: ${sourcePath}`);
        rows.push(row);
        report.images.push(entry);
    }
    await contactSheet(rows.slice(0, 4), 128, path.join(output, 'small-icons.png'));
    await contactSheet(rows.filter(row => ['CatOrange', 'DogWhite'].includes(row.id)), 224,
        path.join(output, 'battlefield-icons.png'));
    await contactSheet(rows.slice(4), 192, path.join(output, 'buildings.png'));
    await contactSheet(rows.slice(0, 4), 128, path.join(output, 'small-icons-palette.png'), true);
    await contactSheet(rows.slice(4), 192, path.join(output, 'buildings-palette.png'), true);
    report.toolSha256 = hash(await fs.readFile(fileURLToPath(import.meta.url)));
    await fs.writeFile(path.join(output, 'report.json'), JSON.stringify(report, null, 2) + '\n');
    return report;
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
    const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
    const report = await reviewImages(root);
    for (const image of report.images) {
        console.log(`${image.id}: ${image.width}x${image.height}, ${image.bytes} source bytes; ` +
            image.variants.map(v => `${v.width}x${v.height}: ${v.bytes} PNG / ${v.paletteCandidate.bytes} palette candidate bytes`).join('; '));
    }
    console.log('Preview only: test-results/image-size-review/report.json');
}
