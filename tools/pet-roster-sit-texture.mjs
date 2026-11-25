import assert from 'node:assert/strict';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {dirname, join} from 'node:path';
import sharp from 'sharp';

const sourceDir = 'art-source/pet-roster-20261124/sit-texture';
const batch = 'test-results/pet-roster-20261124';
const output = join(batch, 'sit-texture');
const variants = ['tuxedo', 'shiba'];
const selectable = [...variants, 'shiba-selected'];
const names = process.argv.length > 2 ? process.argv.slice(2) : variants;
const still = names.length === 1 && names[0] === 'shiba-seat-target';
const station = names.length === 1 && names[0] === 'shiba-station';
assert.ok(still || station || new Set(names).size === names.length && names.every(name => selectable.includes(name)),
    'Use tuxedo, shiba, shiba-selected, shiba-seat-target or shiba-station');
const load = async file => JSON.parse(await readFile(file, 'utf8'));
const clamp = t => Math.max(0, Math.min(1, t));
const smooth = t => { t = clamp(t); return t * t * (3 - 2 * t); };
const mix = (a, b, t) => a + (b - a) * t;
const plus = ([x, y], [u, v]) => [x + u, y + v];
const minus = ([x, y], [u, v]) => [x - u, y - v];
const lerp = ([x, y], [u, v], t) => [mix(x, u, t), mix(y, v, t)];
const length = ([x, y]) => Math.hypot(x, y);
const radians = deg => deg * Math.PI / 180;
const angle = ([x, y]) => Math.atan2(y, x);
const polar = (d, a) => [d * Math.cos(a), d * Math.sin(a)];
const rotate = ([x, y], a) => [x * Math.cos(a) - y * Math.sin(a), x * Math.sin(a) + y * Math.cos(a)];
const transform = (p, a, b, rotation) => plus(b, rotate(minus(p, a), rotation));
const chainNames = {
    front: ['shoulder', 'elbow', 'carpus', 'frontToe'],
    hind: ['hip', 'stifle', 'hock', 'hindToe']
};

function joint(a, b, upper, lower, branch) {
    const delta = minus(b, a);
    const d = length(delta);
    assert.ok(d > Math.abs(upper - lower) && d < upper + lower, `Unreachable leg: ${d.toFixed(2)} vs ${upper.toFixed(2)} + ${lower.toFixed(2)}`);
    const x = (upper * upper - lower * lower + d * d) / (2 * d);
    return plus(a, rotate([x, branch * Math.sqrt(upper * upper - x * x)], angle(delta)));
}

function segments(points, names) {
    return names.slice(1).map((name, i) => minus(points[name], points[names[i]]));
}

function motion(profile) {
    const {rest, keys} = profile;
    const axis = minus(rest.hip, rest.shoulder);
    const startAngle = angle(axis);
    const chains = Object.fromEntries(Object.entries(chainNames).map(([id, names]) => [id, segments(rest, names)]));
    return t => {
        const i = Math.max(1, keys.findIndex(key => key.at >= t));
        const a = keys[i - 1], b = keys[i];
        const f = smooth((t - a.at) / (b.at - a.at));
        const hip = lerp(a.hip, b.hip, f);
        const value = id => mix(a[id], b[id], f);
        return poseAt(hip, value, t);
    };

    function poseAt(hip, value, t) {
        const turn = radians(value('bodyAngle')) - startAngle;
        const shoulder = minus(hip, rotate(axis, turn));
        const head = plus(shoulder, plus(minus(rest.head, rest.shoulder), [0, profile.headLift * smooth(t)]));
        const pose = {hip, shoulder, head, tailRoot: transform(rest.tailRoot, rest.hip, hip, turn)};
        for (const [id, [root, bend, ankle, toe]] of Object.entries(chainNames)) {
            const [upper, lower, paw] = chains[id].map(length);
            const bindAngle = angle(chains[id][2]);
            const endAngle = id === 'front'
                ? mix(bindAngle, radians(profile.frontEndAngle), smooth(t))
                : bindAngle + radians(value('hockAngle') - keys[0].hockAngle);
            pose[toe] = [value(id + 'X'), rest[toe][1] - value(id + 'Lift')];
            pose[ankle] = minus(pose[toe], polar(paw, endAngle));
            pose[bend] = joint(pose[root], pose[ankle], upper, lower, id === 'front' ? -1 : 1);
        }
        return pose;
    }
}

function verifyMotion({rest, hindGroundY = rest.hindToe[1]}, sample) {
    const initial = sample(0);
    for (const name of Object.keys(initial)) assert.ok(length(minus(initial[name], rest[name])) < .02, `Bind mismatch: ${name}`);
    let previous = initial;
    for (let i = 0; i <= 240; i++) {
        const pose = sample(i / 240);
        for (const names of [...Object.values(chainNames), ['shoulder', 'hip'], ['hip', 'tailRoot']]) {
            segments(pose, names).forEach((segment, j) => {
                assert.ok(Math.abs(length(segment) - length(segments(rest, names)[j])) < 1e-7);
            });
        }
        assert.ok(pose.frontToe[1] <= rest.frontToe[1] && pose.hindToe[1] <= hindGroundY);
        assert.ok(pose.frontToe[1] === rest.frontToe[1] || pose.hindToe[1] >= rest.hindToe[1]);
        for (const name of Object.keys(initial)) assert.ok(length(minus(pose[name], previous[name])) < 6);
        previous = pose;
    }
    assert.ok(previous.stifle[0] < previous.hip[0] && previous.stifle[1] < initial.stifle[1] + (previous.hip[1] - initial.hip[1]), 'Thigh did not fold forward and up relative to the pelvis');
    assert.ok(previous.hock[0] > previous.stifle[0] && previous.hock[1] > previous.stifle[1]);
}

