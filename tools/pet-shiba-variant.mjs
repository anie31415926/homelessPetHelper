// 柴犬换色变体（候选）：只改色相情绪，不改轮廓、像素位置、帧序或 alpha。
// 用 sharp 对整张 8×5 姿态图集逐像素重映射暖色区域；白色胸毛/口鼻保持原值。
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import sharp from 'sharp';

const root = path.resolve(import.meta.dirname, '..');
const source = path.join(root, 'unity/HomelessPetHelper/Assets/Art/PetSit/T_Station_ShibaMotion.png');
const outDir = path.join(root, 'art-source/pet-shiba-variants-20261127/black-shiba');
const outAtlas = path.join(outDir, 'atlas.png');
const outStanding = path.join(outDir, 'standing.png');
const outRecord = path.join(outDir, 'derivation.json');
const params = { hueMin: 8, hueMax: 55, saturationMin: 0.18, base: 0.30, lit: 0.35, r: 0.98, g: 0.93, b: 0.88 };
const check = process.argv.includes('--check');

const sha = buffer => createHash('sha256').update(buffer).digest('hex');

function recolor(data) {
    const out = Buffer.from(data);
    for (let i = 0; i < out.length; i += 4) {
        const r = data[i] / 255, g = data[i + 1] / 255, b = data[i + 2] / 255;
        const max = Math.max(r, g, b), min = Math.min(r, g, b);
        const saturation = max === 0 ? 0 : (max - min) / max;
        const d = max - min;
        let hue = 0;
        if (d > 0) {
            if (max === r) hue = 60 * (((g - b) / d) % 6);
            else if (max === g) hue = 60 * (((b - r) / d) + 2);
            else hue = 60 * (((r - g) / d) + 4);
        }
        if (hue < 0) hue += 360;
        if (hue < params.hueMin || hue > params.hueMax || saturation <= params.saturationMin) continue;
        const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        const value = params.base + params.lit * luminance;
        out[i] = Math.min(255, Math.round(value * 255 * params.r));
        out[i + 1] = Math.min(255, Math.round(value * 255 * params.g));
        out[i + 2] = Math.min(255, Math.round(value * 255 * params.b));
    }
    return out;
}

async function main() {
    const sourceBuffer = fs.readFileSync(source);
    const { data, info } = await sharp(sourceBuffer).ensureAlpha().raw()
        .toBuffer({ resolveWithObject: true });
    const recolored = recolor(data);
    const raw = { raw: { width: info.width, height: info.height, channels: 4 } };
    const atlas = await sharp(recolored, raw).png({ compressionLevel: 9 }).toBuffer();
    const frame = 128;
    const standing = await sharp(recolored, raw)
        .extract({ left: 0, top: 0, width: frame, height: frame })
        .png({ compressionLevel: 9 }).toBuffer();
    fs.mkdirSync(outDir, { recursive: true });
    fs.writeFileSync(outAtlas, atlas);
    fs.writeFileSync(outStanding, standing);
    if (check) {
        const previous = JSON.parse(fs.readFileSync(outRecord, 'utf8'));
        const mismatch = previous.atlas?.sha256 !== sha(atlas) || previous.standing?.sha256 !== sha(standing);
        if (mismatch) throw new Error('Colour variant differs from the recorded derivation; rerun without --check.');
        console.log('check ok', previous.atlas.bytes, previous.standing.bytes);
        return;
    }
    fs.writeFileSync(outRecord, `${JSON.stringify({
        schemaVersion: 1,
        variant: 'dog.shiba.dark',
        source: {
            path: path.relative(root, source).replaceAll('\\', '/'),
            width: info.width, height: info.height,
            sheet: { columns: 8, rows: 5, frame: frame },
            sha256: sha(sourceBuffer)
        },
        transform: { space: 'HSV per pixel; warm pixels only; alpha untouched', ...params },
        atlas: { path: path.relative(root, outAtlas).replaceAll('\\', '/'), bytes: atlas.length, sha256: sha(atlas) },
        standing: { path: path.relative(root, outStanding).replaceAll('\\', '/'), bytes: standing.length, sha256: sha(standing) },
        notes: 'Candidate skin only. Silhouette, frame grid and alpha are byte-identical to the source layout; '
            + 'no new poses, no skeleton, not accepted art and not a third species.'
    }, null, 2)}\n`);
    console.log('wrote', path.relative(root, outAtlas), atlas.length, path.relative(root, outStanding), standing.length);
}

main().catch(error => { console.error(error.message); process.exit(1); });
