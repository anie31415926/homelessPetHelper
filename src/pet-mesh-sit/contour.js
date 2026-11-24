import {Matrix4, Vector3} from 'three';
import {sourcePoint} from '../pet-sit-chibi/rig.js';
import {ramp} from './join.js';
import {rumpBand, sampleRump} from './rump.js';

const anchors = [
    {bone: 'nearThighArt', at: [354, 365], tangent: [351, 371]},
    {bone: 'bodyPelvis', at: [373, 391], tangent: [379, 385]}
];

function skinAnchor(skeleton, {bone, at, tangent}) {
    const index = skeleton.bones.findIndex(b => b.name === bone);
    if (index < 0) throw Error('Unknown contour anchor: ' + bone);
    const transform = new Matrix4();
    return (progress = 0) => {
        transform.multiplyMatrices(skeleton.bones[index].matrixWorld, skeleton.boneInverses[index]);
        const [dx, dy] = sampleRump(progress);
        return [at, tangent].map(([x, y]) => {
            const weight = rumpBand(x, y);
            return sourcePoint(x + dx * weight, y + dy * weight).applyMatrix4(transform);
        });
    };
}

export function contourStrength(progress, distance) {
    return ramp(.66, .91, progress) * ramp(.025, .09, distance) * (1 - ramp(.29, .44, distance));
}

export function createContour(host, skeleton) {
    const layer = document.createElement('canvas');
    layer.className = 'contour';
    layer.setAttribute('aria-hidden', 'true');
    host.append(layer);
    const ctx = layer.getContext('2d');
    const [start, end] = anchors.map(anchor => skinAnchor(skeleton, anchor));
    return (base, camera, progress, corrected) => {
        if (layer.width !== base.width || layer.height !== base.height) {
            layer.width = base.width; layer.height = base.height;
        }
        const w = layer.width, h = layer.height;
        ctx.clearRect(0, 0, w, h);
        if (progress <= .66) return;
        const [a, before] = start(corrected ? progress : 0), [b, after] = end();
        const distance = a.distanceTo(b), opacity = contourStrength(progress, distance);
        if (opacity <= .001) return;
        const c1 = a.clone().addScaledVector(new Vector3().subVectors(a, before).normalize(), distance * .39);
        const c2 = b.clone().addScaledVector(new Vector3().subVectors(b, after).normalize(), distance * .36);
        const points = [a, c1, c2, b].map(v => {
            v.project(camera);
            return [(v.x + 1) * w / 2, (1 - v.y) * h / 2];
        });
        drawStroke(ctx, points, opacity, h / (camera.top - camera.bottom));
        // Never let a supplemental interior line paint transparent background pixels.
        ctx.globalCompositeOperation = 'destination-in';
        ctx.drawImage(base, 0, 0);
        ctx.globalCompositeOperation = 'source-over';
    };
}

function drawStroke(ctx, points, opacity, pxPerUnit) {
    const sample = t => {
        const s = 1 - t, weights = [s ** 3, 3 * s * s * t, 3 * s * t * t, t ** 3];
        return [0, 1].map(axis => points.reduce((n, p, i) => n + p[axis] * weights[i], 0));
    };
    ctx.strokeStyle = '#bc7646';
    ctx.lineCap = 'round';
    const count = 20;
    for (let i = 0; i < count; i++) {
        const taper = Math.sin(Math.PI * (i + .5) / count) ** .65;
        ctx.globalAlpha = .42 * opacity * taper;
        ctx.lineWidth = pxPerUnit * (.006 + .012 * taper);
        ctx.beginPath(); ctx.moveTo(...sample(i / count)); ctx.lineTo(...sample((i + 1) / count)); ctx.stroke();
    }
    ctx.globalAlpha = 1;
}