function jointBlend(p, a, b, at, from, to) {
    const [x, y] = minus(b, a);
    const [u, v] = minus(p, at);
    return smooth(((u * x + v * y) / Math.hypot(x, y) - from) / (to - from));
}

function rollRump(roll, point, base, t) {
    if (!roll || t <= roll.start) return base;
    const [x, y] = point;
    const i = roll.edge.findIndex(entry => entry[1] >= y);
    if (i <= 0) return base;
    const a = roll.edge[i - 1], b = roll.edge[i];
    const f = (y - a[1]) / (b[1] - a[1]);
    const inward = mix(a[0], b[0], f) - x;
    const target = [mix(a[2], b[2], f) - inward * .20, mix(a[3], b[3], f) - inward * .90];
    const extent = smooth((y - roll.edge[0][1]) / 41) * smooth((roll.edge.at(-1)[1] - y) / 53);
    const weight = (1 - smooth(inward / roll.blendWidth)) * extent * smooth((t - roll.start) / (1 - roll.start));
    return lerp(base, target, weight);
}

function tailSkin(profile, pose, turn, t) {
    const {rest, rumpRoll: roll} = profile;
    const rotation = turn * profile.tailTurn + (pose.tailSway ?? 0);
    if (roll?.tailTip && t > roll.start) {
        const {source, offset, seatAngle} = roll.tailTip;
        const weight = smooth((t - roll.start) / (1 - roll.start));
        const point = roll.edge.find(([, y]) => y === roll.markedRows[0]).slice(2);
        const start = transform(source, rest.tailRoot, pose.tailRoot, rotation);
        return p => transform(p, source, lerp(start, plus(point, offset), weight), mix(rotation, radians(seatAngle), weight));
    }
    const root = rollRump(roll, rest.tailRoot, pose.tailRoot, t);
    return p => transform(p, rest.tailRoot, root, rotation);
}

function skin(profile, {drive, offset}, pose, t) {
    const {rest, floorY} = profile;
    const turn = angle(minus(pose.hip, pose.shoulder)) - angle(minus(rest.hip, rest.shoulder));
    const body = p => transform(p, rest.hip, pose.hip, turn);
    const attachment = (name, rotation) => p => transform(p, rest[name], pose[name], rotation);
    if (drive === 'tail') return tailSkin(profile, pose, turn, t);
    if (drive === 'head' || drive === 'head-front') {
        const face = attachment('head', radians(profile.headTurn) * smooth(t));
        const neck = attachment('shoulder', turn * .26);
        const front = drive === 'head-front' ? skin(profile, {drive: 'front'}, pose, t) : null;
        const span = rest.shoulder[1] - rest.head[1];
        return p => {
            const head = lerp(face(p), neck(p), smooth((p[1] - rest.head[1] - span * .45) / (span * .72)));
            if (!front) return head;
            const {chestX, shoulderX, forelegY, blendRadius} = profile.frontBlend;
            const raisedChest = p[1] - Math.max(0, p[0] - rest.shoulder[0]) * .7;
            const fore = lerp(head, body(p), smooth((p[0] - chestX) / (shoulderX - chestX))
                * smooth((p[1] - rest.head[1]) / span));
            return lerp(fore, front(p), smooth((raisedChest - forelegY + blendRadius) / (blendRadius * 2)));
        };
    }
    const ground = p => {
        // Preserve the mass above the contact area; only compress the fur that meets the floor.
        const y = p[1], zone = 22;
        return [p[0], y < floorY - zone ? y : floorY - zone * Math.exp(-(y - floorY + zone) / zone)];
    };
    if (drive === 'body') {
        const settled = smooth((t - .68) / .32);
        const target = floorY + 29;
        const push = Math.max(0, target - body(profile.rump)[1]) * settled;
        return p => {
            const q = body(p);
            q[1] += push * Math.exp(-((length(minus(p, profile.rump)) / 55) ** 2));
            return rollRump(profile.rumpRoll, p, lerp(q, ground(q), smooth(t * 3)), t);
        };
    }
    const [root, bend, ankle, toe] = chainNames[drive];
    const origins = [root, bend, ankle];
    const delta = segments(pose, chainNames[drive]).map((s, i) => angle(s) - angle(segments(rest, chainNames[drive])[i]));
    const [upper, lower] = origins.map((name, i) => attachment(name, delta[i]));
    const foot = attachment(toe, 0);
    const mapLeg = p => {
        const knee = jointBlend(p, rest[root], rest[ankle], rest[bend], -23, 18);
        const paw = jointBlend(p, rest[bend], rest[toe], rest[ankle], -15, 20);
        const join = jointBlend(p, rest[root], rest[bend], rest[root], 14, 76);
        const bent = lerp(lerp(upper(p), lower(p), knee), foot(p), paw);
        const moved = lerp(body(p), bent, join);
        const grounded = [moved[0], Math.min(floorY + .5, moved[1])];
        return drive === 'hind' && !offset ? rollRump(profile.rumpRoll, p, grounded, t) : grounded;
    };
    if (!offset) return mapLeg;
    const foreshortening = smooth(t) * (1 - (profile.seatedDepth ?? .54));
    return p => {
        const equivalent = minus(p, offset);
        const weight = jointBlend(equivalent, rest[root], rest[toe], rest[root], 28, 135);
        const anchor = rotate(offset, turn * (1 - weight));
        const moved = plus(mapLeg(equivalent), [anchor[0] * (1 - foreshortening * weight), anchor[1]]);
        return [moved[0], Math.min(floorY - 3, moved[1])];
    };
}

