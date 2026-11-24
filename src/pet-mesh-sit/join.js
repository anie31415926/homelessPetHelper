import {BufferAttribute, CanvasTexture, DoubleSide, Matrix4, MeshBasicMaterial, PlaneGeometry, SkinnedMesh, SRGBColorSpace, Vector3} from 'three';
import {sourcePoint} from '../pet-sit-chibi/rig.js';

export const ramp = (a, b, n) => {
    const t = Math.max(0, Math.min(1, (n - a) / (b - a)));
    return t * t * (3 - 2 * t);
};

const layers = ['nearHindPaw', 'hindLower'];
export const hindBounds = [244, 408, 116, 93];

export function hindWeights(side, y, x) {
    // The outer heel stroke belongs to the planted paw; blend on the fur above and inward.
    const heel = side === 'near' ? ramp(306, 322, x) : 0;
    const ankle = Math.max(ramp(444, 473, y), heel * ramp(418, 438, y));
    return [[side + 'Hock', 1 - ankle], [side + 'Paw', ankle]];
}

export function foreWeights(side, y) {
    const elbow = ramp(399, 430, y), wrist = ramp(447, 469, y);
    return [[side + 'Shoulder', (1 - elbow) * (1 - wrist)], [side + 'Elbow', elbow * (1 - wrist)], [side + 'Wrist', wrist]];
}

export function coatTexture(image, manifest) {
    const [left, top, width, height] = hindBounds;
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    const ctx = canvas.getContext('2d');
    for (const id of layers) {
        const p = manifest.layers.find(layer => layer.id === id);
        // Remove the old cut edge within the overlap, before either side is animated.
        const fade = id === 'hindLower' ? [451, 473] : null;
        const part = document.createElement('canvas');
        part.width = p.width; part.height = p.height;
        const pen = part.getContext('2d');
        pen.drawImage(image, p.atlasX, p.atlasY, p.width, p.height, 0, 0, p.width, p.height);
        if (fade) {
            const mask = pen.createLinearGradient(0, fade[0] - p.top, 0, fade[1] - p.top);
            mask.addColorStop(0, '#fff'); mask.addColorStop(1, '#fff0');
            pen.globalCompositeOperation = 'destination-in';
            pen.fillStyle = mask; pen.fillRect(0, 0, p.width, p.height);
        }
        ctx.drawImage(part, p.left - left, p.top - top);
    }
    const texture = new CanvasTexture(canvas);
    texture.colorSpace = SRGBColorSpace;
    return texture;
}

export function attachJoinedHind(skin, atlas, manifest) {
    const [left, top, width, height] = hindBounds;
    const map = coatTexture(atlas.image, manifest);
    const indices = new Map(skin.skeleton.bones.map((bone, i) => [bone.name, i]));
    const result = [];
    for (const side of ['far', 'near']) {
        const far = side === 'far';
        const offset = far ? skin.rig.profile.rig.farHindOffset : [0, 0];
        const geometry = new PlaneGeometry(width, height, 39, 62);
        const position = geometry.attributes.position, uv = geometry.attributes.uv;
        const ids = [], amounts = [];
        for (let i = 0; i < position.count; i++) {
            const y = top + (1 - uv.getY(i)) * height;
            const x = left + uv.getX(i) * width;
            const p = sourcePoint(x, y);
            position.setXYZ(i, p.x + offset[0], p.y + offset[1], 0);
            const weights = hindWeights(side, y, x);
            ids.push(...weights.map(([name]) => indices.get(name)), 0, 0);
            amounts.push(...weights.map(([, weight]) => weight), 0, 0);
        }
        geometry.setAttribute('skinIndex', new BufferAttribute(new Uint16Array(ids), 4));
        geometry.setAttribute('skinWeight', new BufferAttribute(new Float32Array(amounts), 4));
        const mesh = new SkinnedMesh(geometry, new MeshBasicMaterial({
            map, color: far ? '#f4efe9' : '#fff', transparent: true, alphaTest: .015, side: DoubleSide, depthWrite: false, depthTest: false
        }));
        mesh.renderOrder = far ? 1.13 : 5.13;
        mesh.frustumCulled = false;
        mesh.bind(skin.skeleton, new Matrix4());
        skin.rig.group.add(mesh);
        result.push(mesh);
    }
    const originals = skin.meshes.filter(mesh => /(?:Paw|LowerCoat)$/.test(mesh.name));
    for (const mesh of originals) mesh.visible = false;
    return result;
}

export function attachJoinedFore(skin, manifest) {
    const layer = manifest.layers.find(layer => layer.id === 'nearFront');
    const result = [];
    const ids = new Map(skin.skeleton.bones.map((bone, i) => [bone.name, i]));
    for (const side of ['near', 'far']) {
        const base = skin.meshes.find(mesh => mesh.name === side + 'Foreleg');
        const geometry = base.geometry.clone();
        const {uv, skinIndex, skinWeight} = geometry.attributes;
        for (let i = 0; i < uv.count; i++) {
            const y = (1 - uv.getY(i)) * manifest.height - layer.atlasY + layer.top;
            const weights = foreWeights(side, y);
            skinIndex.setXYZW(i, ...weights.map(([name]) => ids.get(name)), 0);
            skinWeight.setXYZW(i, ...weights.map(([, weight]) => weight), 0);
        }
        const mesh = new SkinnedMesh(geometry, base.material);
        mesh.frustumCulled = false; mesh.renderOrder = base.renderOrder;
        mesh.bind(skin.skeleton, new Matrix4());
        skin.rig.group.add(mesh);
        base.visible = false; result.push(mesh);
    }
    return result;
}

export function liftForepaw(rig, progress) {
    const amount = Math.sin(Math.PI * progress) ** 2;
    const {nearShoulder: shoulder, nearElbow: elbow, nearWrist: wrist} = rig.joints;
    const a = rig.world('nearShoulder'), end = rig.world('nearWrist');
    const palm = rig.world('nearFrontToe').sub(end);
    const palmAngle = Math.atan2(palm.y, palm.x);
    const {humerus, radius} = rig.lengths;
    end.x += .28 * amount; end.y += .40 * amount;
    const delta = end.clone().sub(a);
    const d = Math.hypot(delta.x, delta.y);
    const base = Math.atan2(delta.y, delta.x);
    const beta = Math.acos(Math.max(-1, Math.min(1, (humerus ** 2 + d ** 2 - radius ** 2) / (2 * humerus * d))));
    const bend = base - beta;
    const b = a.clone().add(new Vector3(Math.cos(bend), Math.sin(bend), 0).multiplyScalar(humerus));
    const forearm = Math.atan2(end.y - b.y, end.x - b.x);
    shoulder.rotation.z = bend;
    elbow.rotation.z = forearm - bend;
    wrist.rotation.z = palmAngle + (forearm - palmAngle) * .7 * amount - forearm;
    rig.group.updateMatrixWorld(true);
}
