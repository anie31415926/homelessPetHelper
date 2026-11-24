import {Bone, BufferAttribute, BufferGeometry, DoubleSide, Matrix4, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3} from 'three';
import {createChibiRig, sourcePointFor} from './rig.js';
import {applyTailExpression} from './tail.js';
import {DEFAULT_APPEARANCE, validateAtlas} from './profile.js';

export function createChibiSkin(manifest, texture, profile = DEFAULT_APPEARANCE) {
    validateAtlas(profile, manifest);
    const settings = profile.skin, contact = profile.contact, sourcePoint = sourcePointFor(profile);
    const hindOffset = profile.rig.farHindOffset, frontOffset = profile.rig.farFrontOffset;
    const rig = createChibiRig(profile), meshes = [], bones = [...rig.bones], anchors = {}, index = Object.fromEntries(bones.map((bone, i) => [bone.name, i]));
    rig.apply(0); const rest = Object.fromEntries(rig.bones.map(bone => [bone.name, rig.world(bone.name)]));
    const layers = Object.fromEntries(Object.entries(settings.layers).map(([role, id]) => [role, manifest.layers.find(layer => layer.id === id)]));
    for (const name of ['nearPaw', 'farPaw', 'nearThighArt', 'farThighArt', 'bodyPelvis', 'tailArt']) {
        const bone = new Bone(); bone.name = name; index[name] = bones.length; bones.push(bone); anchors[name] = bone; rig.group.add(bone);
    }
    function positionAnchors(progress) {
        progress = progress * progress * (3 - 2 * progress);
        for (const side of ['near', 'far']) {
            anchors[side + 'Paw'].position.copy(rig.world(side + 'Toe'));
            const thigh = anchors[side + 'ThighArt']; thigh.position.copy(rig.world(side + 'Hip')); rig.joints[side + 'Hip'].getWorldQuaternion(thigh.quaternion);
        }
        anchors.tailArt.position.copy(rig.world('root')).add(new Vector3(...settings.tailBindOffset, 0)); anchors.tailArt.rotation.z = Math.PI / 180 * settings.tailSitDegrees * progress;
        anchors.bodyPelvis.position.copy(rig.world('root')).add(new Vector3(0, settings.pelvisLift * progress, 0)); anchors.bodyPelvis.rotation.z = Math.PI / 180 * (settings.pelvisDegrees[0] + (settings.pelvisDegrees[1] - settings.pelvisDegrees[0]) * progress);
        rig.group.updateMatrixWorld(true);
    }
    positionAnchors(0);
    const inverses = Object.fromEntries(Object.entries(anchors).map(([name, bone]) => [name, bone.matrixWorld.clone().invert()]));
    const tailJoinLocal = sourcePoint(...settings.tailJoinPixels).applyMatrix4(inverses.tailArt);
    const tailSupportLocal = layers.tail.support.map(([x, y]) => sourcePoint(x, y).applyMatrix4(inverses.tailArt));
    const skeleton = new Skeleton(bones); skeleton.calculateInverses();
    function add(id, layerId, mapping, order, tint = '#ffffff') {
        const layer = layers[layerId], cols = Math.max(8, Math.ceil(layer.width / settings.meshSpacingPixels)), rows = Math.max(8, Math.ceil(layer.height / settings.meshSpacingPixels));
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
    const hipWeight = x => smooth((x - settings.hipBlendPixels[0]) / settings.hipBlendPixels[1]);
    for (const side of ['far', 'near']) {
        const far = side === 'far', offset = new Vector3(far ? hindOffset[0] : 0, far ? hindOffset[1] : 0, 0), order = far ? settings.orders.far : settings.orders.near, tint = far ? settings.farTint : settings.nearTint;
        const rigid = name => (x, y) => ({point: sourcePoint(x, y).add(offset), weights: [[name, 1]]});
        add(side + 'Thigh', 'nearHindThigh', rigid(side + 'ThighArt'), order + settings.orders.thighOffset, tint);
        add(side + 'Paw', 'nearHindPaw', rigid(side + 'Paw'), order + settings.orders.pawOffset, tint);
        add(side + 'LowerCoat', 'hindLower', rigid(side + 'Hock'), order + settings.orders.lowerCoatOffset, tint);
        for (const [part, from, to, widths] of [['Tibia', 'Knee', 'Hock', settings.tibiaWidths], ['Metatarsal', 'Hock', 'Toe', settings.metatarsalWidths]]) {
            const a = rest[side + from], b = rest[side + to], direction = b.clone().sub(a).normalize(), normal = new Vector3(-direction.y, direction.x, 0);
            add(side + part, 'shaft', (x, y) => {
                const shaft = settings.shaftPixels;
                const t = Math.max(0, Math.min(1, (y - shaft.startY) / (shaft.endY - shaft.startY))), width = widths[0] + (widths[1] - widths[0]) * t;
                const along = y < shaft.startY ? (y - shaft.startY) / shaft.capLength * widths[0] / 2 : y > shaft.endY ? a.distanceTo(b) + (y - shaft.endY) / shaft.capLength * widths[1] / 2 : a.distanceTo(b) * t;
                return {point: a.clone().addScaledVector(direction, along).addScaledVector(normal, (shaft.centerX - x) * width / shaft.width), weights: [[side + from, 1]]};
            }, order, tint);
        }
        add(side + 'Foreleg', 'nearFront', (x, y) => {
            const elbow = smooth((y - settings.elbowBlendPixels[0]) / settings.elbowBlendPixels[1]), wrist = smooth((y - settings.wristBlendPixels[0]) / settings.wristBlendPixels[1]);
            return {point: sourcePoint(x, y).add(new Vector3(far ? frontOffset[0] : 0, far ? frontOffset[1] : 0, 0)), weights: [[side + 'Shoulder', (1 - elbow) * (1 - wrist)], [side + 'Elbow', elbow * (1 - wrist)], [side + 'FrontToe', wrist]]};
        }, far ? settings.orders.farForeleg : settings.orders.nearForeleg, tint);
    }
    add('torso', 'torso', (x, y) => {
        const hip = hipWeight(x);
        return {point: sourcePoint(x, y), weights: [['skull', 1 - hip], ['bodyPelvis', hip]]};
    }, settings.orders.torso);
    add('tail', 'tail', (x, y) => ({point: sourcePoint(x, y), weights: [['tailArt', 1]]}), settings.orders.tail);
    add('head', 'head', (x, y) => ({point: sourcePoint(x, y), weights: [['skull', 1]]}), settings.orders.head);
    const contactShifts = {};
    function apply(progress, phase = 0, gait = 0, expression = null, seconds = 0) {
        const pose = rig.apply(progress, phase, gait); positionAnchors(progress); const point = new Vector3(), delta = new Matrix4();
        const pelvisDelta = new Matrix4().multiplyMatrices(anchors.bodyPelvis.matrixWorld, inverses.bodyPelvis);
        const skullDelta = new Matrix4().multiplyMatrices(rig.joints.skull.matrixWorld, skeleton.boneInverses[index.skull]);
        const torsoPoint = (x, y) => sourcePoint(x, y).applyMatrix4(pelvisDelta).multiplyScalar(hipWeight(x)).addScaledVector(sourcePoint(x, y).applyMatrix4(skullDelta), 1 - hipWeight(x));
        // Seat the coat outline after the legs fold; the movement skeleton stays unchanged.
        let clearance = Infinity;
        for (const [x, y] of layers.torso.support) if (hipWeight(x) > .01) clearance = Math.min(clearance, (torsoPoint(x, y).y - contact.bodyFloor) / hipWeight(x));
        contactShifts.bodyPelvis = -Math.max(0, clearance) * smooth((progress - contact.bodyBlendStart) / contact.bodyBlendDuration) + Math.max(0, -clearance);
        anchors.bodyPelvis.position.y += contactShifts.bodyPelvis; rig.group.updateMatrixWorld(true);
        pelvisDelta.multiplyMatrices(anchors.bodyPelvis.matrixWorld, inverses.bodyPelvis);
        const tailJoin = torsoPoint(...settings.tailJoinPixels), tail = anchors.tailArt;
        const placeTail = angle => {
            tail.rotation.z = angle; tail.position.copy(tailJoin).sub(point.copy(tailJoinLocal).applyQuaternion(tail.quaternion));
            let bottom = Infinity;
            for (const sample of tailSupportLocal) bottom = Math.min(bottom, point.copy(sample).applyQuaternion(tail.quaternion).y + tail.position.y);
            return bottom;
        };
        const preferred = tail.rotation.z;
        if (placeTail(preferred) < contact.tailFloor) {
            // Rotate around the joined root rather than lifting the entire tail away from the rump.
            let supported = preferred - Math.PI / 180 * contact.tailCorrectionDegrees, blocked = preferred;
            for (let i = 0; i < 18; i++) {const angle = (supported + blocked) / 2; if (placeTail(angle) < contact.tailFloor) blocked = angle; else supported = angle;}
            placeTail(supported);
        }
        rig.group.updateMatrixWorld(true);
        for (const [name, layerId, offset] of [['tailArt', 'tail', [0, 0]], ['nearThighArt', 'nearHindThigh', [0, 0]], ['farThighArt', 'nearHindThigh', hindOffset]]) {
            delta.multiplyMatrices(anchors[name].matrixWorld, inverses[name]); let bottom = Infinity;
            for (const [x, y] of layers[layerId].support) {point.copy(sourcePoint(x, y)).add(new Vector3(...offset, 0)).applyMatrix4(delta); bottom = Math.min(bottom, point.y);}
            contactShifts[name] = Math.max(0, contact.coatFloor - bottom); anchors[name].position.y += contactShifts[name];
        }
        if (expression) applyTailExpression(anchors.tailArt, tailJoinLocal, expression.raise, expression.swish, seconds, profile.tail);
        rig.group.updateMatrixWorld(true); skeleton.update(); return pose;
    }
    apply(0); return {rig, skeleton, meshes, contactShifts, tailJoinLocal, apply, dispose() {for (const mesh of meshes) {mesh.geometry.dispose(); mesh.material.dispose();} skeleton.dispose(); rig.dispose();}};
}