const svg = (w, h, content) => Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}">${content}</svg>`);
const pathMask = (part, w, h, x, y) => svg(w, h, `<path transform="translate(${-x} ${-y})" fill="white" d="${part.path}"/>`);

async function repairBody(input, profile) {
    if (!profile.bodyRepair) return input;
    const [x0, y0, width, height] = profile.crop;
    const {sample: [sx, sy, sw, sh]} = profile.bodyRepair;
    const raw = {width, height, channels: 4};
    const source = await sharp(input).ensureAlpha().raw().toBuffer();
    const mask = await sharp(pathMask(profile.bodyRepair, width, height, x0, y0)).blur(2).raw().toBuffer();
    const result = Buffer.from(source);
    // Borrow the original chest paper/fur texture beneath the accessory, never a second scarf.
    for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
        const i = (y * width + x) * 4;
        const opacity = mask[i + 3] / 255;
        if (!opacity) continue;
        const u = sx - x0 + Math.floor(sw * clamp((x + x0 - 240) / 260));
        const v = sy - y0 + Math.floor(sh * clamp((y + y0 - 400) / 155));
        const donor = (v * width + u) * 4;
        for (let c = 0; c < 3; c++) result[i + c] = Math.round(mix(source[i + c], source[donor + c], opacity));
    }
    return sharp(result, {raw}).png().toBuffer();
}

async function makeParts(profile, sourcePath) {
    const [x, y, w, h] = profile.crop;
    const left = Math.max(0, -x);
    const resized = await sharp(sourcePath).resize({width: profile.sourceWidth}).png().toBuffer();
    const input = await sharp(resized).extract({left: Math.max(x, 0), top: y, width: w - left, height: h})
        .extend({left, top: 0, bottom: 0, right: 0, background: '#000000'}).removeAlpha().png().toBuffer();
    const repaired = await repairBody(input, profile);
    const parts = [];
    for (const part of profile.parts) {
        const mask = await sharp(pathMask(part, w, h, x, y)).blur(part.feather ?? .5).png().toBuffer();
        const pixels = await sharp(part.drive === 'body' ? repaired : input).ensureAlpha().composite([{input: mask, blend: 'dest-in'}]).raw().toBuffer();
        if (part.hiddenJoin) featherRows(pixels, w, h, y, part.hiddenJoin);
        if (part.underBody) featherRows(pixels, w, h, y, part.underBody, true);
        parts.push({...part, pixels});
        await sharp(pixels, {raw: {width: w, height: h, channels: 4}}).png()
            .toFile(join(output, profile.name, part.name + '.png'));
    }
    return parts;
}

function featherRows(pixels, width, height, top, [start, end], invert = false) {
    for (let row = 0; row < height; row++) {
        const u = smooth((row + top - start) / (end - start));
        for (let col = 0; col < width; col++) pixels[(row * width + col) * 4 + 3] *= invert ? 1 - u : u;
    }
}

function meshTriangle(dst, src, texture, frame, dims) {
    const [[ax, ay], [bx, by], [cx, cy]] = dst;
    const det = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
    if (Math.abs(det) < .01) return;
    const [w, h] = dims;
    const x0 = Math.max(0, Math.floor(Math.min(ax, bx, cx))), x1 = Math.min(w - 1, Math.ceil(Math.max(ax, bx, cx)));
    const y0 = Math.max(0, Math.floor(Math.min(ay, by, cy))), y1 = Math.min(h - 1, Math.ceil(Math.max(ay, by, cy)));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
        const u = ((x - ax) * (cy - ay) - (y - ay) * (cx - ax)) / det;
        const v = ((bx - ax) * (y - ay) - (by - ay) * (x - ax)) / det;
        if (u < -1e-6 || v < -1e-6 || u + v > 1 + 1e-6) continue;
        const sx = Math.round(src[0][0] + (src[1][0] - src[0][0]) * u + (src[2][0] - src[0][0]) * v);
        const sy = Math.round(src[0][1] + (src[1][1] - src[0][1]) * u + (src[2][1] - src[0][1]) * v);
        const start = (sy * w + sx) * 4;
        if (texture[start + 3]) texture.copy(frame, (y * w + x) * 4, start, start + 4);
    }
}

