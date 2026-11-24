import {CanvasTexture, SRGBColorSpace} from 'three';
import {hindBounds, ramp} from './join.js';

const tailRoot = [[343, 390], [350, 381], [361, 371], [374, 370], [390, 369],
    [405, 364], [415, 358], [428, 350], [440, 348]];
const thighInside = [[406, 252], [420, 259], [434, 269], [446, 282],
    [453, 286], [461, 282], [467, 270], [477, 264], [492, 263]];

function atRow(points, y) {
    const i = points.findIndex(([row]) => row >= y);
    if (i <= 0) return points[i === 0 ? 0 : points.length - 1][1];
    const [a, x] = points[i - 1], [b, z] = points[i];
    return x + (z - x) * (y - a) / (b - a);
}

export const tailBoundary = y => atRow(tailRoot, y);

function canvasOf(width, height) {
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    return canvas.getContext('2d', {willReadFrequently: true});
}

function getPixels(image) {
    const ctx = canvasOf(image.width, image.height);
    ctx.drawImage(image, 0, 0);
    return ctx.getImageData(0, 0, image.width, image.height);
}

function textureOf(ctx) {
    const texture = new CanvasTexture(ctx.canvas);
    texture.colorSpace = SRGBColorSpace;
    return texture;
}

function sample(pixels, x, y, result, i, alpha = 1, donor = x) {
    const from = (y * pixels.width + x) * 4;
    const colour = (y * pixels.width + donor) * 4;
    result[i] = pixels.data[colour];
    result[i + 1] = pixels.data[colour + 1];
    result[i + 2] = pixels.data[colour + 2];
    result[i + 3] = Math.round(pixels.data[from + 3] * alpha);
}

function repartitionAtlas(atlas, manifest, source) {
    const ctx = canvasOf(manifest.width, manifest.height);
    ctx.drawImage(atlas.image, 0, 0);
    const image = ctx.getImageData(0, 0, manifest.width, manifest.height);
    repartitionPixels(image.data, manifest, source);
    ctx.putImageData(image, 0, 0);
    return textureOf(ctx);
}

export function repartitionPixels(data, manifest, source) {
    for (const id of ['torso', 'tail']) {
        const layer = manifest.layers.find(part => part.id === id);
        for (let y = 0; y < layer.height; y++) for (let x = 0; x < layer.width; x++) {
            const sy = layer.top + y, sx = layer.left + x;
            const i = ((layer.atlasY + y) * manifest.width + layer.atlasX + x) * 4;
            if (sy < 344) continue;
            const cut = tailBoundary(sy);
            const rootBlend = ramp(344, 362, sy);
            if (id === 'torso') {
                // Keep the original rump stroke, but not the tail-side colour beyond it.
                data[i + 3] = Math.round(data[i + 3] * (1 - rootBlend * ramp(cut + 1, cut + 3, sx)));
            } else {
                const root = sx - cut;
                const edge = ramp(-16, -8, root);
                const donor = root < 5 && sy < 432 ? Math.max(sx, Math.round(cut + 5)) : sx;
                const old = Array.from(data.subarray(i, i + 4));
                sample(source, sx, sy, data, i, edge, donor);
                for (let c = 0; c < 4; c++) data[i + c] = Math.round(old[c] + (data[i + c] - old[c]) * rootBlend);
            }
        }
    }
}

function continuousLeg(source) {
    const [left, top, width, height] = hindBounds, ctx = canvasOf(width, height);
    const data = ctx.createImageData(width, height);
    continuousLegPixels(source, data.data);
    ctx.putImageData(data, 0, 0);
    return textureOf(ctx);
}

export function continuousLegPixels(source, data) {
    const [left, top, width, height] = hindBounds;
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const sy = top + y, sx = left + x;
        // Trace the near leg. The far paw stays on its existing layer underneath.
        const inside = ramp(atRow(thighInside, sy) - .5, atRow(thighInside, sy) + 1.4, sx);
        const outside = 1 - ramp(349, 354, sx);
        sample(source, sx, sy, data, (y * width + x) * 4, inside * outside * ramp(408, 426, sy));
    }
}

export function attachRepartition(skin, nearLeg, atlas, manifest, sourceImage) {
    const source = getPixels(sourceImage);
    const map = repartitionAtlas(atlas, manifest, source);
    const targets = ['torso', 'tail'].map(name => skin.meshes.find(mesh => mesh.name === name));
    const variants = [...targets.map(mesh => materialWithMap(mesh, map)), materialWithMap(nearLeg, continuousLeg(source))];
    return enabled => {
        if (enabled) for (const apply of variants) apply();
    };
}

function materialWithMap(mesh, map) {
    const material = mesh.material.clone();
    material.map = map;
    return () => {mesh.material = material;};
}
