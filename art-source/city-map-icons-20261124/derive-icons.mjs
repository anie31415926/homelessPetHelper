import path from 'node:path';
import { createHash } from 'node:crypto';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import sharp from 'sharp';

const [source, output, masterDir] = process.argv.slice(2);
const icons = [
    [1, 'AncientGate'], [2, 'ValleyTrail'], [3, 'FlowerGarden'], [5, 'MysticForest'], [6, 'TreeHouse'],
    [7, 'SnowInn'], [8, 'MoonPool'], [9, 'WindMeadow'], [10, 'CandyGate'], [11, 'CloudPark'],
    [12, 'CartMine'], [13, 'CrystalPortal'], [14, 'FreezeDryFactory'], [15, 'BubblePool'],
    [16, 'RainbowBridge'], [18, 'MushroomHill'], [19, 'HeartShelter'], [72, 'VegetableYard'], [0, 'SkyIslands'],
];
await mkdir(output, { recursive: true });
const variants = {
    6: ['icons', 0], 12: ['icons', 1], 19: ['icons', 2], 72: ['icons', 3],
    15: ['props', 0], 16: ['props', 2],
};

function cropAlpha(rgb, w, h, white = false) {
    const n = w * h;
    const distance = new Float32Array(n);
    const rgba = Buffer.alloc(n * 4);
    const median = values => values.sort((a, b) => a - b)[Math.floor(values.length / 2)];
    const paper = [];
    for (let y = 0; y < h; y++) {
        paper[y] = [0, 1, 2].map(c => white ? 255 : median([-10, -6, -2, 2, 6, 10].flatMap(dy => {
            const row = Math.min(h - 1, Math.max(0, y + dy)) * w;
            return [2, 8, 14, w - 15, w - 9, w - 3].map(x => rgb[(row + x) * 3 + c]);
        })));
        for (let x = 0; x < w; x++) {
            const i = y * w + x;
            distance[i] = Math.max(...paper[y].map((c, k) => Math.abs(rgb[i * 3 + k] - c)));
        }
    }
    const outside = new Uint8Array(n);
    const queue = new Int32Array(n);
    let head = 0; let tail = 0;
    function visit(i) {
        if (!outside[i] && distance[i] <= (white ? 16 : 18)) { outside[i] = 1; queue[tail++] = i; }
    }
    function neighbors(i, callback) {
        if (i % w) callback(i - 1);
        if (i % w < w - 1) callback(i + 1);
        if (i >= w) callback(i - w);
        if (i + w < n) callback(i + w);
    }
    for (let x = 0; x < w; x++) { visit(x); visit((h - 1) * w + x); }
    for (let y = 0; y < h; y++) { visit(y * w); visit(y * w + w - 1); }
    while (head < tail) neighbors(queue[head++], visit);

    const components = new Int32Array(n);
    const areas = [0];
    for (let i = 0; i < n; i++) {
        if (outside[i] || components[i]) continue;
        const id = areas.length;
        head = 0; tail = 1; queue[0] = i; components[i] = id;
        while (head < tail) {
            neighbors(queue[head++], j => {
                if (!outside[j] && !components[j]) { components[j] = id; queue[tail++] = j; }
            });
        }
        areas.push(tail);
    }
    const main = Math.max(...areas);
    let left = w; let top = h; let right = 0; let bottom = 0;
    for (let i = 0; i < n; i++) {
        const x = i % w; const y = Math.floor(i / w);
        if (areas[components[i]] < Math.max(main * .0075, 30)) continue;
        let edge = false;
        neighbors(i, j => { if (outside[j]) edge = true; });
        const alpha = edge ? Math.min(1, Math.max(.36, distance[i] / 48)) : 1;
        for (let c = 0; c < 3; c++) {
            rgba[i * 4 + c] = Math.max(0, Math.min(255, Math.round(
                (rgb[i * 3 + c] - paper[y][c] * (1 - alpha)) / alpha)));
        }
        rgba[i * 4 + 3] = Math.round(alpha * 255);
        left = Math.min(left, x); top = Math.min(top, y);
        right = Math.max(right, x); bottom = Math.max(bottom, y);
    }
    if (right <= left || bottom <= top) throw new Error('No isolated artwork found');
    return sharp(rgba, { raw: { width: w, height: h, channels: 4 } })
        .extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
        .resize(280, 254, { fit: 'inside', withoutEnlargement: true })
        .extend({ top: 3, bottom: 3, left: 3, right: 3, background: '#0000' });
}

const rows = await Promise.all(icons.map(async ([id, name]) => {
    const filename = id ? `游戏界面地图图标设计 (${id}).png` : '游戏界面地图图标设计.png';
    const contents = await readFile(path.join(source, filename));
    const image = sharp(contents);
    const { width, height, xmpAsString } = await image.metadata();
    // Footer area contains the source stamp; trim the blank paper before masking.
    const inset = id === 13 ? .08 : .024;
    const variant = masterDir && variants[id];
    const extract = variant ? sharp(path.join(masterDir, variant[0] + '_00001_.png')).extract({
        left: variant[1] % 2 * 384, top: Math.floor(variant[1] / 2) * 384, width: 384, height: 384,
    }) : image.extract({
        left: Math.round(width * .024), top: Math.round(height * .023),
        width: Math.round(width * (.976 - inset)), height: Math.round(height * .924),
    }).resize(640, 640, { fit: 'inside' });
    const { data, info } = await extract.removeAlpha().raw().toBuffer({ resolveWithObject: true });
    const target = path.join(output, 'T_Valley_' + name + '.png');
    const cut = cropAlpha(data, info.width, info.height, !!variant);
    if (xmpAsString) cut.withXmp(xmpAsString);
    const result = await cut.png({ palette: true, quality: 95, dither: 0.25 }).toFile(target);
    return { id, name, sourceSha256: createHash('sha256').update(contents).digest('hex'),
        variant: variant || 'source pixels', width: result.width, height: result.height, bytes: result.size };
}));
await writeFile(path.join(output, 'source-record.json'), JSON.stringify(rows, null, 2) + '\n');
console.log(`Prepared ${rows.length} isolated assets, ${rows.reduce((sum, row) => sum + row.bytes, 0)} bytes`);