function warp(profile, texture, map) {
    const [left, top, w, h] = profile.crop;
    const frame = Buffer.alloc(w * h * 4);
    const local = ([x, y]) => minus(map([x + left, y + top]), [left, top]);
    const step = profile.meshStep ?? 10;
    for (let y = 0; y < h - 1; y += step) for (let x = 0; x < w - 1; x += step) {
        const right = Math.min(x + step, w - 1), bottom = Math.min(y + step, h - 1);
        const points = [[x, y], [right, y], [right, bottom], [x, bottom]];
        const moved = points.map(local);
        for (const triangle of [[0, 1, 2], [0, 2, 3]]) {
            meshTriangle(triangle.map(i => moved[i]), triangle.map(i => points[i]), texture, frame, [w, h]);
        }
    }
    return frame;
}

function inspect(buffer, profile, floorGap = 13) {
    const [, top, w, h] = profile.crop;
    let area = 0, bottom = 0, edge = 0;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        if (buffer[(y * w + x) * 4 + 3] < 160) continue;
        area++;
        bottom = y + top;
        if (!x || x === w - 1 || !y || y === h - 1) edge++;
    }
    assert.ok(area > w * h * .18, 'Blank or largely missing animal');
    assert.equal(edge, 0, 'Texture clipped by viewport');
    assert.ok(bottom <= profile.floorY + 2 && bottom >= profile.floorY - floorGap, `Texture/floor gap: ${bottom}, floor ${profile.floorY}`);
    return {area, bottom};
}

function alphaDistance(pixels, width, height, reach) {
    const distance = new Uint8Array(width * height);
    for (let i = 0; i < distance.length; i++) distance[i] = pixels[i * 4 + 3] >= 160 ? reach : 0;
    for (let y = 1; y < height; y++) for (let x = 1; x < width; x++) {
        const i = y * width + x;
        distance[i] = Math.min(distance[i], distance[i - 1] + 1, distance[i - width] + 1);
    }
    for (let y = height - 2; y >= 0; y--) for (let x = width - 2; x >= 0; x--) {
        const i = y * width + x;
        distance[i] = Math.min(distance[i], distance[i + 1] + 1, distance[i + width] + 1);
    }
    return distance;
}

function blendBodyJoins(profile, parts, layers, width, height) {
    if (!profile.bodyJoinFeather) return;
    const body = layers[parts.findIndex(part => part.drive === 'body')].input;
    const support = alphaDistance(body, width, height, 8);
    for (const [name, reach] of Object.entries(profile.bodyJoinFeather)) {
        const layer = layers[parts.findIndex(part => part.name === name)].input;
        const edge = alphaDistance(layer, width, height, reach);
        // Feather joins only where the torso already fills the silhouette.
        for (let i = 0; i < edge.length; i++) {
            if (edge[i] >= reach || !support[i]) continue;
            layer[i * 4 + 3] *= 1 - smooth(support[i] / 8) * (1 - smooth(edge[i] / reach));
        }
    }
}

function lowestOpaque(buffers, width, x, top, bottom) {
    for (let y = bottom; y >= top; y--) {
        if (buffers.some(buffer => buffer[(y * width + x) * 4 + 3] >= 160)) return y;
    }
    return -1;
}

function smoothBack(profile, parts, layers, pose, width, height, t) {
    const roll = profile.rumpRoll;
    if (!roll?.backSmooth || t <= roll.start) return;
    const {shoulderSpan, radius, depth} = roll.backSmooth;
    const [left, top] = profile.crop;
    const [first, last] = shoulderSpan.map(y => Math.round(pose.shoulder[1] - top + y));
    const indices = ['head-chest-foreleg', 'body', 'near-hind'].map(name => parts.findIndex(part => part.name === name));
    const buffers = indices.map(i => layers[i].input);
    const edge = [];
    for (let y = Math.max(0, first - radius); y <= Math.min(height - 1, last + radius); y++) {
        let x = width - 1;
        const limit = Math.max(0, Math.round(pose.shoulder[0] - left));
        while (x > limit && !buffers.some(buffer => buffer[(y * width + x) * 4 + 3] >= 160)) x--;
        if (x > limit) edge[y] = x;
    }
    const weight = smooth((t - roll.start) / (1 - roll.start));
    for (let y = Math.max(0, first); y <= Math.min(height - 1, last); y++) {
        const x = edge[y];
        if (x === undefined) continue;
        let sum = 0, total = 0;
        for (let i = -radius; i <= radius; i++) {
            const neighbor = edge[y + i];
            if (neighbor === undefined || Math.abs(neighbor - x) > 70) continue;
            const w = radius + 1 - Math.abs(i);
            sum += w * neighbor;
            total += w;
        }
        const fade = smooth(Math.min(y - first, last - y) / radius);
        const delta = (sum / total - x) * weight * fade;
        if (Math.abs(delta) < .3) continue;
        const start = Math.max(0, x - depth);
        const end = Math.min(width - 1, x + Math.ceil(Math.abs(delta)) + 3);
        for (const buffer of buffers) {
            const row = Buffer.from(buffer.subarray((y * width + start) * 4, (y * width + end + 1) * 4));
            for (let u = start; u <= end; u++) {
                const from = u - delta * (1 - smooth((x - u) / depth));
                const index = Math.min(end, Math.max(start, Math.floor(from)));
                const a = (index - start) * 4, b = Math.min(a + 4, row.length - 4);
                const f = clamp(from - index), out = (y * width + u) * 4;
                const alphaA = (1 - f) * row[a + 3], alphaB = f * row[b + 3];
                const alpha = alphaA + alphaB;
                buffer[out + 3] = Math.round(alpha);
                if (!alpha) continue;
                for (let c = 0; c < 3; c++) buffer[out + c] = Math.round((row[a + c] * alphaA + row[b + c] * alphaB) / alpha);
            }
        }
    }
}

