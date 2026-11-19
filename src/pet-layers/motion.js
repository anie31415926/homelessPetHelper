import { Vector3 } from 'three';
import { CCDIKSolver } from 'three/addons/animation/CCDIKSolver.js';
import { bindSkin, BONE_SPEC, clamp, makeGeometry, SIZE } from '../pet-mesh/skin.js';
import { limbSpec, makeLimbGeometry } from './limbs.js';

export const UNIT = SIZE.height / 512;
export const STEP_PERIOD = 1.1;
const STANCE = .68, STRIDE = 22, TRAVEL = 65, TURN_PAUSE = .4;
const VELOCITY = 2 * STRIDE / (STANCE * STEP_PERIOD), LEG_TIME = 2 * TRAVEL / VELOCITY;
const NEAR_OFFSETS = { front: [25, 7], hind: [31, 8] };
export const MOTIONS = { idle: 4, look: 4.5, tread: STEP_PERIOD, walk: 2 * (LEG_TIME + TURN_PAUSE), sit: 6.4, side: 7, sitMesh: 6.4, lie: 5.2 };
export const FEET = [
    { id: 'farHind', kind: 'hind', side: 'far', phase: 0 },
    { id: 'farFront', kind: 'front', side: 'far', phase: .25 },
    { id: 'nearHind', kind: 'hind', side: 'near', phase: .5 },
    { id: 'nearFront', kind: 'front', side: 'near', phase: .75 },
];
const smooth = (a, b, value) => { const t = clamp((value - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); };
const cycle = (time, period) => ((time % period) + period) % period;

function walkPath(t, amount) {
    if (!amount) return { x: 0, mirror: false, gaitTime: 0, activity: 0 };
    const half = LEG_TIME + TURN_PAUSE, leg = Math.floor(t / half), local = cycle(t, half), moving = local >= TURN_PAUSE;
    const direction = leg % 2 ? 1 : -1, travelTime = Math.max(0, local - TURN_PAUSE);
    const oldDirection = -direction, facing = local < TURN_PAUSE * .5 ? oldDirection : direction;
    return { x: direction * (-TRAVEL + travelTime * VELOCITY) * amount, mirror: facing > 0,
        gaitTime: leg * LEG_TIME + travelTime, activity: moving ? smooth(0, .18, travelTime) * smooth(0, .18, LEG_TIME - travelTime) : 0 };
}
export function footStep(time, phase = 0, amount = 1) {
    const p = cycle(time / STEP_PERIOD + phase, 1), a = clamp(amount, 0, 1);
    if (p < STANCE) return { dx: (-STRIDE + 2 * STRIDE * p / STANCE) * a, lift: 0, contact: true };
    const swing = (p - STANCE) / (1 - STANCE);
    return { dx: STRIDE * Math.cos(Math.PI * swing) * a, lift: Math.sin(Math.PI * swing) * 15 * a, contact: false };
}
export function sampleMotion(time, action = 'tread', amount = .8) {
    if (!Object.hasOwn(MOTIONS, action)) throw new Error('Unknown motion');
    const t = Math.max(0, Number.isFinite(time) ? time : 0), a = clamp(amount, 0, 1), intro = smooth(0, .3, t);
    const walk = action === 'walk' ? walkPath(t, a) : { x: 0, mirror: false, gaitTime: t, activity: 1 };
    const gait = ['tread', 'walk'].includes(action) ? a * intro * walk.activity : 0;
    const lieTime = cycle(t, MOTIONS.lie), lie = action === 'lie' ? (smooth(.4, 1.6, lieTime) * (1 - smooth(3.2, 4.5, lieTime))) * a : 0;
    const down = gait * (8 + Math.sin(walk.gaitTime * Math.PI * 4 / STEP_PERIOD) * 1.5) + lie * 58;
    const look = action === 'look' ? a : 0;
    const postureTime = cycle(t, MOTIONS[action]), isPosture = action === 'sitMesh';
    const posture = isPosture ? smooth(.4, 1.8, postureTime) * (1 - smooth(MOTIONS[action] - 2, MOTIONS[action] - .5, postureTime)) * a : 0;
    const feet = Object.fromEntries(FEET.map(foot => {
        const step = footStep(walk.gaitTime, foot.phase, gait);
        const tuck = foot.kind === 'front' ? -30 : -18;
        // Retract the far limb as a whole so the held pose tucks it behind the abdomen, not through the floor.
        const tuckPhase = foot.id === 'farHind' && lie > 1e-6 ? smooth(0, .8, lie) : 0;
        const behindNearPaw = NEAR_OFFSETS.hind[0] * tuckPhase, retract = 28 * tuckPhase;
        return [foot.id, { dx: step.dx + lie * tuck + behindNearPaw, lift: step.lift, retract, contact: !retract && (!gait || step.contact) }];
    }));
    const restPhase = postureTime / MOTIONS[action] * Math.PI * 2, rest = action === 'side' || action === 'sit';
    return { time: t, action, x: walk.x, mirror: walk.mirror, down, lie, posture, feet,
        breathe: Math.sin(t * 2.1) * .007 * a,
        head: Math.sin(t * 1.4) * .10 * look + lie * .045,
        ears: Math.sin(t * 3.2) * .12 * look + lie * .06,
        tail: Math.sin(t * 2.2) * .12 * a,
        ...(rest ? { breathe: Math.sin(restPhase * 2) * .007 * a, head: Math.sin(restPhase) * (action === 'sit' ? .025 : .008) * a,
            ears: Math.sin(restPhase * 3) * .05 * a, tail: Math.sin(restPhase) * .12 * a } : {}),
    };
}

export function motionLimbSpec(kind, side) {
    if (!['near', 'far'].includes(side)) throw new Error('Unknown limb side');
    const spec = limbSpec(kind, 'copy').map(item => ({ ...item }));
    const offset = NEAR_OFFSETS[kind];
    if (side === 'near') for (const bone of spec.slice(2)) { bone.u += offset[0] / 452; bone.v += offset[1] / 512; }
    spec.push({ id: 'target', parent: 'root', u: spec[4].u, v: spec[4].v });
    return spec;
}
export function createMotionLimb(material, kind, side) {
    const offsetY = side === 'near' ? (kind === 'front' ? 7 : 8) : 0;
    const skin = bindSkin(material, makeLimbGeometry(kind, 'copy', offsetY), motionLimbSpec(kind, side));
    skin.solver = createLimbSolver(skin.mesh, kind);
    return skin;
}
export function createLimbSolver(mesh, kind, { target = 6, ankle = 4, lower = 3, upper = 2 } = {}) {
    const limit = (index, min, max) => ({ index, limitation: new Vector3(0, 0, 1), rotationMin: new Vector3(0, 0, min), rotationMax: new Vector3(0, 0, max) });
    return new CCDIKSolver(mesh, [{ target, effector: ankle, iteration: 24, maxAngle: .35,
        links: [limit(lower, kind === 'front' ? -2.5 : -.25, kind === 'front' ? .35 : 1.8), limit(upper, -1.5, 1.5)] }]);
}
export function resetSkin(skin) {
    for (const bone of Object.values(skin.bones)) { bone.position.copy(bone.userData.rest); bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1); }
}
export function applyMotionLimb(skin, pose, foot, kind) {
    resetSkin(skin);
    skin.bones.root.position.y += (foot.retract || 0) * UNIT;
    skin.bones.target.position.x += foot.dx * UNIT;
    skin.bones.target.position.y += (pose.down + foot.lift) * UNIT;
    skin.mesh.updateMatrixWorld(true);
    if (pose.down || foot.dx || foot.lift) {
        skin.bones.lower.rotation.z = kind === 'front' ? -.12 : .12;
        skin.mesh.updateMatrixWorld(true); skin.solver.update();
    }
    skin.bones.ankle.rotation.z = -skin.bones.upper.rotation.z - skin.bones.lower.rotation.z;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
export function createMotionBody(material) {
    const geometry = makeGeometry(), indices = geometry.getAttribute('skinIndex');
    for (let i = 0; i < indices.count; i++) for (let j = 0; j < 4; j++) if (indices.array[i * 4 + j] >= 8) indices.array[i * 4 + j] = 1;
    return bindSkin(material, geometry, BONE_SPEC.slice(0, 8));
}
export function applyMotionBody(skin, pose) {
    resetSkin(skin);
    const { bones } = skin;
    bones.body.scale.set(1 + pose.breathe * .4, 1 + pose.breathe, 1);
    bones.head.rotation.z = pose.head; bones.earNear.rotation.z = pose.ears; bones.earFar.rotation.z = -pose.ears * .65;
    bones.tailBase.rotation.z = pose.tail * .55 + pose.lie * .7;
    bones.tailMid.rotation.z = pose.tail + pose.lie * .7; bones.tailTip.rotation.z = pose.tail * .8 + pose.lie * .2;
    skin.mesh.updateMatrixWorld(true); skin.skeleton.update();
}
