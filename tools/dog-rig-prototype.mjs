// 柴犬骨骼蒙皮原型（离线）：单帧图 → 网格 + 骨链权重 → 2D 骨骼驱动 → idle 帧对照。
// 仅验证“一张图能不能被骨骼带着动”，不产出 walk/过渡，也不写入 Unity。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const root = path.resolve(import.meta.dirname, '..');
const sourceSheet = process.env.DOG_RIG_SHEET || path.join(root, 'unity/HomelessPetHelper/Assets/Art/PetSit/T_Station_ShibaMotion.png');
const outDir = process.env.DOG_RIG_OUT || path.join(root, 'art-source/dog-rig-prototype-20261127');
const framesDir = path.join(outDir, 'frames');
const crop = { left: 4, top: 2, width: 126, height: 126 };
const size = crop.width * 2;
const step = 6;

const bones = [
    { name: 'root', parent: -1, pivot: [144, 110] },
    { name: 'spineA', parent: 0, pivot: [176, 110] },
    { name: 'spineB', parent: 1, pivot: [140, 110] },
    { name: 'neck', parent: 2, pivot: [110, 104] },
    { name: 'head', parent: 3, pivot: [80, 76] },
    { name: 'tailA', parent: 0, pivot: [180, 96] },
    { name: 'tailB', parent: 5, pivot: [208, 76] },
    { name: 'frontNearUpper', parent: 2, pivot: [104, 120] },
    { name: 'frontNearLower', parent: 7, pivot: [100, 148] },
    { name: 'frontNearPaw', parent: 8, pivot: [94, 176] },
    { name: 'frontFarUpper', parent: 2, pivot: [109, 118] },
    { name: 'frontFarLower', parent: 10, pivot: [105, 146] },
    { name: 'frontFarPaw', parent: 11, pivot: [99, 174] },
    { name: 'hindNearUpper', parent: 0, pivot: [170, 116] },
    { name: 'hindNearLower', parent: 13, pivot: [178, 148] },
    { name: 'hindNearPaw', parent: 14, pivot: [186, 176] },
    { name: 'hindFarUpper', parent: 0, pivot: [174, 114] },
    { name: 'hindFarLower', parent: 16, pivot: [182, 146] },
    { name: 'hindFarPaw', parent: 17, pivot: [190, 174] }
];

const gates = {
    head: { x0: -24, x1: 100, y0: -24, y1: 96, feather: 14 },
    neck: { x0: 86, x1: 126, y0: 36, y1: 114, feather: 12 },
    tail: { x0: 168, x1: 242, y0: 30, y1: 134, feather: 10 },
    body: { x0: 96, x1: 194, y0: 40, y1: 178, feather: 14 },
    frontLeg: { x0: 66, x1: 114, y0: 116, y1: 251, feather: 10 },
    hindLeg: { x0: 156, x1: 206, y0: 108, y1: 251, feather: 10 }
};