function moveContactColumn(src, dst, width, x, anchor, base, drop) {
    // Stretch the existing painted edge toward the floor, tapering to zero above it.
    for (let y = anchor + 1; y <= Math.ceil(base + drop + 3); y++) {
        const sourceY = y - drop * clamp((y - anchor) / (base + drop - anchor));
        const row = Math.floor(sourceY), f = sourceY - row;
        const a = (row * width + x) * 4, b = a + width * 4, target = (y * width + x) * 4;
        const left = (1 - f) * src[a + 3], right = f * src[b + 3], alpha = left + right;
        dst[target + 3] = Math.round(alpha);
        if (!alpha) continue;
        for (let c = 0; c < 3; c++) dst[target + c] = Math.round((src[a + c] * left + src[b + c] * right) / alpha);
    }
}

function settleContour(profile, parts, layers, width, height, t) {
    const contour = profile.seatedContour;
    if (!contour || t <= contour.start) return null;
    const [left, top] = profile.crop;
    const floor = profile.floorY - top - 2;
    const [from, to] = contour.range.map(x => x - left);
    const [leading, trailing] = contour.falloff;
    const indices = contour.parts.map(name => parts.findIndex(part => part.name === name));
    const source = indices.map(i => layers[i].input);
    const destination = source.map(buffer => Buffer.from(buffer));
    const settle = smooth((t - contour.start) / (1 - contour.start));
    for (let x = Math.max(0, from - leading); x <= Math.min(width - 1, to + trailing); x++) {
        const base = lowestOpaque(source, width, x, floor - contour.depth, Math.min(height - 1, floor + 3));
        if (base < 0 || base >= floor) continue;
        const edge = Math.max((from - x) / leading, (x - to) / trailing, 0);
        const drop = Math.min(floor - base, contour.maxDrop) * settle * (1 - smooth(edge));
        if (drop < .01) continue;
        const anchor = Math.max(0, base - contour.depth);
        source.forEach((buffer, i) => moveContactColumn(buffer, destination[i], width, x, anchor, base, drop));
    }
    indices.forEach((index, i) => { layers[index].input = destination[i]; });
    if (t !== 1) return null;
    const columns = Array.from({length: to - from + 1}, (_, i) => from + i);
    const gaps = columns.map(x => floor - lowestOpaque(destination, width, x, floor - 1, floor + 3));
    assert.ok(gaps.every(gap => gap <= 1), 'Seated rump-to-leg contour has a gap above the floor');
    return {columns: columns.length, maxGap: Math.max(...gaps)};
}

function traceRump(profile, layers, parts, maps, t) {
    if (!profile.rumpRoll) return null;
    const [start, end] = profile.rumpRoll.markedRows;
    const [left, top, width, height] = profile.crop;
    const samples = profile.rumpRoll.edge.filter(([, y]) => y >= start && y <= end);
    const indices = ['body', 'near-hind'].map(name => parts.findIndex(part => part.name === name));
    const buffers = indices.map(i => layers[i].input);
    return samples.map(([x, y]) => {
        const index = indices.find(i => parts[i].pixels[((y - top) * width + x - left) * 4 + 3] >= 100) ?? indices[0];
        const point = maps[index]([x, y]).map(Math.round);
        if (t === 1) {
            assert.ok(Math.abs(point[1] - profile.floorY) <= 7, 'Marked standing rump edge failed to roll down');
            if (profile.rumpRoll.tailTip && y === start) {
                assert.ok(Math.abs(point[1] - (profile.floorY - 2)) <= 1, 'Top rump landmark must reach the floor');
            }
            const col = point[0] - left;
            const bottom = Math.max(...[-2, -1, 0, 1, 2].map(dx =>
                lowestOpaque(buffers, width, col + dx, profile.floorY - top - 8, Math.min(height - 1, profile.floorY - top + 2))));
            assert.ok(bottom >= profile.floorY - top - 7, 'Marked rump edge missing at floor in rendered frame');
        }
        return point;
    });
}

