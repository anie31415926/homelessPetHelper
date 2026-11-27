// 遮挡贴片：从底稿自身取毛皮做克隆，羽化边缘，输出 512 RGBA；附三块同时贴上的预览。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const root = path.resolve(import.meta.dirname, '..');
const input = path.join(root, 'art-source/dog-rig-prototype-20261127/comfy-input/plate-512.png');
const outRoot = path.join(root, 'art-source/dog-rig-prototype-20261127/patches/clone');
const size = 512;

// dest 是补绘位置；src 是同一张图上取毛皮的来源；flip 用于避免一眼看出复制。
const patches = [
    { name: 'occl-neck-back', dest: [189, 152, 63, 93], src: [214, 66, 63, 93], flip: true, feather: 16 },
    { name: 'occl-belly', dest: [199, 291, 124, 106], src: [150, 262, 124, 106], flip: false, feather: 18 },
    { name: 'occl-hip-back', dest: [323, 150, 59, 114], src: [286, 96, 59, 114], flip: true, feather: 16 }
];

const plate = await sharp(input).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
fs.mkdirSync(outRoot, { recursive: true });

for (const p of patches) {
    const [dx, dy, dw, dh] = p.dest;
    const [sx, sy, sw, sh] = p.src;
    let crop = sharp(await sharp(input).extract({ left: sx, top: sy, width: sw, height: sh }).png().toBuffer());
    if (p.flip) crop = sharp(await crop.flop().png().toBuffer());
    const resized = await crop.resize(dw, dh, { kernel: 'lanczos3' }).ensureAlpha().raw().toBuffer({ resolveWithObject: true });

    // 颜色对齐：把克隆块的均值/方差拉到目标区一致，再用椭圆+羽化遮罩，避免出现矩形色块。
    const stats = (buf, w, h) => {
        const sum = [0, 0, 0], sq = [0, 0, 0];
        for (let i = 0; i < w * h; i++) for (let c = 0; c < 3; c++) { const v = buf[i * 4 + c]; sum[c] += v; sq[c] += v * v; }
        return { mean: sum.map(s => s / (w * h)), std: sq.map((s, c) => Math.sqrt(Math.max(1e-3, s / (w * h) - (sum[c] / (w * h)) ** 2))) };
    };
    const srcStats = stats(resized.data, dw, dh);
    const dstPixels = Buffer.alloc(dw * dh * 4);
    for (let y = 0; y < dh; y++) for (let x = 0; x < dw; x++) {
        const di = ((dy + y) * size + dx + x) * 4;
        for (let c = 0; c < 4; c++) dstPixels[(y * dw + x) * 4 + c] = plate.data[di + c];
    }
    const dstStats = stats(dstPixels, dw, dh);

    const f = p.feather;
    const patch = Buffer.alloc(dw * dh * 4);
    for (let y = 0; y < dh; y++) {
        for (let x = 0; x < dw; x++) {
            const i = (y * dw + x) * 4;
            for (let c = 0; c < 3; c++) {
                const v = resized.data[i + c];
                const mapped = (v - srcStats.mean[c]) / srcStats.std[c] * dstStats.std[c] + dstStats.mean[c];
                patch[i + c] = Math.max(0, Math.min(255, Math.round(mapped)));
            }
            // 椭圆遮罩：中心实心，边缘羽化到 0，半径带轻微起伏避免机械感。
            const nx = (x - dw / 2) / (dw / 2), ny = (y - dh / 2) / (dh / 2);
            const r = Math.sqrt(nx * nx + ny * ny) * (1 + 0.06 * Math.sin(x * 0.7 + y * 0.3));
            const t = Math.max(0, Math.min(1, (1 - r) * (dw / 2) / f));
            patch[i + 3] = Math.round(patch[i + 3] * (t * t * (3 - 2 * t)));
        }
    }
    const full = Buffer.alloc(size * size * 4);
    for (let y = 0; y < dh; y++) {
        for (let x = 0; x < dw; x++) {
            const si = (y * dw + x) * 4, di = ((dy + y) * size + dx + x) * 4;
            full[di] = patch[si]; full[di + 1] = patch[si + 1]; full[di + 2] = patch[si + 2]; full[di + 3] = patch[si + 3];
        }
    }
    await sharp(full, { raw: { width: size, height: size, channels: 4 } }).png({ compressionLevel: 9 })
        .toFile(path.join(outRoot, p.name + '.png'));
    await sharp(full, { raw: { width: size, height: size, channels: 4 } }).flatten({ background: '#ffffff' })
        .toFile(path.join(outRoot, p.name + '-preview.png'));
}

// 预览：三块贴片全部叠到底稿上（就是用它们补遮挡的最终样子）。
const plateRaw = await sharp(input).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
for (const p of patches) {
    const buf = await sharp(path.join(outRoot, p.name + '.png')).ensureAlpha().raw().toBuffer();
    for (let i = 0; i < buf.length; i += 4) {
        const a = buf[i + 3] / 255;
        if (a <= 0) continue;
        plateRaw.data[i] = Math.round(buf[i] * a + plateRaw.data[i] * (1 - a));
        plateRaw.data[i + 1] = Math.round(buf[i + 1] * a + plateRaw.data[i + 1] * (1 - a));
        plateRaw.data[i + 2] = Math.round(buf[i + 2] * a + plateRaw.data[i + 2] * (1 - a));
    }
}
await sharp(plateRaw.data, { raw: { width: size, height: size, channels: 4 } }).png()
    .toFile(path.join(outRoot, 'patched-plate-preview.png'));
// RGBA 合成版 + 126 帧版（供蒙皮脚本做“补过遮挡后”的空洞复测）。
const patched = await sharp(plateRaw.data, { raw: { width: size, height: size, channels: 4 } })
    .png({ compressionLevel: 9 }).toBuffer();
await sharp(patched).toFile(path.join(outRoot, 'patched-plate.png'));
await sharp(patched).resize(126, 126, { kernel: 'lanczos3' })
    .toFile(path.join(outRoot, 'patched-frame-126.png'));
console.log(JSON.stringify({ patches: patches.map(p => p.name) }));
