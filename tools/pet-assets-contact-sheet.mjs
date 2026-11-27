// 把素材目录里的图拼成 contact sheet，便于快速挑选；同时输出尺寸/水印位置摘要。
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const base = 'C:/Users/Admin/Documents/xwechat_files/wxid_60unbizb1hdy22_1938/msg/file/2026-11/游戏萌萌美术素材/游戏萌萌美术素材/宠物  场景';
const outDir = path.resolve(import.meta.dirname, '..', 'art-source/pet-assets-wx-20261127');
fs.mkdirSync(outDir, { recursive: true });

const folders = fs.readdirSync(base, { withFileTypes: true }).filter(d => d.isDirectory()).map(d => d.name);
const thumb = 150, cols = 10;
const items = [];
for (const folder of folders) {
    const dir = path.join(base, folder);
    for (const file of fs.readdirSync(dir).filter(f => f.toLowerCase().endsWith('.png')).sort()) {
        items.push({ folder, file, full: path.join(dir, file) });
    }
}
const rows = Math.ceil(items.length / cols);
const tiles = [];
for (let i = 0; i < items.length; i++) {
    const buf = await sharp(items[i].full).resize(thumb, thumb, { fit: 'contain', background: '#ffffff' }).toBuffer();
    tiles.push({ input: buf, left: (i % cols) * thumb, top: Math.floor(i / cols) * (thumb + 18) });
}
const labels = items.map((item, i) => `<text x="${(i % cols) * thumb + 4}" y="${Math.floor(i / cols) * (thumb + 18) + thumb + 13}" font-family="Microsoft YaHei" font-size="12" fill="#333">${item.folder} ${i}</text>`).join('');
await sharp({ create: { width: cols * thumb, height: rows * (thumb + 18), channels: 4, background: '#ffffff' } })
    .composite([...tiles, { input: Buffer.from(`<svg width="${cols * thumb}" height="${rows * (thumb + 18)}">${labels}</svg>`), left: 0, top: 0 }])
    .jpeg({ quality: 78 }).toFile(path.join(outDir, 'contact-sheet.jpg'));

const summary = [];
for (const item of items) {
    const meta = await sharp(item.full).metadata();
    summary.push({ folder: item.folder, file: item.file, width: meta.width, height: meta.height, bytes: fs.statSync(item.full).size });
}
fs.writeFileSync(path.join(outDir, 'index.json'), JSON.stringify(summary, null, 2));
console.log(JSON.stringify({ count: items.length, folders: folders.length, out: outDir }));
