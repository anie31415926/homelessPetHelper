import {ramp} from './join.js';

const parts = [
    {id: 'hip', x: 334, y: 0, z: 0, radius: 45, ry: 61, rz: 50},
    {id: 'chest', x: 203, y: 0, z: 0, radius: 49, ry: 63, rz: 49},
    {id: 'head', x: 128, y: 48, z: 0, radius: 51, ry: 51, rz: 51}
];
const leg = (anchor, side) => {
    const front = anchor === 'chest';
    return {anchor, side, x: front ? 200 : 331, y: -25, z: 32 * side, lengths: front ? [46, 36] : [47, 39]};
};
const legs = [-1, 1].flatMap(side => ['chest', 'hip'].map(anchor => leg(anchor, side)));
const rad = degree => degree * Math.PI / 180;

export function sidePose(t) {
    const crouch = ramp(0, .43, t), roll = ramp(.27, .91, t), relax = ramp(.66, 1, t);
    const theta = -Math.PI / 2 * roll;
    const cos = Math.cos(theta), sin = Math.sin(theta);
    const world = ([x, y, z], lean = 0) => [x, y * cos - z * sin + lean, y * sin + z * cos];
    const lift = {hip: 0, chest: 15 * (crouch - relax), head: 14 * crouch - 9 * relax};
    const body = parts.map(p => {
        const xy = world([p.x, p.y, p.z], lift[p.id]);
        const extent = Math.hypot(p.ry * cos, p.rz * sin);
        return {...p, xy, extent};
    });
    const limbs = legs.map(l => {
        const fore = l.anchor === 'chest', side = l.side;
        const a = [l.x, l.y, l.z], amount = crouch * (side === -1 ? .87 : 1);
        const phase = [rad((fore ? -87 : -115) + (fore ? -56 : -46) * amount),
            rad((fore ? -99 : -57) + (fore ? 51 : 85) * amount - 15 * relax)];
        const points = [a];
        for (let i = 0; i < 2; i++) {
            const [x, y, z] = points[i];
            points.push([x + Math.cos(phase[i]) * l.lengths[i], y + Math.sin(phase[i]) * l.lengths[i], z]);
        }
        return {...l, points: points.map(p => world(p, lift[l.anchor]))};
    });
    const supports = [
        ...body.map(p => ({id: p.id, y: p.xy[1] - p.extent})),
        ...limbs.flatMap((l, i) => l.points.slice(1).map((p, j) => ({id: `leg-${i}-${j}`, y: p[1] - (j === 0 ? 8 : 12)})))
    ];
    const floor = Math.min(...supports.map(point => point.y));
    const contact = supports.filter(point => point.y - floor < 2).map(point => point.id);
    return {body, limbs, floor, contact, roll};
}

const svg = 'http://www.w3.org/2000/svg';
export function initSideStudy(panel) {
    const diagram = panel.querySelector('svg'), slider = panel.querySelector('input');
    const ref = panel.querySelector('img');
    const ground = 235;
    const elem = (tag, attrs) => {
        const node = document.createElementNS(svg, tag);
        for (const [name, value] of Object.entries(attrs)) node.setAttribute(name, String(value));
        return node;
    };
    function draw() {
        const {body, limbs, floor, contact, roll} = sidePose(Number(slider.value) / 100);
        const project = ([x, y, z]) => [x + z * .22, ground - (y - floor)];
        const drawLeg = (l, index) => [
            elem('polyline', {points: l.points.map(p => project(p).join(',')).join(' '), fill: 'none',
                stroke: l.side < 0 ? '#929e9b' : '#627f6f', 'stroke-width': 13, 'stroke-linejoin': 'round', 'stroke-linecap': 'round'}),
            ...l.points.slice(1).map((p, j) => {
                const [cx, cy] = project(p);
                return elem('circle', {cx, cy, r: j === 0 ? 8 : 12,
                    fill: contact.includes(`leg-${index}-${j}`) ? '#347255' : '#bdcec3'});
            })
        ];
        diagram.replaceChildren(
            elem('path', {d: `M48 ${ground} H416`, stroke: '#a0b4a8'}),
            ...limbs.slice(0, 2).flatMap(drawLeg),
            elem('path', {d: `M${project(body[0].xy).join(' ')} L${project(body[1].xy).join(' ')} L${project(body[2].xy).join(' ')}`,
                stroke: '#d6af7e', 'stroke-width': 55, fill: 'none', 'stroke-linecap': 'round', 'stroke-linejoin': 'round'}),
            ...body.map(p => {
                const [cx, cy] = project(p.xy);
                return elem('ellipse', {cx, cy, rx: p.radius, ry: p.extent, fill: '#f1d6b2', stroke: contact.includes(p.id) ? '#347255' : '#b99062', 'stroke-width': 2});
            }),
            ...limbs.slice(2).flatMap((l, i) => drawLeg(l, i + 2))
        );
        const [cx, cy] = [404, 62], angle = Math.PI / 2 * roll;
        diagram.append(
            elem('circle', {cx, cy, r: 25, fill: '#fffdf7', stroke: '#a0b4a8'}),
            elem('line', {x1: cx, y1: cy, x2: cx + 21 * Math.sin(angle), y2: cy - 21 * Math.cos(angle),
                stroke: '#347255', 'stroke-width': 4, 'stroke-linecap': 'round'})
        );
        panel.querySelector('output').textContent = slider.value + '%';
    }
    slider.addEventListener('input', draw);
    panel.addEventListener('toggle', () => {
        if (!panel.open) return;
        if (!ref.hasAttribute('src')) ref.src = '/pet-motion/side-rest.png';
        draw();
    });
}
