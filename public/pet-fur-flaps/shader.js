import { axis } from './profile.js';
import { buildEdgeTexture, edgeRows, edgeWidth } from './fur-edge.js';

const vertexSource = `
    uniform vec4 uCrop;
    attribute vec2 aPosition;
    attribute vec2 aLocal;
    attribute vec4 aBasis;
    attribute vec4 aPatch;
    attribute vec2 aBend;
    attribute vec3 aStyle;
    attribute vec2 aWarmthRange;
    varying vec2 vLocal;
    varying vec4 vBasis;
    varying vec4 vPatch;
    varying vec2 vBend;
    varying vec3 vStyle;
    varying vec2 vWarmthRange;
    void main() {
        vLocal = aLocal;
        vBasis = aBasis;
        vPatch = aPatch;
        vBend = aBend;
        vStyle = aStyle;
        vWarmthRange = aWarmthRange;
        vec2 ndc = (aPosition - uCrop.xy) / uCrop.zw * vec2(2.0, -2.0) + vec2(-1.0, 1.0);
        gl_Position = vec4(ndc, 0.0, 1.0);
    }
`;

const fragmentSource = `
    precision mediump float;
    uniform sampler2D uPortrait;
    uniform sampler2D uEdge;
    uniform vec2 uImageSize;
    uniform vec2 uShadowDirection;
    uniform float uShadowStrength;
    varying vec2 vLocal;
    varying vec4 vBasis;
    varying vec4 vPatch;
    varying vec2 vBend;
    varying vec3 vStyle;
    varying vec2 vWarmthRange;

    vec4 fur(vec2 local) {
        return texture2D(uPortrait, (vPatch.xy + vBasis.xy * (local.x - .5) * vPatch.z +
                         vBasis.zw * local.y * vPatch.w) / uImageSize);
    }

    float coatCoverage(vec4 sampleColor) {
        float warmth = dot(sampleColor.rgb, vec3(1.0, .6, -1.6));
        return sampleColor.a * smoothstep(vWarmthRange.x - .035, vWarmthRange.x, warmth) *
            (1.0 - smoothstep(vWarmthRange.y, vWarmthRange.y + .035, warmth));
    }

    vec2 edgeInk(float s, float t) {
        float span = mix(.46, 1.0, clamp((vStyle.x - 2.0) / 14.0, 0.0, 1.0));
        float phase = fract(vStyle.z * ${edgeRows}.0);
        float row = (min(${edgeRows - 1}.0, floor(vStyle.z * ${edgeRows}.0)) + .5) / ${edgeRows}.0;
        float tail = 1.0 - clamp(t, 0.0, 1.0);
        float sweep = .32 * tail * tail;
        float u = clamp((s + sweep) * span + (1.0 - span) * phase, 0.0, 1.0);
        return texture2D(uEdge, vec2(u, row)).ra;
    }

    float tipHeight(float s, float ink) {
        float height = .69 + (ink - .44) * vStyle.y * 1.6;
        return mix(.18, height, smoothstep(0.0, .24, s) * (1.0 - smoothstep(.77, 1.0, s)));
    }

    float sideFade(float s) {
        return smoothstep(0.0, .19, s) * (1.0 - smoothstep(.81, 1.0, s));
    }

    float foldedTip(float s, float edge, float bend) {
        return edge * (1.0 - bend * (.61 + .51 * sin(s * 3.14159)));
    }

    float shadowAmount(vec2 local, float lip, float bend) {
        // Project away from the light in portrait space, then convert to the tuft's axes.
        vec2 away = vec2(dot(uShadowDirection, vBasis.xy), dot(uShadowDirection, vBasis.zw));
        float height = vBend.x * (.20 + .08 * vBend.y) * vPatch.w;
        vec2 shift = away * height / vPatch.zw;
        float s = clamp(local.x - shift.x * smoothstep(.06, .58, local.y), 0.0, 1.0);
        float tip = foldedTip(s, tipHeight(s, edgeInk(s, local.y - shift.y).x), bend) + shift.y;
        float softness = (2.0 + .23 * height) / vPatch.w;
        float projected = 1.0 - smoothstep(tip - softness, tip + softness, local.y);
        float contact = 1.0 - smoothstep(0.0, 4.5 / vPatch.w, local.y - lip);
        return (.07 * contact * sideFade(local.x) + .23 * projected * sideFade(s)) *
            smoothstep(0.0, .58, vBend.x) * smoothstep(0.0, .30, local.y);
    }

    void main() {
        float s = vLocal.x;
        float t = vLocal.y;
        float lift = vBend.x;
        float curl = vBend.y;
        vec2 ink = edgeInk(s, t);
        float edge = tipHeight(s, ink.x);
        float aa = 1.05 / vPatch.w;
        float bend = lift * (.38 + .23 * curl);
        float lip = foldedTip(s, edge, bend);
        float mask = sideFade(s) * smoothstep(0.0, .24, t) * smoothstep(0.0, .21, lift);
        float cover = 1.0 - smoothstep(lip - aa, lip + aa, t);
        float shade = shadowAmount(vLocal, lip, bend) * uShadowStrength;
        if (mask * cover + shade < .002) discard;

        // Keep color and grain registered to the portrait; only the outline and light bend.
        vec4 localFur = fur(vLocal);
        float coverage = coatCoverage(localFur);
        if (coverage < .002) discard;
        float projected = clamp(t / lip, 0.0, 1.0);
        float roll = smoothstep(.28, .65, projected) * (1.0 - smoothstep(.69, .97, projected));
        float tuck = smoothstep(.73, .98, projected) * curl;
        float facingLight = .5 - .5 * dot(uShadowDirection, vBasis.zw);
        float highlight = ((.018 + .040 * curl) * roll + .025 * smoothstep(.91, 1.0, projected)) * facingLight;
        float light = 1.0 + lift * (highlight - .08 * tuck * (1.0 - facingLight));
        vec3 surface = localFur.rgb * light;
        float stroke = ink.y * smoothstep(.60, .94, projected) * lift;
        surface *= 1.0 - stroke * vec3(.11, .15, .18);
        float a = mask * cover * coverage;
        float edgeFade = min(sideFade(s), 1.0 - smoothstep(.93, 1.0, t));
        float b = shade * (1.0 - cover) * edgeFade * coverage * (1.0 - a);
        gl_FragColor = vec4(surface * a + localFur.rgb * vec3(.09, .065, .07) * b, a + b);
    }
`;