const index = name => bones.findIndex(b => b.name === name);
const legChains = [
    { gate: 'frontLeg', chain: ['frontNearUpper', 'frontNearLower', 'frontNearPaw'] },
    { gate: 'frontLeg', chain: ['frontFarUpper', 'frontFarLower', 'frontFarPaw'] },
    { gate: 'hindLeg', chain: ['hindNearUpper', 'hindNearLower', 'hindNearPaw'] },
    { gate: 'hindLeg', chain: ['hindFarUpper', 'hindFarLower', 'hindFarPaw'] }
].map(item => ({
    gate: item.gate,
    idx: item.chain.map(index),
    segments: item.chain.slice(0, -1).map((name, i) => {
        const a = bones[index(name)].pivot, b = bones[index(item.chain[i + 1])].pivot;
        return { a, dx: b[0] - a[0], dy: b[1] - a[1], len: Math.max(1, (b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2) };
    })
}));

const band = (value, edge, feather, inside) => {
    const signed = inside ? value - edge : edge - value;
    if (signed >= feather) return 1;
    if (signed <= 0) return 0;
    const t = signed / feather;
    return t * t * (3 - 2 * t);
};
const rect = (x, y, r) => band(x, r.x0, r.feather, true) * band(x, r.x1, r.feather, false)
    * band(y, r.y0, r.feather, true) * band(y, r.y1, r.feather, false);

function weightsAt(x, y) {
    const w = new Array(bones.length).fill(0);
    // 层级式：头/尾先占位，余量按 x 分给躯干前后段，四肢只在自身门控内叠加。
    const head = rect(x, y, gates.head);
    const tail = rect(x, y, gates.tail) * Math.max(0, 1 - head);
    const free = Math.max(0, 1 - head - tail);
    w[4] += head;
    w[3] += free * rect(x, y, gates.neck) * 0.9;
    w[5] += tail * band(y, 90, 16, true);
    w[6] += tail * band(y, 90, 16, false);
    let legSum = 0;
    for (const leg of legChains) {
        const gate = rect(x, y, gates[leg.gate]) * free;
        if (gate <= 0) continue;
        const distances = leg.segments.map((s, i) => {
            const t = Math.max(0, Math.min(1, ((x - s.a[0]) * s.dx + (y - s.a[1]) * s.dy) / s.len));
            return { i, d: Math.hypot(x - (s.a[0] + s.dx * t), y - (s.a[1] + s.dy * t)) };
        }).sort((a, b) => Math.abs(a.d) - Math.abs(b.d));
        const near = gate * Math.exp(-((distances[0].d / 14) ** 2));
        const far = distances[1] ? gate * Math.exp(-((distances[1].d / 14) ** 2)) * 0.45 : 0;
        w[leg.idx[distances[0].i]] += near;
        if (distances[1]) w[leg.idx[distances[1].i]] += far;
        legSum += near + far;
    }
    const remain = Math.max(0, 1 - w[3] - w[4] - w[5] - w[6] - legSum);
    w[0] += remain * band(x, 152, 20, true);
    w[1] += remain * band(x, 152, 20, false);
    const sum = w.reduce((a, b) => a + b, 0);
    return sum < 1e-6 ? null : w.map(v => v / sum);
}

const identity = () => [1, 0, 0, 0, 1, 0];
const compose = (a, b) => [
    a[0] * b[0] + a[1] * b[3], a[0] * b[1] + a[1] * b[4], a[0] * b[2] + a[1] * b[5] + a[2],
    a[3] * b[0] + a[4] * b[3], a[3] * b[1] + a[4] * b[4], a[3] * b[2] + a[4] * b[5] + a[5]
];
const rotateAbout = ([cx, cy], deg) => {
    const r = deg * Math.PI / 180, c = Math.cos(r), s = Math.sin(r);
    return [c, -s, cx - c * cx + s * cy, s, c, cy - s * cx - c * cy];
};
const apply = (m, [x, y]) => [m[0] * x + m[1] * y + m[2], m[3] * x + m[4] * y + m[5]];

function matrices(angles) {
    const mats = new Array(bones.length);
    for (let i = 0; i < bones.length; i++) {
        const local = rotateAbout(bones[i].pivot, angles[i] || 0);
        mats[i] = bones[i].parent >= 0 ? compose(mats[bones[i].parent], local) : local;
    }
    return mats;
}

function sample(pixels, w, h, x, y) {
    const x0 = Math.max(0, Math.min(w - 1, Math.floor(x - .5))), y0 = Math.max(0, Math.min(h - 1, Math.floor(y - .5)));
    const x1 = Math.min(w - 1, x0 + 1), y1 = Math.min(h - 1, y0 + 1);
    const fx = Math.max(0, Math.min(1, x - .5 - x0)), fy = Math.max(0, Math.min(1, y - .5 - y0));
    const read = (xx, yy) => { const i = (yy * w + xx) * 4; return [pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]]; };
    const p00 = read(x0, y0), p10 = read(x1, y0), p01 = read(x0, y1), p11 = read(x1, y1);
    return [0, 1, 2, 3].map(k => (p00[k] * (1 - fx) + p10[k] * fx) * (1 - fy) + (p01[k] * (1 - fx) + p11[k] * fx) * fy);
}

