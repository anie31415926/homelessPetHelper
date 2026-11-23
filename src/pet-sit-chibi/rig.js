import {Bone, BufferGeometry, Group, MathUtils, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3} from 'three';
import {CCDIKSolver} from 'three/addons/animation/CCDIKSolver.js';
import {DURATION, KEY_POSES, samplePose} from '../pet-sit-study/rig.js';

export {DURATION, KEY_POSES};
export const sourcePoint = (x, y) => new Vector3((226 - x) / 100, (496 - y) / 100, 0);
export const LANDMARKS = Object.freeze({hip: [337, 350], knee: [280, 389], hock: [330, 449], toe: [291, 477], shoulder: [145, 355], elbow: [150, 413], wrist: [137, 465], frontToe: [134, 487], skull: [153, 275]});
const points = Object.fromEntries(Object.entries(LANDMARKS).map(([name, point]) => [name, sourcePoint(...point)]));
export const LENGTHS = Object.freeze({femur: points.hip.distanceTo(points.knee), tibia: points.knee.distanceTo(points.hock), metatarsal: points.hock.distanceTo(points.toe), humerus: points.shoulder.distanceTo(points.elbow), radius: points.elbow.distanceTo(points.wrist), palm: points.wrist.distanceTo(points.frontToe), spine: .28});
const angle = (from, to) => Math.atan2(to.y - from.y, to.x - from.x);
const degrees = MathUtils.degToRad, mix = MathUtils.lerp, ease = t => t * t * (3 - 2 * t);
const hipKeys = [[-1.11, 1.46], [-1.14, 1.38], [-1.18, .96], [-1.2, .46], [-1.17, .42]];
const shoulderDrop = [0, .04, 0, .035, 0];
const hockAngles = [angle(points.hock, points.toe), degrees(-30), degrees(-17), degrees(-6), degrees(-5)];

