// 批量去角标水印：“豆包 AI 生成”（右下角灰字）。按像素掩码识别 → 从上方向下采样纸底颜色填补 → 羽化 → 输出 1024。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const base = 'C:/Users/Admin/Documents/xwechat_files/wxid_60unbizb1hdy22_1938/msg/file/2026-11/游戏萌萌美术素材/游戏萌萌美术素材/宠物  场景';
const outRoot = path.resolve(import.meta.dirname, '..', 'art-source/pet-assets-wx-20261127');
const cleanRoot = path.join(outRoot, 'clean');
const maxSide = 1024;
const report = [];

for (const folder of fs.readdirSync(base, { withFileTypes: true }).filter(d => d.isDirectory()).map(d => d.name)) {
    const dir = path.join(base, folder);
    fs.mkdirSync(path.join(cleanRoot, folder), { recursive: true });
    for (const file of fs.readdirSync(dir).filter(f => f.toLowerCase().endsWith('.png')).sort()) {
        const full = path.join(dir, file);
        const { data, info } = await sharp(full).removeAlpha().raw().toBuffer({ resolveWithObject: true });
        const w = info.width, h = info.height;

        const bandTop = Math.floor(h * .9), bandLeft = Math.floor(w * .8);
        const mask = new Uint8Array(w * h);
        for (let y = bandTop; y < h; y++) {
            for (let x = bandLeft; x < w; x++) {
                const i = (y * w + x) * 3, r = data[i], g = data[i + 1], b = data[i + 2];
                const mean = (r + g + b) / 3, spread = Math.max(r, g, b) - Math.min(r, g, b);
                if (spread < 14 && mean > 175 && mean < 243) mask[y * w + x] = 1;
            }
        }
        // 膨胀 3px，覆盖笔画抗锯齿边缘。
        const dilated = new Uint8Array(mask);
        for (let y = bandTop; y < h; y++) {
            for (let x = bandLeft; x < w; x++) {
                if (mask[y * w + x]) continue;
                let hit = false;
                for (let dy = -3; dy <= 3 && !hit; dy++) {
                    for (let dx = -3; dx <= 3; dx++) {
                        const nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && mask[ny * w + nx]) { hit = true; break; }
                    }
                }
                if (hit) dilated[y * w + x] = 1;
            }
        }

        let minX = w, minY = h, maxX = -1, maxY = -1, count = 0, saturated = 0;
        for (let y = bandTop; y < h; y++) {
            for (let x = bandLeft; x < w; x++) {
                if (!dilated[y * w + x]) continue;
                count++;
                minX = Math.min(minX, x); maxX = Math.max(maxX, x);
                minY = Math.min(minY, y); maxY = Math.max(maxY, y);
                const i = (y * w + x) * 3, r = data[i], g = data[i + 1], b = data[i + 2];
                if ((Math.max(r, g, b) - Math.min(r, g, b)) > 30) saturated++;
            }
        }

        const patch = Buffer.from(data);
        if (count > 0) {
            // 逐行填：取同一行左侧的干净纸面颜色（中位数）填满水印框，再按距离羽化，避免克隆出阴影/色块。
            const pad = 8, feather = 8;
            const x0 = Math.max(0, minX - pad), x1 = Math.min(w, maxX + pad + 1);
            const y0 = Math.max(0, minY - pad), y1 = Math.min(h, maxY + pad + 1);
            let globalPaper = [0, 0, 0];
            {
                const samples = [];
                for (let y = 0; y < Math.min(40, h); y++) for (let x = 0; x < Math.min(40, w); x++) {
                    const i = (y * w + x) * 3;
                    samples.push([data[i], data[i + 1], data[i + 2]]);
                }
                for (let c = 0; c < 3; c++) {
                    const v = samples.map(s => s[c]).sort((a, b) => a - b);
                    globalPaper[c] = v[Math.floor(v.length / 2)];
                }
            }
            const rowColor = new Map();
            for (let y = y0; y < y1; y++) {
                const vals = [[], [], []];
                for (let x = Math.max(0, x0 - 200); x < x0; x++) {
                    const i = (y * w + x) * 3, r = data[i], g = data[i + 1], b = data[i + 2];
                    if (Math.max(r, g, b) - Math.min(r, g, b) < 12 && (r + g + b) / 3 > 228) { vals[0].push(r); vals[1].push(g); vals[2].push(b); }
                }
                const mid = c => {
                    if (vals[c].length < 10) return null;
                    const v = vals[c].sort((a, b) => a - b);
                    return v[Math.floor(v.length / 2)];
                };
                const mc = [mid(0), mid(1), mid(2)];
                rowColor.set(y, mc[0] === null ? globalPaper : mc);
            }
            for (let y = y0; y < y1; y++) {
                const col = rowColor.get(y);
                for (let x = x0; x < x1; x++) {
                    const edge = Math.min(x - x0, x1 - 1 - x, y - y0, y1 - 1 - y);
                    const t = Math.min(1, edge / feather);
                    const a = t * t * (3 - 2 * t);
                    const di = (y * w + x) * 3;
                    for (let c = 0; c < 3; c++) patch[di + c] = Math.round(col[c] * a + data[di + c] * (1 - a));
                }
            }
        }

        const outPath = path.join(cleanRoot, folder, file.replace(/\.png$/i, '') + '-clean.png');
        await sharp(patch, { raw: { width: w, height: h, channels: 3 } })
            .resize({ width: maxSide, height: maxSide, fit: 'inside', withoutEnlargement: false })
            .png({ compressionLevel: 9 }).toFile(outPath);

        // 比对裁片（原图 vs 清理后），统一按原始坐标裁右下角。
        const crop = { left: Math.max(0, w - 520), top: Math.max(0, h - 240), width: Math.min(w, 520), height: Math.min(h, 240) };
        const before = await sharp(full).extract(crop).resize(520, 240, { fit: 'fill' }).flatten({ background: '#fff' }).png().toBuffer();
        const after = await sharp(patch, { raw: { width: w, height: h, channels: 3 } }).extract(crop)
            .resize(520, 240, { fit: 'fill' }).flatten({ background: '#fff' }).png().toBuffer();
        const compareDir = path.join(outRoot, 'watermark-compare');
        fs.mkdirSync(compareDir, { recursive: true });
        fs.writeFileSync(path.join(compareDir, folder + '-' + file.replace(/[^\d]/g, '') + '-before.png'), before);
        fs.writeFileSync(path.join(compareDir, folder + '-' + file.replace(/[^\d]/g, '') + '-after.png'), after);

        report.push({ folder, file, size: [w, h], watermarkPixels: count, saturatedInMask: saturated, bbox: count ? [minX, minY, maxX - minX, maxY - minY] : null, out: outPath.replace(/\\/g, '/') });
    }
}
fs.writeFileSync(path.join(outRoot, 'clean-index.json'), JSON.stringify(report, null, 2));
const risky = report.filter(r => r.watermarkPixels === 0 || r.saturatedInMask > r.watermarkPixels * .15);
console.log(JSON.stringify({ count: report.length, needReview: risky.map(r => r.folder + '/' + r.file + ' sat=' + r.saturatedInMask + '/' + r.watermarkPixels) }, null, 1));