function compile(gl, type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(shader));
    return shader;
}

function createProgram(gl) {
    const program = gl.createProgram();
    const vertex = compile(gl, gl.VERTEX_SHADER, vertexSource);
    const fragment = compile(gl, gl.FRAGMENT_SHADER, fragmentSource);
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
    return program;
}

const stride = 19;
const quad = [[0, 0], [1, 0], [0, 1], [0, 1], [1, 0], [1, 1]];

function fillQuad(vertices, start, patch) {
    const { x, y, gx, gy, width, length, lift, curl, teeth, toothDepth, seed, warmthRange } = patch;
    for (const [s, t] of quad) {
        const across = (s - .5) * width;
        const along = t * length;
        vertices[start++] = x + gy * across + gx * along;
        vertices[start++] = y - gx * across + gy * along;
        vertices[start++] = s;
        vertices[start++] = t;
        vertices[start++] = gy;
        vertices[start++] = -gx;
        vertices[start++] = gx;
        vertices[start++] = gy;
        vertices[start++] = x;
        vertices[start++] = y;
        vertices[start++] = width;
        vertices[start++] = length;
        vertices[start++] = lift;
        vertices[start++] = curl;
        vertices[start++] = teeth;
        vertices[start++] = toothDepth;
        vertices[start++] = seed;
        vertices[start++] = warmthRange?.[0] ?? -1.6;
        vertices[start++] = warmthRange?.[1] ?? 1.6;
    }
    return start;
}

export function makeRenderer(canvas, portrait, crop, patchCount) {
    const gl = canvas.getContext('webgl', { alpha: true, antialias: false, depth: false, stencil: false });
    if (!gl) return null;
    const program = createProgram(gl);
    const buffer = gl.createBuffer();
    const vertices = new Float32Array(patchCount * quad.length * stride);
    const batchViews = [];
    gl.useProgram(program);
    gl.uniform4f(gl.getUniformLocation(program, 'uCrop'), crop.x, crop.y, crop.width, crop.height);
    gl.uniform2f(gl.getUniformLocation(program, 'uImageSize'), portrait.width, portrait.height);
    const direction = gl.getUniformLocation(program, 'uShadowDirection');
    const strength = gl.getUniformLocation(program, 'uShadowStrength');
    bindTexture(gl, program, 'uPortrait', 0);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, portrait);
    bindTexture(gl, program, 'uEdge', 1);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.LUMINANCE_ALPHA, edgeWidth, edgeRows, 0, gl.LUMINANCE_ALPHA, gl.UNSIGNED_BYTE, buildEdgeTexture());
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, vertices.byteLength, gl.DYNAMIC_DRAW);
    let offset = 0;
    for (const [name, count] of [['aPosition', 2], ['aLocal', 2], ['aBasis', 4], ['aPatch', 4], ['aBend', 2], ['aStyle', 3], ['aWarmthRange', 2]]) {
        const loc = gl.getAttribLocation(program, name);
        gl.enableVertexAttribArray(loc);
        gl.vertexAttribPointer(loc, count, gl.FLOAT, false, stride * 4, offset * 4);
        offset += count;
    }
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
    return {
        setLight(light) {
            gl.uniform2fv(direction, axis(light.angle));
            gl.uniform1f(strength, light.shadow);
        },
        draw(patches) {
            gl.viewport(0, 0, canvas.width, canvas.height);
            gl.clear(gl.COLOR_BUFFER_BIT);
            let count = 0;
            for (const patch of patches) if (patch.lift > .008) count = fillQuad(vertices, count, patch);
            if (!count) return;
            const vertexCount = count / stride;
            const active = vertexCount / quad.length;
            const view = batchViews[active] ??= vertices.subarray(0, count);
            gl.bufferSubData(gl.ARRAY_BUFFER, 0, view);
            gl.drawArrays(gl.TRIANGLES, 0, vertexCount);
        },
    };
}

function bindTexture(gl, program, sampler, unit) {
    gl.activeTexture(gl.TEXTURE0 + unit);
    gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.uniform1i(gl.getUniformLocation(program, sampler), unit);
}
