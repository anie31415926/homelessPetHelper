import {BufferGeometry, Color, CylinderGeometry, Group, Line, LineBasicMaterial, Mesh, MeshBasicMaterial, OrthographicCamera, Scene, SphereGeometry, Vector3, WebGLRenderer} from 'three';
import {createSitRig} from './rig.js';

const COLORS = {bone: '#56615d', far: '#b9c3bf', hind: '#167a66', knee: '#a96116', hock: '#9d4b70', floor: '#c6cfca'};
const labels = [['nearHip', '髋', -32, -28], ['nearKnee', '膝', 13, -25], ['nearHock', '跗', -30, 6], ['nearToe', '趾', 13, 14]];

function line(points, color, opacity = 1) {
    return new Line(new BufferGeometry().setFromPoints(points.map(p => new Vector3(...p))), new LineBasicMaterial({color, transparent: opacity < 1, opacity}));
}

function ellipse(rx, ry, z = 0) {
    return Array.from({length: 65}, (_, i) => [rx * Math.cos(i / 64 * Math.PI * 2), ry * Math.sin(i / 64 * Math.PI * 2), z]);
}

function createBody() {
    const ribs = new Group(), pelvis = new Group(), skull = new Group();
    ribs.add(line(ellipse(.8, .48), COLORS.far));
    for (const x of [-.48, -.24, 0, .24, .48]) {
        const radius = .48 * Math.sqrt(1 - (x / .86) ** 2);
        const points = Array.from({length: 33}, (_, i) => [x + .1 * Math.sin(i / 32 * Math.PI), radius * Math.cos(i / 32 * Math.PI), .33 * Math.sin(i / 32 * Math.PI)]);
        ribs.add(line(points, COLORS.far));
    }
    pelvis.add(line(ellipse(.4, .24), COLORS.bone));
    pelvis.add(line([[-.38, 0, 0], [-.22, -.23, .12], [.19, -.12, .12], [.3, .05, 0]], COLORS.bone));
    skull.add(line(ellipse(.3, .25), COLORS.bone));
    skull.add(line([[.17, .15, 0], [.42, .02, 0], [.43, -.09, 0], [.12, -.2, 0], [-.13, -.19, 0]], COLORS.bone));
    skull.add(line([[.38, -.13, .01], [.25, -.27, .01], [-.12, -.25, .01], [-.25, -.1, .01]], COLORS.bone));
    const eye = new Mesh(new SphereGeometry(.067, 20, 12), new MeshBasicMaterial({color: COLORS.bone}));
    eye.position.set(.13, .055, .07); skull.add(eye);
    return {ribs, pelvis, skull};
}

