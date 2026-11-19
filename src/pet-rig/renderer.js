import { createRig, poseAt, PROPORTIONS, transformPoint } from './rig.js';

export const STAGE = { width: 640, height: 540 };

export async function loadArt() {
    const response = await fetch('/pet-rig/atlas.json');
    if (!response.ok) throw new Error('分件配置加载失败');
    const atlas = await response.json();
    const images = {};
    for (const view of ['front', 'side']) {
        const image = new Image();
        image.src = `/pet-rig/${view}-atlas.png`;
        await image.decode();
        images[view] = image;
    }
    return { atlas, images };
}

function drawPart(ctx, image, source, node) {
    ctx.drawImage(image, source.x, source.y, source.width, source.height,
        -node.width * node.ox, -node.height * node.oy, node.width, node.height);
}

export function renderRig(ctx, art, state, time) {
    const { width: w, height: h } = STAGE;
    const ratio = ctx.canvas.width / w;
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    ctx.clearRect(0, 0, w, h);
    const { view, action, manual, proportion, bones, exploded } = state;
    const pose = poseAt(time, action, manual), nodes = createRig(view, proportion);
    const leg = PROPORTIONS[proportion].leg;
    ctx.fillStyle = 'rgba(67,88,77,0.08)';
    ctx.beginPath(); ctx.ellipse(320, 470, view === 'side' ? 163 : 102, 12, 0, 0, Math.PI * 2); ctx.fill();
    const transforms = {}, joints = {};
    ctx.save();
    ctx.translate(320, 470 - 57 - 106 * leg * .88 + pose.bodyY);
    const root = ctx.getTransform();
    for (const node of nodes) {
        ctx.setTransform(node.parent === 'head' ? transforms.head : root);
        const angle = pose[node.id] || 0;
        let y = node.y;
        if (node.id.startsWith('front') || node.id.startsWith('back')) {
            const lifted = ['frontL', 'backR'].includes(node.id) ? pose.lift : pose.oppositeLift;
            y -= lifted;
            if (action === 'crouch') y -= 15;
        }
        let dx = 0, dy = 0;
        if (exploded) {
            const offsets = { head: [0, -32], earL: [-17, -27], earR: [17, -27], tail: [40, -8], frontL: [-25, 12], frontR: [25, 12], backL: [-32, 12], backR: [32, 12] };
            [dx, dy] = offsets[node.id] || [0, 0];
        }
        ctx.translate(node.x + dx, y + dy);
        ctx.rotate((angle + (node.id === 'tail' && view === 'side' ? -42 : 0)) * Math.PI / 180);
        transforms[node.id] = ctx.getTransform();
        joints[node.id] = transformPoint(ctx.getTransform(), 0, 0);
        ctx.save();
        if (node.id === 'body') ctx.scale(1, pose.bodyScaleY);
        if (['frontL', 'backL'].includes(node.id) && view === 'side') ctx.globalAlpha = .88;
        drawPart(ctx, art.images[view], art.atlas.views[view][node.id], node);
        ctx.restore();
    }
    ctx.restore();
    if (bones) drawBones(ctx, nodes, joints, ratio, root);
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    return { joints: Object.fromEntries(Object.entries(joints).map(([id, p]) => [id, { x: p.x / ratio, y: p.y / ratio }])), pose };
}

function drawBones(ctx, nodes, joints, ratio, root) {
    ctx.save();
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.strokeStyle = '#467963'; ctx.fillStyle = '#fff'; ctx.lineWidth = 1.5 * ratio;
    for (const node of nodes) {
        const parent = node.parent === 'head' ? joints.head : { x: root.e, y: root.f };
        const joint = joints[node.id];
        ctx.beginPath(); ctx.moveTo(parent.x, parent.y); ctx.lineTo(joint.x, joint.y); ctx.stroke();
        ctx.beginPath(); ctx.arc(joint.x, joint.y, 4 * ratio, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    }
    ctx.restore();
}
