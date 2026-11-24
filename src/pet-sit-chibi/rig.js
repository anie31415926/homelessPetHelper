import {Bone, BufferGeometry, Group, MathUtils, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3} from 'three';
import {CCDIKSolver} from 'three/addons/animation/CCDIKSolver.js';
import {DEFAULT_APPEARANCE} from './profile.js';
import {sampleChannel} from './curves.js';

export const KEY_POSES = DEFAULT_APPEARANCE.sit.keys;
export const DURATION = DEFAULT_APPEARANCE.sit.seconds;
export const LANDMARKS = DEFAULT_APPEARANCE.landmarks;
export const sourcePointFor = profile => (x, y) => new Vector3(
    (profile.source.origin[0] - x) / profile.source.pixelsPerUnit,
    (profile.source.origin[1] - y) / profile.source.pixelsPerUnit, 0);
export const sourcePoint = sourcePointFor(DEFAULT_APPEARANCE);
function geometry(profile) {
    const point = sourcePointFor(profile);
    const points = Object.fromEntries(Object.entries(profile.landmarks).map(([name, xy]) => [name, point(...xy)]));
    const lengths = {femur: points.hip.distanceTo(points.knee), tibia: points.knee.distanceTo(points.hock), metatarsal: points.hock.distanceTo(points.toe), humerus: points.shoulder.distanceTo(points.elbow), radius: points.elbow.distanceTo(points.wrist), palm: points.wrist.distanceTo(points.frontToe), spine: profile.rig.spineLength};
    if (Object.values(lengths).some(length => length <= 0)) throw new Error('Animal appearance contains a zero-length limb');
    return {points, lengths};
}
export const LENGTHS = Object.freeze(geometry(DEFAULT_APPEARANCE).lengths);
const angle = (from, to) => Math.atan2(to.y - from.y, to.x - from.x);
const degrees = MathUtils.degToRad, mix = MathUtils.lerp, ease = t => t * t * (3 - 2 * t);