function checkTailContact(profile, parts, layers, t) {
    const roll = profile.rumpRoll;
    if (!roll?.tailTip) return null;
    const [left, top, width, height] = profile.crop;
    const find = name => layers[parts.findIndex(part => part.name === name)].input;
    const tail = find('tail'), body = find('body'), hind = find('near-hind');
    let bottom = -1, overlap = 0;
    for (let i = 3; i < tail.length; i += 4) {
        if (tail[i] < 160) continue;
        bottom = Math.floor(i / (width * 4));
        if (body[i] >= 160 || hind[i] >= 160) overlap++;
    }
    const floor = profile.floorY - top;
    assert.ok(bottom >= 0 && bottom <= floor, 'Tail is missing or penetrates the floor');
    assert.ok(overlap >= 40, 'Tail separated from the body');
    if (t !== 1) return null;
    const clearance = floor - bottom;
    const [min, max] = roll.tailClearance;
    assert.ok(clearance >= min && clearance <= max, `Unexpected seated tail clearance: ${clearance}`);
    const [contactX, contactY] = roll.edge.find(([, y]) => y === roll.markedRows[0]).slice(2);
    const tipX = contactX - left + roll.tailTip.offset[0];
    const tip = Math.max(...[-3, -2, -1, 0, 1, 2, 3].map(dx =>
        lowestOpaque([tail], width, tipX + dx, floor - max - 2, Math.min(height - 1, floor))));
    assert.ok(tip >= bottom - 2 && Math.abs(tip + top - contactY) <= max, 'Tail tip missed the marked contact');
    return clearance;
}

async function render(profile, parts, pose, t, farPoses = null) {
    const [, , width, height] = profile.crop;
    const raw = {width, height, channels: 4};
    const maps = parts.map(part => skin(profile, part, part.offset && farPoses ? farPoses[part.drive] : pose, t));
    const layers = parts.map((part, i) => ({input: warp(profile, part.pixels, maps[i]), raw}));
    smoothBack(profile, parts, layers, pose, width, height, t);
    blendBodyJoins(profile, parts, layers, width, height);
    const contour = settleContour(profile, parts, layers, width, height, t);
    if (t === 1) {
        const body = layers[parts.findIndex(part => part.drive === 'body')].input;
        const [, top] = profile.crop;
        const y = profile.floorY - top - 6;
        const contacts = Array.from({length: width}, (_, x) => body[(y * width + x) * 4 + 3])
            .filter(alpha => alpha >= 160).length;
        assert.ok(contacts >= 8, 'Seated body layer did not settle on the floor');
    }
    const buffer = await sharp({create: {width, height, channels: 4, background: '#00000000'}}).composite(layers).raw().toBuffer();
    const check = inspect(buffer, profile, farPoses ? 28 : 13);
    if (contour) check.contour = contour;
    const clearance = checkTailContact(profile, parts, layers, t);
    if (clearance !== null) check.tailClearance = clearance;
    if (t === 0 || t === 1) {
        const trace = traceRump(profile, layers, parts, maps, t);
        if (trace) check.rumpEdge = trace;
    }
    return {check, png: await sharp(buffer, {raw}).png().toBuffer(), layers};
}

// Source faces left. A planted paw moves right at a constant speed in local space.
const walkStride = 156;
const stance = .76;
const beats = [
    ['hind', false, 0, 24], ['front', false, .25, 31],
    ['hind', true, .5, 22], ['front', true, .75, 27]
];

function walkFoot(cycle, lift) {
    cycle -= Math.floor(cycle);
    const swing = clamp((cycle - stance) / (1 - stance));
    return [walkStride * (cycle - stance / 2 - smooth(swing)), -lift * Math.sin(Math.PI * swing) ** 2];
}

function walkPose({rest}, cycle, weight) {
    const wave = Math.PI * 2 * cycle;
    const hip = plus(rest.hip, [0, weight * (21 + 3 * Math.sin(wave * 2))]);
    const turn = weight * .012 * Math.sin(wave * 2);
    const shoulder = transform(rest.shoulder, rest.hip, hip, turn);
    const pose = {hip, shoulder, head: plus(rest.head, minus(shoulder, rest.shoulder)),
        tailRoot: transform(rest.tailRoot, rest.hip, hip, turn), tailSway: weight * .045 * Math.sin(wave)};
    const far = {front: {...pose}, hind: {...pose}};
    for (const [drive, distant, phase, lift] of beats) {
        const chain = chainNames[drive];
        const [root, bend, ankle, toe] = chain;
        const [upper, lower, paw] = segments(rest, chain);
        const target = distant ? far[drive] : pose;
        const shift = walkFoot(cycle - phase, lift);
        target[toe] = plus(rest[toe], shift.map(value => weight * value));
        target[ankle] = minus(target[toe], paw);
        target[bend] = joint(target[root], target[ankle], length(upper), length(lower), drive === 'front' ? -1 : 1);
        segments(target, chain).forEach((segment, i) => {
            assert.ok(Math.abs(length(segment) - length([upper, lower, paw][i])) < 1e-7);
        });
    }
    return [pose, far];
}

