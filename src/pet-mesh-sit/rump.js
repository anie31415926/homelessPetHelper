import {CanvasTexture, SRGBColorSpace} from 'three';
import {ramp} from './join.js';

const keys = [
    [0, 0, 0, 0], [.31, 0, 0, 0], [.55, 3.5, -2.5, .72], [1, 6, -4, 1]
];

export function sampleRump(progress) {
    const i = keys.findIndex(key => key[0] >= progress);
    if (i <= 0) return i === 0 ? keys[0].slice(1) : keys.at(-1).slice(1);
    const a = keys[i - 1], b = keys[i];
    const t = ramp(a[0], b[0], progress);
    return a.slice(1).map((v, j) => v + (b[j + 1] - v) * t);
}

export function rumpBand(x, y) {
    return ramp(319, 349, x) * ramp(345, 362, y) * (1 - ramp(383, 416, y));
}

function coatMap(image, layer) {
    const {width, height, atlasX, atlasY, left, top} = layer;
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    const ctx = canvas.getContext('2d');
    ctx.drawImage(image, atlasX, atlasY, width, height, 0, 0, width, height);
    const pixels = ctx.getImageData(0, 0, width, height);
    const source = pixels.data.slice();
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const w = rumpBand(left + x, top + y);
        if (!w) continue;
        const i = (y * width + x) * 4;
        const donor = (y * width + x - 11) * 4;
        if (!source[i + 3] || source[donor + 3] < 75) continue;
        for (let rgb = 0; rgb < 3; rgb++) pixels.data[i + rgb] += (source[donor + rgb] - source[i + rgb]) * w;
    }
    ctx.putImageData(pixels, 0, 0);
    const map = new CanvasTexture(canvas);
    map.colorSpace = SRGBColorSpace;
    return map;
}

export function attachRumpCorrective(skin, texture, manifest) {
    const mesh = skin.meshes.find(item => item.name === 'nearThigh');
    const layer = manifest.layers.find(item => item.id === 'nearHindThigh');
    const material = mesh.material.clone();
    const colour = {value: 0};
    const map = coatMap(texture.image, layer);
    material.onBeforeCompile = shader => {
        shader.uniforms.rumpColor = colour;
        shader.uniforms.rumpMap = {value: map};
        shader.fragmentShader = shader.fragmentShader.replace('#include <common>',
            'uniform float rumpColor; uniform sampler2D rumpMap;\n#include <common>');
        shader.fragmentShader = shader.fragmentShader.replace('#include <map_fragment>', `#include <map_fragment>
            vec2 rimUv = (vMapUv * vec2(${manifest.width}.0, ${manifest.height}.0)
                - vec2(${layer.atlasX}.0, ${manifest.height - layer.atlasY - layer.height}.0))
                / vec2(${layer.width}.0, ${layer.height}.0);
            diffuseColor.rgb = mix(diffuseColor.rgb, texture2D(rumpMap, rimUv).rgb, rumpColor);
        `);
    };
    const original = mesh.material;
    const p = mesh.geometry.attributes.position;
    const bind = p.array.slice();
    const weights = Array.from({length: p.count}, (_, i) =>
        rumpBand(226 - 100 * p.getX(i), 496 - 100 * p.getY(i)));
    let last = -1;
    return (enabled, progress) => {
        const active = enabled ? progress : 0;
        mesh.material = active > .31 ? material : original;
        if (last === active) return;
        last = active;
        const [x, y, tint] = sampleRump(active);
        colour.value = tint;
        for (let i = 0; i < p.count; i++) {
            p.setXY(i, bind[i * 3] - x * weights[i] / 100, bind[i * 3 + 1] - y * weights[i] / 100);
        }
        p.needsUpdate = true;
    };
}
