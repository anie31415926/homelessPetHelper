import {Bone, BufferGeometry, Group, MathUtils, Skeleton, SkinnedMesh, MeshBasicMaterial, Vector3} from 'three';
import {CCDIKSolver} from 'three/addons/animation/CCDIKSolver.js';

export const DURATION = 3.6;
export const LENGTHS = Object.freeze({femur: .95, tibia: 1.1, metatarsal: .8, humerus: 1, radius: 1.03, palm: .3, spine: .45, neck: .47});
export const KEY_POSES = Object.freeze([
    {at: 0, name: '站立', hip: [-.95, 2.15], spine: [35, 15, 0, -5, -10, -15], metatarsal: -65, frontX: 1.55, pelvis: -12},
    {at: .2, name: '预备', hip: [-1.02, 2.02], spine: [40, 20, 5, 0, -8, -10], metatarsal: -56, frontX: 1.28, pelvis: -18},
    {at: .55, name: '下降', hip: [-1.17, 1.25], spine: [60, 40, 25, 15, 5, 0], metatarsal: -28, frontX: 1.28, pelvis: -30},
    {at: .86, name: '接触', hip: [-1.23, .49], spine: [79, 69, 54, 44, 29, 15], metatarsal: -6, frontX: 1.28, pelvis: -43},
    {at: 1, name: '坐稳', hip: [-1.2, .45], spine: [80, 70, 55, 45, 30, 15], metatarsal: -5, frontX: 1.28, pelvis: -45},
]);
const radians = MathUtils.degToRad;
const mix = MathUtils.lerp;
const ease = t => t * t * (3 - 2 * t);

export function samplePose(progress) {
    if (!Number.isFinite(progress) || progress < 0 || progress > 1) throw new RangeError('Posture must be between 0 and 1');
    const end = KEY_POSES.findIndex(pose => pose.at >= progress);
    const b = KEY_POSES[Math.max(1, end)], a = KEY_POSES[Math.max(0, end - 1)];
    const t = ease((progress - a.at) / (b.at - a.at));
    const result = {progress, name: KEY_POSES.reduce((best, pose) => Math.abs(pose.at - progress) < Math.abs(best.at - progress) ? pose : best).name};
    for (const key of ['metatarsal', 'pelvis']) result[key] = mix(a[key], b[key], t);
    result.hip = a.hip.map((value, i) => mix(value, b.hip[i], t));
    result.spine = a.spine.map((value, i) => radians(mix(value, b.spine[i], t)));
    // Alternating preparation steps leave the other forepaw supporting the chest.
    result.front = Object.fromEntries(['near', 'far'].map(side => {
        const step = MathUtils.clamp((progress - (side === 'far' ? .1 : 0)) / .1, 0, 1);
        return [side, {x: mix(KEY_POSES[0].frontX, KEY_POSES[1].frontX, ease(step)), lift: .1 * Math.sin(Math.PI * step) ** 2}];
    }));
    return result;
}