async function raster(positions, uv, triangles, pixels, info) {
    const out = new Float32Array(size * size * 4), ss = 2;
    const cross = (a, b, px, py) => (b[0] - a[0]) * (py - a[1]) - (b[1] - a[1]) * (px - a[0]);
    for (const [i0, i1, i2] of triangles) {
        const p0 = positions[i0], p1 = positions[i1], p2 = positions[i2];
        const u0 = uv[i0], u1 = uv[i1], u2 = uv[i2];
        const area = cross(p0, p1, p2[0], p2[1]);
        if (Math.abs(area) < 1e-6) continue;
        const minX = Math.max(0, Math.floor(Math.min(p0[0], p1[0], p2[0])) - 1);
        const maxX = Math.min(size, Math.ceil(Math.max(p0[0], p1[0], p2[0])) + 1);
        const minY = Math.max(0, Math.floor(Math.min(p0[1], p1[1], p2[1])) - 1);
        const maxY = Math.min(size, Math.ceil(Math.max(p0[1], p1[1], p2[1])) + 1);
        for (let y = minY; y < maxY; y++) {
            for (let x = minX; x < maxX; x++) {
                for (let sy = 0; sy < ss; sy++) {
                    for (let sx = 0; sx < ss; sx++) {
                        const px = x + (sx + .5) / ss, py = y + (sy + .5) / ss;
                        const a0 = cross(p1, p2, px, py) * area, a1 = cross(p2, p0, px, py) * area,
                            a2 = cross(p0, p1, px, py) * area;
                        if (a0 < 0 || a1 < 0 || a2 < 0) continue;
                        const sum = a0 + a1 + a2;
                        const u = (a0 * u0[0] + a1 * u1[0] + a2 * u2[0]) / sum;
                        const v = (a0 * u0[1] + a1 * u1[1] + a2 * u2[1]) / sum;
                        const s = sample(pixels, info.width, info.height, u * info.width, v * info.height);
                        const alpha = s[3] / 255 / (ss * ss), k = (y * size + x) * 4;
                        out[k] += s[0] * alpha; out[k + 1] += s[1] * alpha; out[k + 2] += s[2] * alpha; out[k + 3] += alpha;
                    }
                }
            }
        }
    }
    const data = Buffer.alloc(size * size * 4);
    for (let i = 0; i < out.length; i += 4) {
        const a = out[i + 3];
        if (a <= .002) { data[i + 3] = 0; continue; }
        data[i] = Math.min(255, Math.round(out[i] / a));
        data[i + 1] = Math.min(255, Math.round(out[i + 1] / a));
        data[i + 2] = Math.min(255, Math.round(out[i + 2] / a));
        data[i + 3] = Math.min(255, Math.round(a * 255));
    }
    return sharp(data, { raw: { width: size, height: size, channels: 4 } }).png({ compressionLevel: 9 }).toBuffer();
}

