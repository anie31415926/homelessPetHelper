import {CanvasTexture, SRGBColorSpace} from 'three';
import {coatTexture, hindBounds, ramp} from './join.js';

const strokes = [
    {layer: 'torso', from: 469, to: 526,
        points: [[469, 287], [486, 287], [495, 286.5], [505, 285], [513, 282.5], [521, 279], [526, 276]],
        donor: -10, width: 6},
    {layer: 'hindLower', from: 568, to: 609,
        points: [[568, 324], [576, 320.5], [584, 315.5], [593, 315.5], [601, 314.5], [609, 311]],
        donor: -10, width: 6}
];

export const deinkAmount = progress => ramp(.32, .64, progress);

function center(points, y) {
    const i = points.findIndex(([row]) => row >= y);
    if (i <= 0) return points[i === 0 ? 0 : points.length - 1][1];
    const [a, x] = points[i - 1], [b, z] = points[i];
    return x + (z - x) * (y - a) / (b - a);
}

function makeCleanAtlas(image, manifest) {
    const {width, height} = manifest;
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(image, 0, 0);
    const pixels = ctx.getImageData(0, 0, width, height), original = pixels.data.slice();
    for (const stroke of strokes) {
        const layer = manifest.layers.find(item => item.id === stroke.layer);
        for (let y = stroke.from; y < Math.min(stroke.to, layer.atlasY + layer.height); y++) {
            const xMid = center(stroke.points, y);
            for (let x = Math.round(xMid - stroke.width); x <= xMid + stroke.width; x++) {
                const strength = ramp(stroke.from, stroke.from + 5, y) * (1 - ramp(stroke.to - 6, stroke.to, y))
                    * (1 - ramp(3, stroke.width, Math.abs(x - xMid)));
                const i = (y * width + x) * 4, src = i + stroke.donor * 4;
                if (!strength || !original[i + 3] || original[src + 3] < 150) continue;
                for (let c = 0; c < 3; c++) pixels.data[i + c] += strength * (original[src + c] - original[i + c]);
            }
        }
    }
    ctx.putImageData(pixels, 0, 0);
    const map = new CanvasTexture(canvas);
    map.colorSpace = SRGBColorSpace;
    return map;
}

function bindCleanMaterial(mesh, map, blend, [left, top, right, bottom]) {
    const original = mesh.material;
    const clean = original.clone();
    clean.customProgramCacheKey = () => `rim:${left}:${top}:${right}:${bottom}`;
    clean.onBeforeCompile = shader => {
        shader.uniforms.deinkMap = {value: map};
        shader.uniforms.deinkBlend = blend;
        shader.fragmentShader = shader.fragmentShader.replace('#include <common>',
            'uniform sampler2D deinkMap; uniform float deinkBlend;\n#include <common>');
        shader.fragmentShader = shader.fragmentShader.replace('#include <map_fragment>', `#include <map_fragment>
            if (vMapUv.x >= ${left} && vMapUv.x <= ${right} && 1.0 - vMapUv.y >= ${top} && 1.0 - vMapUv.y <= ${bottom}) {
                vec3 adjustment = texture2D(deinkMap, vMapUv).rgb - texture2D(map, vMapUv).rgb;
                diffuseColor.rgb += adjustment * deinkBlend;
            }
        `);
    };
    return enabled => {mesh.material = enabled ? clean : original;};
}

export function attachDeink(skin, nearLeg, texture, manifest) {
    const clean = makeCleanAtlas(texture.image, manifest);
    const amount = {value: 0};
    const [left, top, width, height] = hindBounds;
    const lower = manifest.layers.find(layer => layer.id === 'hindLower');
    const offsetX = lower.left - left - lower.atlasX, offsetY = lower.top - top - lower.atlasY;
    const apply = [
        bindCleanMaterial(skin.meshes.find(mesh => mesh.name === 'torso'), clean, amount,
            [268 / manifest.width, 467 / manifest.height, 295 / manifest.width, 528 / manifest.height]),
        bindCleanMaterial(nearLeg, coatTexture(clean.image, manifest), amount,
            [(303 + offsetX) / width, (566 + offsetY) / height, (332 + offsetX) / width, (611 + offsetY) / height])
    ];
    return (enabled, progress) => {
        amount.value = enabled ? deinkAmount(progress) : 0;
        for (const change of apply) change(amount.value > 0);
    };
}