export function createPreview(host, labelHost) {
    const renderer = new WebGLRenderer({antialias: true, preserveDrawingBuffer: true});
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2)); renderer.setClearColor(new Color('#f5f7f6'));
    renderer.domElement.setAttribute('aria-label', '猫站到坐的侧面骨架小样'); host.append(renderer.domElement);
    const scene = new Scene(), rig = createSitRig(), body = createBody();
    const camera = new OrthographicCamera(-4, 4, 3.8, -.6, .1, 50);
    camera.position.set(0, 0, 12); scene.add(rig.group, ...Object.values(body));
    const materials = Object.fromEntries(Object.entries(COLORS).map(([name, color]) => [name, new MeshBasicMaterial({color})]));
    const boneGeometry = new CylinderGeometry(.027, .037, 1, 8), jointGeometry = new SphereGeometry(.056, 16, 12);
    const boneMeshes = rig.segments.map(segment => {
        const name = segment.to, material = name.startsWith('far') ? materials.far : /near(Knee|Hock|Toe)/.test(name) ? materials.hind : materials.bone;
        const mesh = new Mesh(boneGeometry, material); scene.add(mesh); return {segment, mesh};
    });
    const jointMeshes = Object.keys(rig.joints).filter(name => !name.endsWith('Target') && name !== 'pelvis' && name !== 'skull').map(name => {
        const material = name.startsWith('far') ? materials.far : name === 'nearKnee' ? materials.knee : name === 'nearHock' ? materials.hock : name.startsWith('near') && !/Shoulder|Elbow|Wrist|FrontToe/.test(name) ? materials.hind : materials.bone;
        const mesh = new Mesh(jointGeometry, material); scene.add(mesh); return {name, mesh};
    });
    const tail = line(Array.from({length: 25}, () => [0, 0, 0]), COLORS.far); scene.add(tail);
    const scapula = line([[0, 0, 0], [0, 0, 0], [0, 0, 0], [0, 0, 0]], COLORS.bone); scene.add(scapula);
    scene.add(line([[-3.5, .015, -.5], [3.3, .015, -.5]], COLORS.floor));
    const ghosts = new Group(); scene.add(ghosts);
    for (const at of [0, 1]) {
        rig.apply(at);
        for (const segment of rig.segments.filter(s => s.to.startsWith('near') || s.to.startsWith('spine'))) {
            ghosts.add(line([rig.world(segment.from).toArray(), rig.world(segment.to).toArray()], '#b7c4be', .35));
        }
    }
    const labelElements = labels.map(([name, text]) => {
        const element = document.createElement('span'); element.textContent = text; element.dataset.joint = name; labelHost.append(element); return element;
    });
    let current = {progress: 0, labels: true, ghost: false}, disposed = false;
    const up = new Vector3(0, 1, 0), direction = new Vector3();
    function render(state = current) {
        if (disposed) return;
        current = state; const pose = rig.apply(state.progress);
        for (const {segment, mesh} of boneMeshes) {
            const from = rig.world(segment.from), to = rig.world(segment.to);
            mesh.position.copy(from).add(to).multiplyScalar(.5);
            mesh.quaternion.setFromUnitVectors(up, direction.subVectors(to, from).normalize()); mesh.scale.y = segment.length;
        }
        for (const {name, mesh} of jointMeshes) mesh.position.copy(rig.world(name));
        const chest = rig.world('spine5'), ribBack = rig.world('spine2');
        body.ribs.position.copy(chest).lerp(ribBack, .48); body.ribs.position.y -= .24;
        body.ribs.rotation.z = (pose.spine[3] + pose.spine[4]) / 2;
        body.pelvis.position.copy(rig.world('root')); body.pelvis.rotation.z = pose.pelvis * Math.PI / 180;
        body.skull.position.copy(rig.world('skull')); body.skull.rotation.z = (5 + 13 * state.progress) * Math.PI / 180;
        const scapulaPoints = [rig.world('spine4').add(new Vector3(-.15, -.08, .24)), rig.world('spine5').add(new Vector3(.02, .03, .24)), rig.world('nearShoulder'), rig.world('spine4').add(new Vector3(-.15, -.08, .24))];
        scapulaPoints.forEach((point, i) => scapula.geometry.attributes.position.setXYZ(i, point.x, point.y, point.z));
        scapula.geometry.attributes.position.needsUpdate = true; scapula.geometry.computeBoundingSphere();
        const tailPositions = tail.geometry.attributes.position;
        for (let i = 0; i < tailPositions.count; i++) {
            const t = i / (tailPositions.count - 1), hip = rig.world('root');
            const y = Math.max(.09, hip.y - .18 - .8 * Math.sin(t * Math.PI / 2));
            tailPositions.setXYZ(i, hip.x - .28 - 1.2 * t, y, -.1);
        }
        tailPositions.needsUpdate = true; tail.geometry.computeBoundingSphere();
        ghosts.visible = state.ghost;
        const {width, height} = host.getBoundingClientRect();
        labelElements.forEach((element, i) => {
            const [name, , dx, dy] = labels[i], point = rig.world(name).project(camera);
            element.style.transform = `translate(${(point.x + 1) / 2 * width + dx}px, ${(-point.y + 1) / 2 * height + dy}px)`;
            element.hidden = !state.labels;
        });
        renderer.domElement.dataset.progress = String(state.progress); renderer.render(scene, camera);
    }
    function resize() {
        const {width, height} = host.getBoundingClientRect(), aspect = Math.max(1, width) / Math.max(1, height);
        const halfWidth = Math.max(2.95, 2.05 * aspect), halfHeight = halfWidth / aspect;
        camera.left = -halfWidth; camera.right = halfWidth; camera.top = 1.55 + halfHeight; camera.bottom = 1.55 - halfHeight;
        camera.updateProjectionMatrix(); renderer.setSize(width, height, false); render();
    }
    const observer = new ResizeObserver(resize); observer.observe(host); resize();
    return {render, rig, renderer, camera, dispose() {
        disposed = true; observer.disconnect(); rig.dispose(); boneGeometry.dispose(); jointGeometry.dispose();
        scene.traverse(object => {if (object.isLine) {object.geometry.dispose(); object.material.dispose();}});
        for (const material of Object.values(materials)) material.dispose();
        body.skull.traverse(object => {if (object.isMesh) {object.geometry.dispose(); object.material.dispose();}});
        renderer.dispose(); renderer.domElement.remove(); labelElements.forEach(element => element.remove());
    }};
}