async function main() {
    const { data, info } = await sharp(sourceSheet).extract(crop)
        .resize(size, size, { kernel: 'lanczos3' }).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const nodes = [], uv = [];
    const count = Math.floor(size / step) + 1;
    for (let gy = 0; gy < count; gy++) {
        for (let gx = 0; gx < count; gx++) {
            const x = Math.min(gx * step, size - 1), y = Math.min(gy * step, size - 1);
            nodes.push([x, y]); uv.push([x / size, y / size]);
        }
    }
    const weights = nodes.map(([x, y]) => weightsAt(x, y));
    const tris = [];
    for (let y = 0; y < count - 1; y++) {
        for (let x = 0; x < count - 1; x++) {
            const a = y * count + x, b = a + 1, c = a + count, d = c + 1;
            tris.push([a, c, b], [b, c, d]);
        }
    }
    const rest = await raster(nodes, uv, tris, data, info);
    fs.mkdirSync(framesDir, { recursive: true });
    fs.writeFileSync(path.join(outDir, 'rest.png'), rest);
    const restRaw = await sharp(rest).raw().toBuffer({ resolveWithObject: true });
    const poses = [
        { name: 'idle-a', a: [0, 0, 0, 0, 0] },
        { name: 'idle-b', a: [0.2, 0.2, 2.0, 3.0, 10] },
        { name: 'idle-c', a: [0.4, 0.4, 2.8, 4.2, 16] },
        { name: 'idle-d', a: [-0.2, -0.2, -1.2, -2.2, -6] },
        { name: 'idle-e', a: [0, 0, -2.4, -3.6, -14] },
        { name: 'test-strong', a: [1, 1, 3, 6, 18] }
    ];
    const report = [];
    for (const pose of poses) {
        const angles = new Array(bones.length).fill(0);
        angles[1] = pose.a[0]; angles[2] = pose.a[1]; angles[3] = pose.a[2]; angles[4] = pose.a[3];
        angles[5] = pose.a[4]; angles[6] = pose.a[4] * .6;
        const mats = matrices(angles);
        const moved = nodes.map((node, i) => {
            if (!weights[i]) return node;
            let x = 0, y = 0;
            for (let b = 0; b < weights[i].length; b++) {
                if (weights[i][b] <= 0) continue;
                const p = apply(mats[b], node);
                x += p[0] * weights[i][b]; y += p[1] * weights[i][b];
            }
            return [x, y];
        });
        const png = await raster(moved, uv, tris, data, info);
        fs.writeFileSync(path.join(framesDir, pose.name + '.png'), png);
        const raw = await sharp(png).raw().toBuffer({ resolveWithObject: true });
        let bottom = 0, changed = 0, holes = 0;
        for (let i = 0; i < raw.data.length; i += 4) {
            if (raw.data[i + 3] > 40) bottom = Math.max(bottom, Math.floor((i / 4) / size));
            if (raw.data[i] !== restRaw.data[i] || raw.data[i + 1] !== restRaw.data[i + 1]) changed++;
            if (restRaw.data[i + 3] > 120 && raw.data[i + 3] < 40) holes++;
        }
        report.push({ pose: pose.name, bottomY: bottom, changedPixels: changed, holes: holes });
    }
    await sharp({ create: { width: size * poses.length, height: size, channels: 4, background: '#ffffff' } })
        .composite(poses.map((pose, i) => ({ input: path.join(framesDir, pose.name + '.png'), left: i * size, top: 0 })))
        .png().toFile(path.join(outDir, 'contact-sheet.png'));

    // 循环预览：只有 idle 参与，test-strong 不进 GIF。
    const order = ['idle-a', 'idle-b', 'idle-c', 'idle-d', 'idle-e', 'idle-d', 'idle-c', 'idle-b'];
    const stacked = Buffer.concat(await Promise.all(order.map(name =>
        sharp(path.join(framesDir, name + '.png')).ensureAlpha().raw().toBuffer())));
    await sharp(stacked, { raw: { width: size, height: size * order.length, channels: 4, pageHeight: size } })
        .gif({ loop: 0, delay: 110, effort: 7 }).toFile(path.join(outDir, 'idle-loop.gif'));

    // 对照板：上排原图 + 5 个 idle，下排头尾 2× 放大（rest 与 idle-c）。
    const zoom = { left: 20, top: 0, width: 232, height: 240 };
    await sharp(path.join(outDir, 'rest.png')).extract(zoom).resize(464, 480, { kernel: 'lanczos3' })
        .toFile(path.join(outDir, 'zoom-rest.png'));
    await sharp(path.join(framesDir, 'idle-c.png')).extract(zoom).resize(464, 480, { kernel: 'lanczos3' })
        .toFile(path.join(outDir, 'zoom-idle-c.png'));
    await sharp({ create: { width: size * 6, height: size * 2, channels: 4, background: '#ffffff' } })
        .composite([
            { input: path.join(outDir, 'rest.png'), left: 0, top: 0 },
            ...order.slice(0, 5).map((name, i) => ({ input: path.join(framesDir, name + '.png'), left: (i + 1) * size, top: 0 })),
            { input: path.join(outDir, 'zoom-rest.png'), left: 0, top: size },
            { input: path.join(outDir, 'zoom-idle-c.png'), left: 464, top: size }
        ]).png().toFile(path.join(outDir, 'board.png'));

    // 极限对照：静止 vs 夸张姿态（头颈 11°、尾 18°），看撕裂从哪开始。
    await sharp(path.join(framesDir, 'test-strong.png')).extract(zoom).resize(464, 480, { kernel: 'lanczos3' })
        .toFile(path.join(outDir, 'zoom-test-strong.png'));
    await sharp({ create: { width: 948, height: 480, channels: 4, background: '#ffffff' } })
        .composite([
            { input: path.join(outDir, 'zoom-rest.png'), left: 0, top: 0 },
            { input: path.join(outDir, 'zoom-test-strong.png'), left: 484, top: 0 }
        ]).png().toFile(path.join(outDir, 'compare-strong.png'));
    fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify({
        source: 'T_Station_ShibaMotion.png frame 0', crop, size, grid: step, nodes: nodes.length,
        triangles: tris.length, bones: bones.length, poses: report,
        limits: ['single frame, no walk/transition', 'offscreen raster only', 'not imported into Unity']
    }, null, 2));
    console.log(JSON.stringify(report));
}

main().catch(error => { console.error(error.stack || error.message); process.exit(1); });
