import {ramp} from './join.js';

const floor = .006;
const band = .14;
const flat = .065;
const inkBand = .024;
const inkSlope = .68;
const furSlope = .15;
const xLeft = -1.58;
const xRight = -.30;
const feather = .19;
const rootLeft = -1.94;
const rootRight = -.86;
const rootFeather = .28;
const rootDepth = .43;
const rootFlat = .275;
const rootSlope = .04;

export const contactAmount = progress => ramp(.72, .99, progress);

export function contactY(x, y, amount, pawWeight = 0) {
    const coverage = ramp(xLeft, xLeft + feather, x) * (1 - ramp(xRight - feather, xRight, x));
    const flatten = 1 - ramp(flat, band, y);
    const target = floor + Math.min(inkBand, Math.max(0, y)) * inkSlope + Math.max(0, y - inkBand) * furSlope;
    const root = ramp(rootLeft, rootLeft + rootFeather, x)
        * (1 - ramp(rootRight - rootFeather, rootRight, x))
        * (1 - ramp(rootFlat, rootDepth, y));
    const rootTarget = floor + Math.max(0, y) * rootSlope;
    const seat = y + (target - y) * coverage * flatten;
    return y + (seat + (rootTarget - seat) * root - y) * amount * (1 - pawWeight);
}

export function createSeatContact(meshes, nearLeg) {
    const amount = {value: 0};
    const variants = new WeakMap();

    function variant(original, holdEdge) {
        const cached = variants.get(original)?.[Number(holdEdge)];
        if (cached) return cached;
        const material = original.clone();
        material.customProgramCacheKey = () => original.customProgramCacheKey() + ':seat-contact:' + holdEdge;
        material.onBeforeCompile = (shader, renderer) => {
            original.onBeforeCompile.call(material, shader, renderer);
            shader.uniforms.seatContact = amount;
            shader.vertexShader = 'uniform float seatContact;\n'
                + shader.vertexShader.replace('#include <skinning_vertex>', `
                #include <skinning_vertex>
                // In this rig all skin layers share the same coordinates after skinning.
                float seatWidth = smoothstep(${xLeft}, ${xLeft + feather}, transformed.x)
                    * (1.0 - smoothstep(${xRight - feather}, ${xRight}, transformed.x));
                float seatDepth = 1.0 - smoothstep(${flat}, ${band}, transformed.y);
                float seatY = ${floor} + clamp(transformed.y, 0.0, ${inkBand}) * ${inkSlope}
                    + max(0.0, transformed.y - ${inkBand}) * ${furSlope};
                // The rump and tail meshes share this falloff, so the seam moves together.
                float rootWeight = smoothstep(${rootLeft}, ${rootLeft + rootFeather}, transformed.x)
                    * (1.0 - smoothstep(${rootRight - rootFeather}, ${rootRight}, transformed.x))
                    * (1.0 - smoothstep(${rootFlat}, ${rootDepth}, transformed.y));
                float rootY = ${floor} + max(0.0, transformed.y) * ${rootSlope};
                float seat = mix(transformed.y, seatY, seatWidth * seatDepth);
                transformed.y = mix(transformed.y, mix(seat, rootY, rootWeight),
                    seatContact${holdEdge ? ' * (1.0 - skinWeight.y)' : ''});
            `);
        };
        const pair = variants.get(original) || [];
        pair[Number(holdEdge)] = material;
        variants.set(original, pair);
        return material;
    }

    return (enabled, progress) => {
        amount.value = enabled ? contactAmount(progress) : 0;
        // The caller selects each view's unmodified materials before applying this pass.
        for (const mesh of meshes) {
            const holdEdge = mesh === nearLeg;
            if (amount.value > 0) mesh.material = variant(mesh.material, holdEdge);
        }
    };
}