export function createChibiRig(profile = DEFAULT_APPEARANCE) {
    const {points, lengths} = geometry(profile), settings = profile.rig, keys = profile.sit.keys;
    const times = keys.map(key => key.at), channel = (progress, values) => sampleChannel(progress, values, times);
    const hipKeys = keys.map(key => key.hip);
    const hockAngles = keys.map((key, i) => i === 0 ? angle(points.hock, points.toe) : degrees(key.hockDegrees));
    const stepSettings = profile.sit.frontStep, walk = profile.walk;
    const group = new Group(), bones = [], joints = {}, segments = [], iks = [];
    function joint(name, parent, position = [0, 0, 0]) {
        const bone = new Bone(); bone.name = name; bone.position.set(...position); (parent || group).add(bone); bones.push(bone); joints[name] = bone; return bone;
    }
    function segment(name, parent, length) {const bone = joint(name, parent, [length, 0, 0]); segments.push({from: parent.name, to: name, length}); return bone;}
    const root = joint('root', null), pelvis = joint('pelvis', root); let spine = root;
    for (let i = 0; i < 6; i++) spine = segment(`spine${i}`, spine, lengths.spine);
    const initial = keys[0].spine.map(degrees), initialChest = points.hip.clone();
    initial.forEach(rotation => initialChest.add(new Vector3(Math.cos(rotation), Math.sin(rotation), 0).multiplyScalar(lengths.spine)));
    const neckLength = initialChest.distanceTo(points.skull), neckAngle = angle(initialChest, points.skull);
    segment('neck', spine, neckLength); joint('skull', joints.neck);
    for (const side of ['far', 'near']) {
        const hip = joint(side + 'Hip', root), knee = segment(side + 'Knee', hip, lengths.femur), hock = segment(side + 'Hock', knee, lengths.tibia);
        segment(side + 'Toe', hock, lengths.metatarsal);
        const shoulder = joint(side + 'Shoulder', null), elbow = segment(side + 'Elbow', shoulder, lengths.humerus), wrist = segment(side + 'Wrist', elbow, lengths.radius);
        segment(side + 'FrontToe', wrist, lengths.palm);
        for (const [effector, links, front] of [[hock, [knee, hip], false], [wrist, [elbow, shoulder], true]]) {
            const target = joint(effector.name + 'Target', null);
            iks.push({target: bones.indexOf(target), effector: bones.indexOf(effector), iteration: 480, maxAngle: .18,
                links: links.map((bone, i) => ({index: bones.indexOf(bone), limitation: new Vector3(0, 0, 1), rotationMin: new Vector3(0, 0, i === 0 ? (front ? settings.jointLimits.frontBend[0] : settings.jointLimits.hindBend[0]) : settings.jointLimits.base[0]), rotationMax: new Vector3(0, 0, i === 0 ? (front ? settings.jointLimits.frontBend[1] : settings.jointLimits.hindBend[1]) : settings.jointLimits.base[1])}))});
        }
    }
    group.updateMatrixWorld(true); const skeleton = new Skeleton(bones), mesh = new SkinnedMesh(new BufferGeometry(), new MeshBasicMaterial()); mesh.bind(skeleton); mesh.visible = false; group.add(mesh);
    const solver = new CCDIKSolver(mesh, iks), world = name => joints[name].getWorldPosition(new Vector3());
    const shoulderOffset = points.shoulder.clone().sub(initialChest), palmAngle = angle(points.wrist, points.frontToe);
    function apply(progress, phase = 0, gait = 0) {
        if (!Number.isFinite(progress) || progress < 0 || progress > 1) throw new RangeError('Posture must be between 0 and 1');
        const settled = ease(progress), nearest = keys.reduce((a, b) => Math.abs(a.at - progress) <= Math.abs(b.at - progress) ? a : b);
        const pose = {progress, name: nearest.name, spine: keys[0].spine.map((_, i) => degrees(channel(progress, keys.map(key => key.spine[i]))))};
        for (const bone of bones) {bone.rotation.set(0, 0, 0); bone.scale.set(1, 1, 1);}
        root.position.set(channel(progress, hipKeys.map(key => key[0])), channel(progress, hipKeys.map(key => key[1])), 0); root.rotation.z = pose.spine[0];
        root.position.y += walk.bodyBob * (1 - Math.cos(phase * 2)) * gait;
        pelvis.rotation.z = degrees(mix(...settings.pelvisDegrees, settled)) - root.rotation.z;
        for (let i = 0; i < 5; i++) joints[`spine${i}`].rotation.z = pose.spine[i + 1] - pose.spine[i];
        const neckRotation = neckAngle - degrees(settings.neckDropDegrees) * settled;
        joints.spine5.rotation.z = neckRotation - pose.spine[5]; joints.skull.rotation.z = degrees(settings.skullRaiseDegrees) * settled - neckRotation;
        group.updateMatrixWorld(true); const chest = world('spine5'), metatarsal = channel(progress, hockAngles);
        for (const side of ['far', 'near']) {
            const far = side === 'far', dx = far ? settings.farHindOffset[0] : 0, dy = far ? settings.farHindOffset[1] : 0, z = (far ? -1 : 1) * settings.sideDepth;
            joints[side + 'Hip'].position.set(dx * Math.cos(root.rotation.z) + dy * Math.sin(root.rotation.z), -dx * Math.sin(root.rotation.z) + dy * Math.cos(root.rotation.z), z);
            joints[side + 'Hip'].rotation.z = mix(angle(points.hip, points.knee), settings.foldHipRadians, progress) - root.rotation.z;
            joints[side + 'Knee'].rotation.z = mix(angle(points.knee, points.hock) - angle(points.hip, points.knee), settings.foldKneeRadians, progress);
            const toe = points.toe.clone().add(new Vector3(dx, dy, z));
            const hindPhase = phase + (far ? Math.PI : 0), frontPhase = hindPhase + Math.PI;
            toe.x += walk.stride * Math.sin(hindPhase) * gait;
            toe.y += walk.lift * Math.max(0, Math.sin(hindPhase)) ** 2 * gait;
            joints[side + 'HockTarget'].position.copy(toe).sub(new Vector3(Math.cos(metatarsal), Math.sin(metatarsal), 0).multiplyScalar(lengths.metatarsal));
            const settle = ease(progress), shoulder = joints[side + 'Shoulder'];
            shoulder.position.copy(chest).add(shoulderOffset).add(new Vector3(profile.sit.shoulderShift[0] * settle + (far ? settings.farFrontOffset[0] : 0), profile.sit.shoulderShift[1] * settle + (far ? settings.farFrontOffset[1] : 0), z));
            shoulder.rotation.z = angle(points.shoulder, points.elbow); joints[side + 'Elbow'].rotation.z = angle(points.elbow, points.wrist) - shoulder.rotation.z;
            const step = MathUtils.clamp((progress - (far ? stepSettings.farDelay : 0)) / stepSettings.duration, 0, 1), frontToe = points.frontToe.clone();
            frontToe.x -= stepSettings.distance * ease(step); frontToe.y += stepSettings.lift * Math.sin(Math.PI * step) ** 2;
            frontToe.x += walk.stride * Math.sin(frontPhase) * gait;
            frontToe.y += walk.lift * Math.max(0, Math.sin(frontPhase)) ** 2 * gait;
            frontToe.add(new Vector3(far ? settings.farFrontOffset[0] : 0, far ? settings.farFrontOffset[1] : 0, z));
            joints[side + 'WristTarget'].position.copy(frontToe).sub(new Vector3(Math.cos(palmAngle), Math.sin(palmAngle), 0).multiplyScalar(lengths.palm));
        }
        group.updateMatrixWorld(true); solver.update();
        for (const side of ['far', 'near']) {
            joints[side + 'Hock'].rotation.z = metatarsal - root.rotation.z - joints[side + 'Hip'].rotation.z - joints[side + 'Knee'].rotation.z;
            joints[side + 'Wrist'].rotation.z = palmAngle - joints[side + 'Shoulder'].rotation.z - joints[side + 'Elbow'].rotation.z;
        }
        group.updateMatrixWorld(true); skeleton.update(); return pose;
    }
    apply(0); return {group, bones, joints, segments, world, apply, profile, lengths, dispose() {mesh.geometry.dispose(); mesh.material.dispose(); skeleton.dispose();}};
}