async function stationAtlas() {
    const configPath = join(sourceDir, 'shiba-selected.json');
    const profile = {...await load(configPath), name: 'shiba-station'};
    const folder = join(output, profile.name);
    await mkdir(folder, {recursive: true});
    const parts = await makeParts(profile, join(dirname(configPath), profile.sourceFile));
    const sit = motion(profile);
    verifyMotion(profile, sit);
    for (let i = 0; i <= 240; i++) {
        const cycle = i / 240;
        const feet = beats.map(([, , phase, lift]) => walkFoot(cycle - phase, lift));
        assert.ok(feet.filter(([, y]) => y < -1e-8).length <= 1, 'Walk lost lateral support');
        walkPose(profile, cycle, 1);
        if (i <= 120) walkPose(profile, cycle, smooth(cycle * 2));
    }

    const frames = [];
    for (let i = 0; i < 16; i++) {
        const t = i / 15;
        frames.push((await render(profile, parts, sit(t), t)).png);
    }
    const walk = [];
    for (let i = 0; i < 16; i++) {
        const [pose, far] = walkPose(profile, i / 16, 1);
        walk.push((await render(profile, parts, pose, 0, far)).png);
    }
    frames.push(...walk);
    for (let i = 0; i < 8; i++) {
        const [pose, far] = walkPose(profile, i / 16, smooth(i / 8));
        frames.push((await render(profile, parts, pose, 0, far)).png);
    }
    const side = 128, columns = 8;
    const tiles = [];
    for (const [i, png] of frames.entries()) {
        const input = await sharp(png).resize(side, side).png().toBuffer();
        tiles.push({input, left: side * (i % columns), top: side * Math.floor(i / columns)});
    }
    await sharp({create: {width: side * columns, height: side * frames.length / columns, channels: 4,
        background: '#00000000'}}).composite(tiles).png({compressionLevel: 9, palette: true, dither: 0})
        .toFile('unity/HomelessPetHelper/Assets/Art/PetSit/T_Station_ShibaMotion.png');
    const preview = [];
    for (const png of walk) preview.push(await tile(profile, png, '#f2f5ed'));
    await sharp(preview, {join: {animated: true}}).gif({delay: 50, loop: 0}).toFile(join(folder, 'walk.gif'));
    console.log('PASS shiba-station: 241 gait and sit samples; 40 atlas cells.');
}

async function tile(profile, png, background) {
    const [, top, w, h] = profile.crop;
    const y = profile.floorY - top;
    const ground = svg(w, h, `<path d="M 8,${y + 2} H ${w - 8}" stroke="${background === '#283937' ? '#647a72' : '#cbd7cd'}"/>`);
    const composited = await sharp({create: {width: w, height: h, channels: 3, background}})
        .composite([{input: ground}, {input: png}]).png().toBuffer();
    return sharp(composited).resize({height: 265}).png().toBuffer();
}

async function run(name, manifest) {
    const configPath = join(sourceDir, name + '.json');
    const profile = {...await load(configPath), name};
    if (profile.motionStudy) {
        const study = await load(join(dirname(configPath), profile.motionStudy));
        Object.assign(profile, {rest: study.standing, keys: study.keys});
    }
    const sourcePath = profile.sourceFile ? join(dirname(configPath), profile.sourceFile)
        : join(batch, manifest.animals.find(animal => animal.id === profile.id)[profile.source]);
    const motionAt = motion(profile);
    verifyMotion(profile, motionAt);
    await mkdir(join(output, name), {recursive: true});
    const parts = await makeParts(profile, sourcePath);
    const frames = [], images = [], findings = [];
    for (let i = 0; i <= 30; i++) {
        const t = i / 30;
        const {png, check, layers} = await render(profile, parts, motionAt(t), t);
        if (profile.seatedContact && i === 30) {
            check.support = seatSupport(profile, parts, layers);
            for (const part of profile.seatedContact) {
                assert.ok(check.support[part] >= 8, `Expected visible support from ${part}: ${JSON.stringify(check.support)}`);
            }
            assert.ok(check.support.tail <= (profile.rumpRoll?.tailTip ? 12 : 0), 'Only the tail tip may touch the floor');
        }
        findings.push(check);
        frames.push(await tile(profile, png, '#f2f5ed'));
        if ([0, 9, 16, 24, 30].includes(i)) {
            await writeFile(join(output, name, `frame-${i}.png`), png);
            images.push([frames.at(-1), await tile(profile, png, '#283937')]);
        }
    }
    const {width, height} = await sharp(frames[0]).metadata();
    const tiles = images.flatMap((pair, i) => pair.map((input, j) => ({input, left: width * i, top: height * j})));
    await sharp({create: {width: width * images.length, height: height * 2, channels: 3, background: '#f2f5ed'}})
        .composite(tiles).jpeg({quality: 88}).toFile(join(sourceDir, name + '-poses.jpg'));
    const loop = [...frames, ...frames.slice(1, -1).reverse()];
    const delay = loop.map((_, i) => i === 0 || i === 30 ? 690 : 45);
    const previewDir = profile.sourceFile ? join(output, name) : output;
    await sharp(loop, {join: {animated: true}}).gif({delay, loop: 0}).toFile(join(previewDir, name + '-sit.gif'));
    console.log(`PASS ${name}: 241 skeleton samples; ${frames.length} raster frames.`);
    return {id: profile.id, skeletonSamples: 241, rasterFrames: findings};
}