export function createSitRig() {
    const group = new Group(), bones = [], joints = {}, segments = [], chains = [];
    function joint(name, parent, position = [0, 0, 0]) {
        const bone = new Bone(); bone.name = name; bone.position.set(...position);
        (parent || group).add(bone); bones.push(bone); joints[name] = bone; return bone;
    }
    function segment(name, parent, length) {
        const bone = joint(name, parent, [length, 0, 0]);
        segments.push({from: parent.name, to: name, length}); return bone;
    }
    const root = joint('root', null), pelvis = joint('pelvis', root);
    let previous = root;
    for (let i = 0; i < 6; i++) previous = segment(`spine${i}`, previous, LENGTHS.spine);
    const neck = segment('neck', previous, LENGTHS.neck);
    const skull = joint('skull', neck);
    for (const side of ['far', 'near']) {
        const hip = joint(`${side}Hip`, root, [0, 0, side === 'near' ? .22 : -.22]);
        const knee = segment(`${side}Knee`, hip, LENGTHS.femur);
        const hock = segment(`${side}Hock`, knee, LENGTHS.tibia);
        segment(`${side}Toe`, hock, LENGTHS.metatarsal);
        const shoulder = joint(`${side}Shoulder`, null);
        const elbow = segment(`${side}Elbow`, shoulder, LENGTHS.humerus);
        const wrist = segment(`${side}Wrist`, elbow, LENGTHS.radius);
        segment(`${side}FrontToe`, wrist, LENGTHS.palm);
        for (const [effector, links] of [[hock, [knee, hip]], [wrist, [elbow, shoulder]]]) {
            const target = joint(`${effector.name}Target`, null);
            const front = effector === wrist;
            chains.push({target: bones.indexOf(target), effector: bones.indexOf(effector), iteration: 260, maxAngle: .2,
                links: links.map((bone, i) => ({index: bones.indexOf(bone), limitation: new Vector3(0, 0, 1),
                    rotationMin: new Vector3(0, 0, i === 0 ? (front ? .06 : -3.1) : -3.1),
                    rotationMax: new Vector3(0, 0, i === 0 ? (front ? 2.7 : -.06) : 1.6)}))});
        }
    }
    group.updateMatrixWorld(true);
    const skeleton = new Skeleton(bones);
    const mesh = new SkinnedMesh(new BufferGeometry(), new MeshBasicMaterial());
    mesh.bind(skeleton); mesh.visible = false; group.add(mesh);
    const solver = new CCDIKSolver(mesh, chains);
    const world = name => joints[name].getWorldPosition(new Vector3());
    function apply(progress) {
        const pose = samplePose(progress);
        for (const bone of bones) { bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1); }
        root.position.set(...pose.hip, 0); pelvis.rotation.z = radians(pose.pelvis) - pose.spine[0];
        root.rotation.z = pose.spine[0];
        for (let i = 0; i < 5; i++) joints[`spine${i}`].rotation.z = pose.spine[i + 1] - pose.spine[i];
        joints.spine5.rotation.z = radians(mix(38, 65, progress)) - pose.spine[5];
        skull.rotation.z = radians(mix(5, 18, progress)) - radians(mix(38, 65, progress));
        group.updateMatrixWorld(true);
        const chest = world('spine5');
        for (const side of ['far', 'near']) {
            const far = side === 'far', z = far ? -.22 : .22, offset = far ? -.13 : 0;
            const hip = joints[`${side}Hip`];
            hip.position.set(offset * Math.cos(root.rotation.z), -offset * Math.sin(root.rotation.z), z);
            hip.rotation.z = mix(-.65, .65, progress) - root.rotation.z;
            joints[`${side}Knee`].rotation.z = mix(-1.5, -2.8, progress);
            // The toe is the ground contact; lowering the hock rotates the intact metatarsal.
            const angle = radians(pose.metatarsal), toe = new Vector3(-.3 + offset, .075, z);
            joints[`${side}HockTarget`].position.copy(toe).sub(new Vector3(Math.cos(angle), Math.sin(angle), 0).multiplyScalar(LENGTHS.metatarsal));
            const shoulder = joints[`${side}Shoulder`];
            const scapula = ease(Math.max(0, (progress - .55) / .45));
            shoulder.position.copy(chest).add(new Vector3(mix(.18, .45, scapula) + offset, mix(-.2, -.08, scapula), z));
            shoulder.rotation.z = -2;
            joints[`${side}Elbow`].rotation.z = .7;
            const footAngle = radians(-42), frontToe = new Vector3(pose.front[side].x + offset, .075 + pose.front[side].lift, z);
            joints[`${side}WristTarget`].position.copy(frontToe).sub(new Vector3(Math.cos(footAngle), Math.sin(footAngle), 0).multiplyScalar(LENGTHS.palm));
        }
        group.updateMatrixWorld(true); solver.update();
        for (const side of ['far', 'near']) {
            const hipAngle = root.rotation.z + joints[`${side}Hip`].rotation.z;
            joints[`${side}Hock`].rotation.z = radians(pose.metatarsal) - hipAngle - joints[`${side}Knee`].rotation.z;
            joints[`${side}Wrist`].rotation.z = radians(-42) - joints[`${side}Shoulder`].rotation.z - joints[`${side}Elbow`].rotation.z;
        }
        group.updateMatrixWorld(true); skeleton.update(); return pose;
    }
    apply(0);
    return {group, bones, joints, segments, world, apply, dispose() {mesh.geometry.dispose(); mesh.material.dispose(); skeleton.dispose();}};
}
