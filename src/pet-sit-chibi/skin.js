import {Bone, BufferAttribute, BufferGeometry, DoubleSide, Matrix4, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3} from 'three';
import {createChibiRig, sourcePoint} from './rig.js';

export function createChibiSkin(manifest, texture) {
    const rig = createChibiRig(), meshes = [], bones = [...rig.bones], anchors = {}, index = Object.fromEntries(bones.map((bone, i) => [bone.name, i]));
    rig.apply(0); const rest = Object.fromEntries(rig.bones.map(bone => [bone.name, rig.world(bone.name)]));
    const layers = Object.fromEntries(manifest.layers.map(layer => [layer.id, layer]));
    for (const name of ['nearPaw', 'farPaw', 'nearThighArt', 'farThighArt', 'bodyPelvis', 'tailArt']) {
        const bone = new Bone(); bone.name = name; index[name] = bones.length; bones.push(bone); anchors[name] = bone; rig.group.add(bone);
    }
    function positionAnchors(progress) {
        for (const side of ['near', 'far']) {
            anchors[side + 'Paw'].position.copy(rig.world(side + 'Toe'));
            const thigh = anchors[side + 'ThighArt']; thigh.position.copy(rig.world(side + 'Hip')); rig.joints[side + 'Hip'].getWorldQuaternion(thigh.quaternion);
        }
        anchors.tailArt.position.copy(rig.world('root')).add(new Vector3(-.35, -.4, 0)); anchors.tailArt.rotation.z = -Math.PI / 3 * progress;
        anchors.bodyPelvis.position.copy(rig.world('root')).add(new Vector3(0, .5 * progress, 0)); anchors.bodyPelvis.rotation.z = Math.PI / 180 * (35 + 20 * progress);
        rig.group.updateMatrixWorld(true);
    }
    positionAnchors(0);
    const inverses = Object.fromEntries(Object.entries(anchors).map(([name, bone]) => [name, bone.matrixWorld.clone().invert()]));
    const tailJoinLocal = sourcePoint(363, 374).applyMatrix4(inverses.tailArt);
    const tailSupportLocal = layers.tail.support.map(([x, y]) => sourcePoint(x, y).applyMatrix4(inverses.tailArt));
    const skeleton = new Skeleton(bones); skeleton.calculateInverses();
    function add(id, layerId, mapping, order, tint = '#ffffff') {
        const layer = layers[layerId], cols = Math.max(8, Math.ceil(layer.width / 5)), rows = Math.max(8, Math.ceil(layer.height / 5));
        const positions = [], uv = [], ids = [], weights = [], triangles = [];
        for (let row = 0; row <= rows; row++) for (let col = 0; col <= cols; col++) {
            const x = layer.left + col / cols * layer.width, y = layer.top + row / rows * layer.height, mapped = mapping(x, y);
            positions.push(mapped.point.x, mapped.point.y, 0); uv.push((layer.atlasX + x - layer.left) / manifest.width, 1 - (layer.atlasY + y - layer.top) / manifest.height);
            for (let j = 0; j < 4; j++) {ids.push(index[mapped.weights[j]?.[0]] || 0); weights.push(mapped.weights[j]?.[1] || 0);}
            if (row < rows && col < cols) {const a = row * (cols + 1) + col; triangles.push(a, a + 1, a + cols + 1, a + 1, a + cols + 2, a + cols + 1);}
        }
        const geometry = new BufferGeometry(); geometry.setAttribute('position', new BufferAttribute(new Float32Array(positions), 3)); geometry.setAttribute('uv', new BufferAttribute(new Float32Array(uv), 2));
        geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(ids), 4)); geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4)); geometry.setIndex(triangles);
        const material = new MeshBasicMaterial({map: texture, color: tint, transparent: true, depthTest: false, depthWrite: false, side: DoubleSide, alphaTest: .015});
        const mesh = new SkinnedMesh(geometry, material); mesh.name = id; mesh.renderOrder = order; mesh.frustumCulled = false; mesh.bind(skeleton, new Matrix4()); rig.group.add(mesh); meshes.push(mesh);
    }
    const smooth = t => {t = Math.max(0, Math.min(1, t)); return t * t * (3 - 2 * t);};
    const hipWeight = x => smooth((x - 165) / 170);
    for (const side of ['far', 'near']) {
        const far = side === 'far', offset = new Vector3(far ? .31 : 0, far ? .08 : 0, 0), order = far ? 1 : 5, tint = far ? '#f4efe9' : '#ffffff';
        const rigid = name => (x, y) => ({point: sourcePoint(x, y).add(offset), weights: [[name, 1]]});
        add(side + 'Thigh', 'nearHindThigh', rigid(side + 'ThighArt'), order + .2, tint);
        add(side + 'Paw', 'nearHindPaw', rigid(side + 'Paw'), order + .1, tint);
        add(side + 'LowerCoat', 'hindLower', rigid(side + 'Hock'), order + .15, tint);
        for (const [part, from, to, widths] of [['Tibia', 'Knee', 'Hock', [.28, .15]], ['Metatarsal', 'Hock', 'Toe', [.17, .17]]]) {
            const a = rest[side + from], b = rest[side + to], direction = b.clone().sub(a).normalize(), normal = new Vector3(-direction.y, direction.x, 0);
            add(side + part, 'shaft', (x, y) => {
                const t = Math.max(0, Math.min(1, (y - 36) / 120)), width = widths[0] + (widths[1] - widths[0]) * t;
                const along = y < 36 ? (y - 36) / 36 * widths[0] / 2 : y > 156 ? a.distanceTo(b) + (y - 156) / 36 * widths[1] / 2 : a.distanceTo(b) * t;
                return {point: a.clone().addScaledVector(direction, along).addScaledVector(normal, (36 - x) * width / 72), weights: [[side + from, 1]]};
            }, order, tint);
        }
        add(side + 'Foreleg', 'nearFront', (x, y) => {
            const elbow = smooth((y - 398) / 30), wrist = smooth((y - 438) / 40);
            return {point: sourcePoint(x, y).add(new Vector3(far ? .25 : 0, far ? .07 : 0, 0)), weights: [[side + 'Shoulder', (1 - elbow) * (1 - wrist)], [side + 'Elbow', elbow * (1 - wrist)], [side + 'FrontToe', wrist]]};
        }, far ? 2 : 8, tint);
    }
    add('torso', 'torso', (x, y) => {
        const hip = hipWeight(x);
        return {point: sourcePoint(x, y), weights: [['skull', 1 - hip], ['bodyPelvis', hip]]};
    }, 4);
    add('tail', 'tail', (x, y) => ({point: sourcePoint(x, y), weights: [['tailArt', 1]]}), 0);
    add('head', 'head', (x, y) => ({point: sourcePoint(x, y), weights: [['skull', 1]]}), 10);
    const contactShifts = {};
    function apply(progress) {
        const pose = rig.apply(progress); positionAnchors(progress); const point = new Vector3(), delta = new Matrix4();
        const pelvisDelta = new Matrix4().multiplyMatrices(anchors.bodyPelvis.matrixWorld, inverses.bodyPelvis);
        const skullDelta = new Matrix4().multiplyMatrices(rig.joints.skull.matrixWorld, skeleton.boneInverses[index.skull]);
        const torsoPoint = (x, y) => sourcePoint(x, y).applyMatrix4(pelvisDelta).multiplyScalar(hipWeight(x)).addScaledVector(sourcePoint(x, y).applyMatrix4(skullDelta), 1 - hipWeight(x));
        // Seat the coat outline after the legs fold; the movement skeleton stays unchanged.
        let clearance = Infinity;
        for (const [x, y] of layers.torso.support) if (hipWeight(x) > .01) clearance = Math.min(clearance, (torsoPoint(x, y).y - .018) / hipWeight(x));
        contactShifts.bodyPelvis = -Math.max(0, clearance) * smooth((progress - .55) / .31) + Math.max(0, -clearance);
        anchors.bodyPelvis.position.y += contactShifts.bodyPelvis; rig.group.updateMatrixWorld(true);
        pelvisDelta.multiplyMatrices(anchors.bodyPelvis.matrixWorld, inverses.bodyPelvis);
        const tailJoin = torsoPoint(363, 374), tail = anchors.tailArt;
        const placeTail = angle => {
            tail.rotation.z = angle; tail.position.copy(tailJoin).sub(point.copy(tailJoinLocal).applyQuaternion(tail.quaternion));
            let bottom = Infinity;
            for (const sample of tailSupportLocal) bottom = Math.min(bottom, point.copy(sample).applyQuaternion(tail.quaternion).y + tail.position.y);
            return bottom;
        };
        const preferred = tail.rotation.z;
        if (placeTail(preferred) < .014) {
            // Rotate around the joined root rather than lifting the entire tail away from the rump.
            let supported = preferred - Math.PI / 4, blocked = preferred;
            for (let i = 0; i < 18; i++) {const angle = (supported + blocked) / 2; if (placeTail(angle) < .014) blocked = angle; else supported = angle;}
            placeTail(supported);
        }
        rig.group.updateMatrixWorld(true);
        for (const [name, layerId, offset] of [['tailArt', 'tail', [0, 0]], ['nearThighArt', 'nearHindThigh', [0, 0]], ['farThighArt', 'nearHindThigh', [.31, .08]]]) {
            delta.multiplyMatrices(anchors[name].matrixWorld, inverses[name]); let bottom = Infinity;
            for (const [x, y] of layers[layerId].support) {point.copy(sourcePoint(x, y)).add(new Vector3(...offset, 0)).applyMatrix4(delta); bottom = Math.min(bottom, point.y);}
            contactShifts[name] = Math.max(0, .006 - bottom); anchors[name].position.y += contactShifts[name];
        }
        rig.group.updateMatrixWorld(true); skeleton.update(); return pose;
    }
    apply(0); return {rig, skeleton, meshes, contactShifts, apply, dispose() {for (const mesh of meshes) {mesh.geometry.dispose(); mesh.material.dispose();} skeleton.dispose(); rig.dispose();}};
}