async function seatTarget(manifest) {
    const profile = {...await load(join(sourceDir, 'shiba.json')), name: 'shiba-seat-target'};
    const target = await load(join(sourceDir, 'shiba-seat-target.json'));
    const study = await load(join(sourceDir, profile.motionStudy));
    profile.rest = study.standing;
    profile.keys = [study.keys[0], {...target.key, hindLift: profile.rest.hindToe[1] - target.hindToeY}];
    profile.rump = target.rump;
    profile.seatedDepth = target.seatedDepth;
    if (target.hindPawJoin) {
        const index = profile.parts.findIndex(part => part.name === 'near-hind');
        const [hind] = profile.parts.splice(index, 1);
        profile.parts.splice(profile.parts.findIndex(part => part.drive === 'body'), 0,
            {...hind, name: 'near-hind-under-body', underBody: target.hindPawJoin});
        profile.parts.splice(index + 1, 0, {...hind, name: 'near-hind-paw', hiddenJoin: target.hindPawJoin});
    }
    const sample = motion(profile);
    const pose = sample(1);
    for (const names of Object.values(chainNames)) {
        segments(pose, names).forEach((segment, i) =>
            assert.ok(Math.abs(length(segment) - length(segments(profile.rest, names)[i])) < 1e-7));
    }
    assert.ok(Math.abs(pose.shoulder[0] - pose.frontToe[0]) < 24, 'Front support under shoulder');
    assert.ok(Math.abs(pose.shoulder[0] - pose.elbow[0]) < 16, 'Front upper leg close to vertical');
    assert.ok(pose.stifle[0] < pose.hip[0] && pose.stifle[1] < pose.hip[1], 'Thigh folds forward');
    assert.ok(pose.hindToe[0] < pose.hock[0] && pose.hindToe[0] > pose.frontToe[0], 'Rear paw tucks ahead of rump');
    const folder = join(output, profile.name);
    await mkdir(folder, {recursive: true});
    const shiba = manifest.animals.find(a => a.id === profile.id);
    const parts = await makeParts(profile, join(batch, shiba[profile.source]));
    const {png, check, layers} = await render(profile, parts, pose, 1);
    const support = seatSupport(profile, parts, layers);
    assert.ok(support['head-chest-foreleg'] >= 8, 'Visible near forepaw support');
    assert.ok(support['near-hind-paw'] >= 8, 'Visible near hind paw support');
    assert.ok(support.body >= 8, 'Visible rump support');
    await writeFile(join(folder, 'seat.png'), png);
    await writeFile(join(folder, 'check.json'), JSON.stringify({pose, floor: profile.floorY, support, ...check}, null, 2) + '\n');
    await seatBoard(profile, folder, png, pose);
    console.log('PASS Shiba seated endpoint, original textures and fixed limb lengths.');
}

function seatSupport(profile, parts, layers) {
    const [, top, width] = profile.crop;
    const counts = Object.fromEntries(parts.map(part => [part.name, 0]));
    for (let x = 0; x < width; x++) {
        const visible = new Set();
        for (let y = profile.floorY - 3; y <= profile.floorY + 1; y++) {
            const pixel = ((y - top) * width + x) * 4 + 3;
            const layer = layers.findLastIndex(({input}) => input[pixel] >= 160);
            if (layer >= 0) visible.add(parts[layer].name);
        }
        for (const id of visible) counts[id]++;
    }
    return counts;
}

async function seatBoard(profile, folder, png, pose) {
    const [left, top, width, height] = profile.crop;
    const point = name => [pose[name][0] - left, pose[name][1] - top].join(',');
    const limbs = Object.values(chainNames).map(names =>
        `<polyline points="${names.map(point).join(' ')}" fill="none" stroke="#23684f" stroke-width="3" stroke-linejoin="round"/>`).join('');
    const marks = [...new Set(Object.values(chainNames).flat())].map(name => {
        const [x, y] = point(name).split(',');
        return `<circle cx="${x}" cy="${y}" r="4" fill="#fcf8eb" stroke="#23684f" stroke-width="2"/>`;
    }).join('');
    const skeleton = await sharp(png).composite([{input: svg(width, height, limbs + marks)}]).png().toBuffer();
    await sharp(skeleton).png().toFile(join(folder, 'seat-bones.png'));
    const tiles = await Promise.all([tile(profile, png, '#f2f5ed'), tile(profile, skeleton, '#283937')]);
    const box = await sharp(tiles[0]).metadata();
    const labels = svg(box.width * 2, 32, '<g fill="#31473d" font-family="Segoe UI" font-size="16">'
        + `<text x="12" y="22">Seated target</text><text x="${box.width + 12}" y="22">Fixed-length leg chains</text></g>`);
    await sharp({create: {width: box.width * 2, height: box.height + 32, channels: 3, background: '#f2f5ed'}})
        .composite([{input: labels, top: 0, left: 0}, ...tiles.map((input, i) => ({input, top: 32, left: i * box.width}))])
        .jpeg({quality: 91}).toFile(join(folder, 'review.jpg'));
}

const manifest = still || names.some(name => variants.includes(name)) ? await load(join(batch, 'manifest.json')) : null;
if (station) {
    await stationAtlas();
    process.exit(0);
}
if (still) {
    await seatTarget(manifest);
    process.exit(0);
}
const report = [];
for (const name of names) report.push(await run(name, manifest));
const reportName = names.length === variants.length && variants.every(name => names.includes(name))
    ? 'checks.json' : 'checks-' + names.join('-') + '.json';
await writeFile(join(output, reportName), JSON.stringify(report, null, 2) + '\n');