export function createChibiRig() {
    const group = new Group(), bones = [], joints = {}, segments = [], iks = [];
    function joint(name, parent, position = [0, 0, 0]) {
        const bone = new Bone(); bone.name = name; bone.position.set(...position); (parent || group).add(bone); bones.push(bone); joints[name] = bone; return bone;
    }
    function segment(name, parent, length) {const bone = joint(name, parent, [length, 0, 0]); segments.push({from: parent.name, to: name, length}); return bone;}
    const root = joint('root', null), pelvis = joint('pelvis', root); let spine = root;
    for (let i = 0; i < 6; i++) spine = segment(`spine${i}`, spine, LENGTHS.spine);
    const initial = samplePose(0).spine, initialChest = points.hip.clone();
    initial.forEach(rotation => initialChest.add(new Vector3(Math.cos(rotation), Math.sin(rotation), 0).multiplyScalar(LENGTHS.spine)));
    const neckLength = initialChest.distanceTo(points.skull), neckAngle = angle(initialChest, points.skull);
    segment('neck', spine, neckLength); joint('skull', joints.neck);
    for (const side of ['far', 'near']) {
        const hip = joint(side + 'Hip', root), knee = segment(side + 'Knee', hip, LENGTHS.femur), hock = segment(side + 'Hock', knee, LENGTHS.tibia);
        segment(side + 'Toe', hock, LENGTHS.metatarsal);
        const shoulder = joint(side + 'Shoulder', null), elbow = segment(side + 'Elbow', shoulder, LENGTHS.humerus), wrist = segment(side + 'Wrist', elbow, LENGTHS.radius);
        segment(side + 'FrontToe', wrist, LENGTHS.palm);
        for (const [effector, links, front] of [[hock, [knee, hip], false], [wrist, [elbow, shoulder], true]]) {
            const target = joint(effector.name + 'Target', null);
            iks.push({target: bones.indexOf(target), effector: bones.indexOf(effector), iteration: 320, maxAngle: .18,
                links: links.map((bone, i) => ({index: bones.indexOf(bone), limitation: new Vector3(0, 0, 1), rotationMin: new Vector3(0, 0, i === 0 ? (front ? .025 : -3.12) : -3.12), rotationMax: new Vector3(0, 0, i === 0 ? (front ? 2.7 : -.025) : 2.7)}))});
        }
    }
    group.updateMatrixWorld(true); const skeleton = new Skeleton(bones), mesh = new SkinnedMesh(new BufferGeometry(), new MeshBasicMaterial()); mesh.bind(skeleton); mesh.visible = false; group.add(mesh);
    const solver = new CCDIKSolver(mesh, iks), world = name => joints[name].getWorldPosition(new Vector3());
    const shoulderOffset = points.shoulder.clone().sub(initialChest), palmAngle = angle(points.wrist, points.frontToe);
    function apply(progress) {
        const pose = samplePose(progress), end = Math.max(1, KEY_POSES.findIndex(key => key.at >= progress)), start = end - 1;
        const t = ease((progress - KEY_POSES[start].at) / (KEY_POSES[end].at - KEY_POSES[start].at));
        for (const bone of bones) {bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1);}
        root.position.set(mix(hipKeys[start][0], hipKeys[end][0], t), mix(hipKeys[start][1], hipKeys[end][1], t), 0); root.rotation.z = pose.spine[0];
        pelvis.rotation.z = degrees(mix(-12, -45, progress)) - root.rotation.z;
        for (let i = 0; i < 5; i++) joints[`spine${i}`].rotation.z = pose.spine[i + 1] - pose.spine[i];
        const neckRotation = neckAngle - degrees(20) * progress;
        joints.spine5.rotation.z = neckRotation - pose.spine[5]; joints.skull.rotation.z = degrees(7) * progress - neckRotation;
        group.updateMatrixWorld(true); const chest = world('spine5'), metatarsal = mix(hockAngles[start], hockAngles[end], t);
        for (const side of ['far', 'near']) {
            const far = side === 'far', dx = far ? .31 : 0, dy = far ? .08 : 0, z = far ? -.2 : .2;
            joints[side + 'Hip'].position.set(dx * Math.cos(root.rotation.z) + dy * Math.sin(root.rotation.z), -dx * Math.sin(root.rotation.z) + dy * Math.cos(root.rotation.z), z);
            joints[side + 'Hip'].rotation.z = mix(angle(points.hip, points.knee), .6, progress) - root.rotation.z;
            joints[side + 'Knee'].rotation.z = mix(angle(points.knee, points.hock) - angle(points.hip, points.knee), -2.8, progress);
            const toe = points.toe.clone().add(new Vector3(dx, dy, z));
            joints[side + 'HockTarget'].position.copy(toe).sub(new Vector3(Math.cos(metatarsal), Math.sin(metatarsal), 0).multiplyScalar(LENGTHS.metatarsal));
            const settle = ease(progress), shoulder = joints[side + 'Shoulder'];
            shoulder.position.copy(chest).add(shoulderOffset).add(new Vector3(.18 * settle + (far ? .25 : 0), -.11 * settle - mix(shoulderDrop[start], shoulderDrop[end], t) + (far ? .07 : 0), z));
            shoulder.rotation.z = angle(points.shoulder, points.elbow); joints[side + 'Elbow'].rotation.z = angle(points.elbow, points.wrist) - shoulder.rotation.z;
            const step = MathUtils.clamp((progress - (far ? .1 : 0)) / .1, 0, 1), frontToe = points.frontToe.clone();
            frontToe.x -= .2 * ease(step); frontToe.y += .035 * Math.sin(Math.PI * step) ** 2;
            frontToe.add(new Vector3(far ? .25 : 0, far ? .07 : 0, z));
            joints[side + 'WristTarget'].position.copy(frontToe).sub(new Vector3(Math.cos(palmAngle), Math.sin(palmAngle), 0).multiplyScalar(LENGTHS.palm));
        }
        group.updateMatrixWorld(true); solver.update();
        for (const side of ['far', 'near']) {
            joints[side + 'Hock'].rotation.z = metatarsal - root.rotation.z - joints[side + 'Hip'].rotation.z - joints[side + 'Knee'].rotation.z;
            joints[side + 'Wrist'].rotation.z = palmAngle - joints[side + 'Shoulder'].rotation.z - joints[side + 'Elbow'].rotation.z;
        }
        group.updateMatrixWorld(true); skeleton.update(); return pose;
    }
    apply(0); return {group, bones, joints, segments, world, apply, dispose() {mesh.geometry.dispose(); mesh.material.dispose(); skeleton.dispose();}};
}
