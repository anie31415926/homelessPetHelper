import assert from 'node:assert/strict';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {dirname, join} from 'node:path';
import sharp from 'sharp';

const sourceDir = 'art-source/pet-roster-20261124/sit-texture';
const batch = 'test-results/pet-roster-20261124';
const output = join(batch, 'sit-texture');
const variants = ['tuxedo', 'shiba'];
const names = process.argv.length > 2 ? process.argv.slice(2) : variants;
const still = names.length === 1 && names[0] === 'shiba-seat-target';
assert.ok(still || new Set(names).size === names.length && names.every(name => variants.includes(name)),
    'Use tuxedo and/or shiba, or shiba-seat-target for the seated still');
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

function verifyMotion(rest, sample) {
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
        assert.ok(pose.frontToe[1] <= rest.frontToe[1] && pose.hindToe[1] <= rest.hindToe[1]);
        assert.ok(pose.frontToe[1] === rest.frontToe[1] || pose.hindToe[1] === rest.hindToe[1]);
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

function skin(profile, {drive, offset}, pose, t) {
    const {rest, floorY} = profile;
    const turn = angle(minus(pose.hip, pose.shoulder)) - angle(minus(rest.hip, rest.shoulder));
    const body = p => transform(p, rest.hip, pose.hip, turn);
    const attachment = (name, rotation) => p => transform(p, rest[name], pose[name], rotation);
    if (drive === 'tail') return attachment('tailRoot', turn * profile.tailTurn);
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
            return lerp(q, ground(q), smooth(t * 3));
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
        return [moved[0], Math.min(floorY + .5, moved[1])];
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
    const resized = await sharp(sourcePath).resize({width: profile.sourceWidth}).png().toBuffer();
    const input = await sharp(resized).extract({left: x, top: y, width: w, height: h}).removeAlpha().png().toBuffer();
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

function inspect(buffer, profile) {
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
    assert.ok(bottom <= profile.floorY + 2 && bottom >= profile.floorY - 13, `Texture/floor gap: ${bottom}, floor ${profile.floorY}`);
    return {area, bottom};
}

async function render(profile, parts, pose, t) {
    const [, , width, height] = profile.crop;
    const raw = {width, height, channels: 4};
    const layers = parts.map(part => ({input: warp(profile, part.pixels, skin(profile, part, pose, t)), raw}));
    if (t === 1) {
        const body = layers[parts.findIndex(part => part.drive === 'body')].input;
        const [, top] = profile.crop;
        const y = profile.floorY - top - 6;
        const contacts = Array.from({length: width}, (_, x) => body[(y * width + x) * 4 + 3])
            .filter(alpha => alpha >= 160).length;
        assert.ok(contacts >= 8, 'Seated body layer did not settle on the floor');
    }
    const buffer = await sharp({create: {width, height, channels: 4, background: '#00000000'}}).composite(layers).raw().toBuffer();
    return {check: inspect(buffer, profile), png: await sharp(buffer, {raw}).png().toBuffer(), layers};
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
    const record = manifest.animals.find(animal => animal.id === profile.id);
    const sourcePath = join(batch, record[profile.source]);
    const motionAt = motion(profile);
    verifyMotion(profile.rest, motionAt);
    await mkdir(join(output, name), {recursive: true});
    const parts = await makeParts(profile, sourcePath);
    const frames = [], images = [], findings = [];
    for (let i = 0; i <= 30; i++) {
        const t = i / 30;
        const {png, check} = await render(profile, parts, motionAt(t), t);
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
    await sharp(loop, {join: {animated: true}}).gif({delay, loop: 0}).toFile(join(output, name + '-sit.gif'));
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

const manifest = await load(join(batch, 'manifest.json'));
if (still) {
    await seatTarget(manifest);
    process.exit(0);
}
const report = [];
for (const name of names) report.push(await run(name, manifest));
const reportName = names.length === variants.length ? 'checks.json' : 'checks-' + names[0] + '.json';
await writeFile(join(output, reportName), JSON.stringify(report, null, 2) + '\n');
