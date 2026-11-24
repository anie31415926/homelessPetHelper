import assert from 'node:assert/strict';
import {readFile, writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import sharp from 'sharp';

const base = 'test-results/pet-roster-20261124';
const config = JSON.parse(await readFile('art-source/pet-roster-20261124/dog-shiba-sit-study.json', 'utf8'));
const sources = JSON.parse(await readFile(join(base, 'manifest.json'), 'utf8'));
const shiba = sources.animals.find(animal => animal.id === 'dog.shiba');
const rest = config.standing;
const len = ([x, y], [u, v]) => Math.hypot(x - u, y - v);
const add = ([x, y], [u, v]) => [x + u, y + v];
const sub = ([x, y], [u, v]) => [x - u, y - v];
const polar = (distance, degrees) => [distance * Math.cos(degrees * Math.PI / 180), distance * Math.sin(degrees * Math.PI / 180)];
const smooth = a => a * a * (3 - 2 * a);
const blend = (a, b, t) => a + (b - a) * t;
const frontL = [len(rest.shoulder, rest.elbow), len(rest.elbow, rest.carpus), len(rest.carpus, rest.frontToe)];
const hindL = [len(rest.hip, rest.stifle), len(rest.stifle, rest.hock), len(rest.hock, rest.hindToe)];
const torsoL = len(rest.shoulder, rest.hip), initialBodyAngle = config.keys[0].bodyAngle;

function solveJoint([x, y], [u, v], a, b, branch) {
    const dx = u - x, dy = v - y, d = Math.hypot(dx, dy);
    assert.ok(d > Math.abs(a - b) && d < a + b, `Unreachable target: ${d.toFixed(3)} for ${a.toFixed(3)} + ${b.toFixed(3)}`);
    const axial = (a * a - b * b + d * d) / (2 * d);
    const lateral = branch * Math.sqrt(a * a - axial * axial);
    return [x + (dx * axial - dy * lateral) / d, y + (dy * axial + dx * lateral) / d];
}

function sample(progress) {
    const i = Math.max(1, config.keys.findIndex(key => key.at >= progress));
    const a = config.keys[i - 1], b = config.keys[i], t = smooth((progress - a.at) / (b.at - a.at));
    const value = name => blend(a[name], b[name], t);
    const hip = a.hip.map((x, j) => blend(x, b.hip[j], t));
    const bodyAngle = value('bodyAngle');
    const shoulder = sub(hip, polar(torsoL, bodyAngle));
    const head = add(shoulder, add(sub(rest.head, rest.shoulder), [0, -10 * progress]));
    const nose = add(head, sub(rest.nose, rest.head));
    const frontToe = [value('frontX'), rest.frontToe[1] - value('frontLift')];
    const carpus = sub(frontToe, polar(frontL[2], blend(109.02560603756869, 98, progress)));
    const elbow = solveJoint(shoulder, carpus, ...frontL.slice(0, 2), -1);
    const hindToe = [value('hindX'), rest.hindToe[1] - value('hindLift')];
    const hock = sub(hindToe, polar(hindL[2], value('hockAngle')));
    const stifle = solveJoint(hip, hock, ...hindL.slice(0, 2), 1);
    const [tailX, tailY] = sub(rest.tailRoot, rest.hip), theta = (bodyAngle - initialBodyAngle) * Math.PI / 180;
    const tailRoot = add(hip, [tailX * Math.cos(theta) - tailY * Math.sin(theta), tailX * Math.sin(theta) + tailY * Math.cos(theta)]);
    return {hip, shoulder, head, nose, frontToe, carpus, elbow, hindToe, hock, stifle, tailRoot};
}

function verify() {
    const samples = Array.from({length: 241}, (_, i) => sample(i / 240));
    for (const [i, p] of samples.entries()) {
        for (const [names, lengths] of [[['shoulder', 'elbow', 'carpus', 'frontToe'], frontL], [['hip', 'stifle', 'hock', 'hindToe'], hindL]]) {
            for (let j = 0; j < 3; j++) assert.ok(Math.abs(len(p[names[j]], p[names[j + 1]]) - lengths[j]) < 1e-7);
        }
        assert.ok(Math.abs(len(p.hip, p.shoulder) - torsoL) < 1e-7);
        assert.ok(p.frontToe[1] <= config.floorY && p.hindToe[1] <= config.floorY);
        assert.ok(p.frontToe[1] === rest.frontToe[1] || p.hindToe[1] === rest.hindToe[1], 'Airborne support pair');
        if (i) for (const name of Object.keys(p)) assert.ok(len(p[name], samples[i - 1][name]) < 6);
    }
    for (const [name, p] of Object.entries(rest)) assert.ok(len(p, samples[0][name]) < .02, `Bind mismatch: ${name}`);
    const last = samples.at(-1);
    assert.ok(last.stifle[0] < last.hip[0] && last.stifle[1] < last.hip[1]);
    assert.ok(last.hock[0] > last.stifle[0] && last.hock[1] > last.stifle[1]);
    assert.ok(last.hip[1] - rest.hip[1] > 100);
    return {samples: samples.length, frontL, hindL, torsoL, final: last};
}

function diagram(p) {
    const chain = (names, color, width = 5) => `<polyline points="${names.map(name => p[name].join(',')).join(' ')}" stroke="${color}" stroke-width="${width}" fill="none" stroke-linejoin="round" stroke-linecap="round"/>`;
    const mark = (names, color) => names.map(name => `<circle cx="${p[name][0]}" cy="${p[name][1]}" r="4" fill="#fff" stroke="${color}" stroke-width="2"/>`).join('');
    return `<line x1="326" y1="${config.floorY}" x2="676" y2="${config.floorY}" stroke="#aebbb4" stroke-width="2"/>
    ${chain(['nose', 'head', 'shoulder', 'hip', 'tailRoot'], '#274a45')}
    ${chain(['shoulder', 'elbow', 'carpus', 'frontToe'], '#367996')}
    ${chain(['hip', 'stifle', 'hock', 'hindToe'], '#b96838')}
    ${mark(['nose', 'head', 'shoulder', 'hip', 'tailRoot'], '#274a45')}
    ${mark(['elbow', 'carpus', 'frontToe'], '#367996')}
    ${mark(['stifle', 'hock', 'hindToe'], '#b96838')}`;
}

function overlay(p) {
    return Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="360" height="390" viewBox="325 85 360 390">${diagram(p)}</svg>`);
}

const result = verify();
const side = await sharp(join(base, shiba.turnaroundFile)).resize({width: 1000}).png().toBuffer();
const reference = await sharp(side).extract({left: 325, top: 85, width: 360, height: 390}).composite([{input: overlay(sample(0))}]).png().toBuffer();
const render = t => sharp({create: {width: 360, height: 390, channels: 3, background: '#f6f8f3'}})
    .composite([{input: overlay(sample(t))}]).png().toBuffer();
const frames = [reference];
for (const t of [.29, .48, .72, 1]) frames.push(await render(t));
const labels = ['Standing landmarks', 'Forepaw set', 'Pelvis lowering', 'Rear support', 'Settled'];
const tiles = frames.flatMap((input, i) => [{input, left: i * 360, top: 30},
    {input: Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="360" height="30"><text x="14" y="22" font-family="Segoe UI" font-size="18" fill="#304945">${labels[i]}</text></svg>`), left: i * 360, top: 0}]);
await sharp({create: {width: 1800, height: 420, channels: 3, background: '#f6f8f3'}}).composite(tiles).png().toFile(join(base, 'shiba-sit-blocking.png'));
const motion = [];
for (let i = 0; i <= 30; i++) motion.push(await render(i / 30));
await sharp(motion, {join: {animated: true}}).gif({delay: motion.map((_, i) => (i === 0 || i === 30 ? 700 : 55)), loop: 0})
    .toFile(join(base, 'shiba-sit-blocking.gif'));
await writeFile(join(base, 'shiba-sit-check.json'), JSON.stringify(result, null, 2) + '\n');
console.log(`PASS ${result.samples} canine skeleton samples; fixed lengths, contact, bind and seated fold.`);
