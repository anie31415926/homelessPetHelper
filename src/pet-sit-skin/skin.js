import {Bone, BufferAttribute, BufferGeometry, DoubleSide, Matrix4, MeshBasicMaterial, Skeleton, SkinnedMesh, Vector3} from 'three';
import {createSitRig} from '../pet-sit-study/rig.js';

export function createSkin(manifest, texture) {
    const rig = createSitRig(), meshes = [], anchors = {}, lookup = Object.fromEntries(rig.bones.map((bone, i) => [bone.name, i]));
    rig.apply(0);
    const rest = Object.fromEntries(rig.bones.map(bone => [bone.name, rig.world(bone.name)]));
    const bones = [...rig.bones];
    for (const name of ['nearPaw', 'farPaw', 'tailArt']) {
        const bone = new Bone(); bone.name = name; lookup[name] = bones.length; bones.push(bone); anchors[name] = bone; rig.group.add(bone);
    }
    function positionAnchors(progress) {
        for (const side of ['near', 'far']) anchors[side + 'Paw'].position.copy(rig.world(side + 'Toe'));
        anchors.tailArt.position.copy(rig.world('root')).add(new Vector3(.10, 0, 0));
        anchors.tailArt.rotation.z = -Math.PI / 3 * progress;
        rig.group.updateMatrixWorld(true);
    }
    positionAnchors(0);
    const skeleton = new Skeleton(bones); skeleton.calculateInverses();
    const layers = Object.fromEntries(manifest.layers.map(layer => [layer.id, layer]));

    function attachment(id, layerId, map, order, tint = '#ffffff', crop = null) {
        const layer = layers[layerId]; if (!layer) throw new Error(`Missing skin layer: ${layerId}`);
        const box = crop || {left: layer.left, top: layer.top, width: layer.width, height: layer.height};
        const columns = Math.max(6, Math.ceil(box.width / 7)), rows = Math.max(6, Math.ceil(box.height / 7));
        const positions = [], uv = [], indices = [], weights = [], triangles = [];
        for (let row = 0; row <= rows; row++) for (let col = 0; col <= columns; col++) {
            const x = box.left + col / columns * box.width, y = box.top + row / rows * box.height;
            const mapped = map(x, y); positions.push(mapped.point.x, mapped.point.y, 0);
            uv.push((layer.atlasX + x - layer.left) / manifest.width, 1 - (layer.atlasY + y - layer.top) / manifest.height);
            for (let k = 0; k < 4; k++) {indices.push(lookup[mapped.weights[k]?.[0]] || 0); weights.push(mapped.weights[k]?.[1] || 0);}
            if (row < rows && col < columns) {const a = row * (columns + 1) + col; triangles.push(a, a + 1, a + columns + 1, a + 1, a + columns + 2, a + columns + 1);}
        }
        const geometry = new BufferGeometry();
        geometry.setAttribute('position', new BufferAttribute(new Float32Array(positions), 3)); geometry.setAttribute('uv', new BufferAttribute(new Float32Array(uv), 2));
        geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(indices), 4)); geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(weights), 4)); geometry.setIndex(triangles);
        const material = new MeshBasicMaterial({map: texture, color: tint, transparent: true, depthTest: false, depthWrite: false, side: DoubleSide, alphaTest: .015});
        const mesh = new SkinnedMesh(geometry, material); mesh.name = id; mesh.renderOrder = order; mesh.frustumCulled = false;
        mesh.bind(skeleton, new Matrix4()); rig.group.add(mesh); meshes.push(mesh); return mesh;
    }
    const rigid = (name, origin, source, scaleX, scaleY = scaleX) => (x, y) => ({point: origin.clone().add(new Vector3((source[0] - x) * scaleX, (source[1] - y) * scaleY, 0)), weights: [[name, 1]]});
    function tube(id, from, to, width, order, tint) {
        const a = rest[from], b = rest[to], direction = b.clone().sub(a).normalize(), normal = new Vector3(-direction.y, direction.x, 0);
        return attachment(id, 'shaft', (x, y) => {
            const [top, bottom] = Array.isArray(width) ? width : [width, width], t = Math.max(0, Math.min(1, (y - 36) / 120)), currentWidth = top + (bottom - top) * t;
            const along = y < 36 ? (y - 36) / 36 * top / 2 : y > 156 ? a.distanceTo(b) + (y - 156) / 36 * bottom / 2 : t * a.distanceTo(b);
            return {point: a.clone().addScaledVector(direction, along).addScaledVector(normal, (36 - x) * currentWidth / 72), weights: [[from, 1]]};
        }, order, tint);
    }
    for (const side of ['far', 'near']) {
        const tint = side === 'far' ? '#f1ebe5' : '#ffffff', order = side === 'far' ? 1 : 5;
        tube(side + 'Tibia', side + 'Knee', side + 'Hock', [.38, .16], order, tint);
        tube(side + 'Metatarsal', side + 'Hock', side + 'Toe', .13, order, tint);
        attachment(side + 'Paw', 'nearHindPaw', rigid(side + 'Paw', rest[side + 'Toe'].clone().add(new Vector3(0, -.087, 0)), [291, 500], .008, .007), order + .1, tint);
        const center = rest[side + 'Hip'].clone().lerp(rest[side + 'Knee'], .4);
        attachment(side + 'Thigh', 'nearHindThigh', rigid(side + 'Hip', center, [303, 381], .010, .010), order + .2, tint);
        attachment(side + 'Foreleg', 'nearFront', (x, y) => {
            const sections = [{y: 350, name: side + 'Shoulder', next: side + 'Elbow', end: 420}, {y: 420, name: side + 'Elbow', next: side + 'Wrist', end: 463}];
            if (y >= 463) {
                const t = (y - 463) / 33, point = rest[side + 'Wrist'].clone().lerp(rest[side + 'FrontToe'], t);
                point.x += (134 - x) * .008; point.y -= .075 * t;
                return {point, weights: [[side + 'FrontToe', 1]]};
            }
            const section = sections[y < 420 ? 0 : 1], a = rest[section.name], b = rest[section.next], t = (y - section.y) / (section.end - section.y);
            const centerX = y < 420 ? 146 + (143 - 146) * t : 143 + (134 - 143) * t;
            const point = a.clone().lerp(b, t).add(new Vector3((centerX - x) * .008, 0, 0));
            const blend = Math.max(0, Math.min(1, (y - 408) / 24));
            return {point, weights: y < 432 ? [[side + 'Shoulder', 1 - blend], [side + 'Elbow', blend]] : [[side + 'Elbow', 1]]};
        }, side === 'far' ? 2 : 8, tint);
    }
    attachment('torso', 'torso', (x, y) => {
        const s = Math.max(0, Math.min(5.999, (393 - x) / 298 * 6)), i = Math.floor(s), t = s - i;
        const from = i === 0 ? 'root' : `spine${i - 1}`, to = `spine${i}`;
        const direction = rest[to].clone().sub(rest[from]).normalize();
        const nextDirection = i < 5 ? rest[`spine${i + 1}`].clone().sub(rest[to]).normalize() : direction;
        const angle = Math.atan2(direction.y, direction.x) * (1 - t) + Math.atan2(nextDirection.y, nextDirection.x) * t;
        const normal = new Vector3(-Math.sin(angle), Math.cos(angle), 0);
        const point = rest[from].clone().lerp(rest[to], t).addScaledVector(normal, (286 - y) * .0075);
        const next = i === 5 ? from : to;
        return {point, weights: [[from, 1 - t], [next, t]]};
    }, 4);
    const neckCenter = rest.spine5.clone().lerp(rest.skull, .55).add(new Vector3(-.05, -.13, 0));
    attachment('neck', 'neck', rigid('spine5', neckCenter, [50, 60], .012, .0105), 7);
    attachment('tail', 'tail', rigid('tailArt', anchors.tailArt.position, [372, 350], .012, .014), 0);
    attachment('head', 'head', rigid('skull', rest.skull.clone().add(new Vector3(.05, -.04, 0)), [153, 275], .009), 10);
    function apply(progress) {
        const pose = rig.apply(progress); positionAnchors(progress); skeleton.update();
        let floor = Infinity;
        const angle = anchors.tailArt.rotation.z;
        for (const [x, y] of layers.tail.support) floor = Math.min(floor, anchors.tailArt.position.y + (372 - x) * .012 * Math.sin(angle) + (350 - y) * .014 * Math.cos(angle));
        if (floor < .025) {anchors.tailArt.position.y += .025 - floor; rig.group.updateMatrixWorld(true); skeleton.update();}
        return pose;
    }
    apply(0);
    return {rig, skeleton, meshes, apply, dispose() {for (const mesh of meshes) {mesh.geometry.dispose(); mesh.material.dispose();} skeleton.dispose(); rig.dispose();}};
}
