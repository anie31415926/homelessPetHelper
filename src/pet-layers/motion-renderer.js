import { BufferGeometry, CircleGeometry, Color, DoubleSide, Group, Line, LineBasicMaterial, Mesh, MeshBasicMaterial, OrthographicCamera, Scene, SkeletonHelper, SkinnedMesh, SRGBColorSpace, TextureLoader, Vector3, WebGLRenderer } from 'three';
import { makeGeometry, SIZE } from '../pet-mesh/skin.js';
import { applyMotionBody, applyMotionLimb, createMotionBody, createMotionLimb, FEET, sampleMotion, UNIT } from './motion.js';
import { applyLyingSkin, createLyingSkin } from './lying.js';
import { applyPostureBody, applyPostureFarHind, applyPostureFront, createPostureBody, POSTURES } from './postures.js';
import { applySideRest, createSideRest } from './side-rest.js';
import { applySitRest, createSitRest } from './sit-rest.js';

export async function createMotionPreview(host) {
    const renderer = new WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2)); renderer.outputColorSpace = SRGBColorSpace;
    renderer.setClearColor(new Color('#f0f6f1'), 0); renderer.domElement.setAttribute('aria-label', '同源四腿整身动作预览'); host.appendChild(renderer.domElement);
    const scene = new Scene(), character = new Group(), camera = new OrthographicCamera(-320, 320, 320, -320, .1, 100);
    camera.position.z = 10; scene.add(character);
    const textures = [], materials = [], geometries = [], skins = [], helpers = [], layers = [];
    const material = (map, wireframe = false) => {
        const result = new MeshBasicMaterial({ map, transparent: true, side: DoubleSide, depthTest: false, depthWrite: false, alphaTest: .01,
            color: wireframe ? '#155d43' : '#ffffff', wireframe, opacity: wireframe ? .35 : 1 });
        materials.push(result); return result;
    };
    const load = async path => { const texture = await new TextureLoader().loadAsync(path); texture.colorSpace = SRGBColorSpace; textures.push(texture); return texture; };
    const addLayer = (skin, texture, order, type) => {
        skin.mesh.name = typeof type === 'string' ? type : type.id;
        const wire = new SkinnedMesh(skin.geometry, material(texture, true)); wire.bind(skin.skeleton, skin.mesh.bindMatrix);
        const helper = new SkeletonHelper(skin.mesh); helper.material.depthTest = false; helper.renderOrder = 30;
        helper.setColors(new Color('#638b73'), new Color('#d78484')); helpers.push(helper); scene.add(helper);
        skin.mesh.renderOrder = order; wire.renderOrder = order + 10; character.add(skin.mesh, wire); skins.push(skin);
        const layer = { skin, wire, helper, type }; layers.push(layer); return layer;
    };
    let observer, dirty = true, lastState, lastTime;
    try {
        const footTextures = {};
        for (const foot of FEET.filter(foot => foot.side === 'far')) {
            const texture = await load(`/pet-limbs/far-${foot.kind}-copy.png`);
            footTextures[foot.id] = texture;
            addLayer(createMotionLimb(material(texture), foot.kind, foot.side), texture, foot.kind === 'hind' ? 0 : 1, foot);
        }
        const bodyTexture = await load('/pet-motion/body.png'); addLayer(createMotionBody(material(bodyTexture)), bodyTexture, 2, 'body');
        let hindTexture;
        for (const foot of FEET.filter(foot => foot.side === 'near')) {
            const texture = await load(`/pet-motion/near-${foot.kind}.png`);
            if (foot.kind === 'hind') hindTexture = texture;
            footTextures[foot.id] = texture;
            addLayer(createMotionLimb(material(texture), foot.kind, foot.side), texture, foot.kind === 'hind' ? 3 : 4, foot);
        }
        const connectedTexture = await load('/pet-motion/lie-connected.png');
        const connected = addLayer(createLyingSkin(material(connectedTexture)), connectedTexture, 2, 'connected');
        const postureBody = addLayer(createPostureBody(material(connectedTexture)), connectedTexture, 2, 'postureBody');
        for (const foot of FEET.filter(foot => foot.id !== 'nearHind')) {
            const texture = footTextures[foot.id];
            addLayer(createMotionLimb(material(texture), foot.kind, foot.side), texture, foot.side === 'near' ? 4 : foot.kind === 'hind' ? 0 : 1, { ...foot, posture: true });
        }
        const sideTexture = await load('/pet-motion/side-rest.png');
        const sideRest = addLayer(createSideRest(material(sideTexture)), sideTexture, 2, 'sideRest');
        const sitTexture = await load('/pet-motion/sit-rest.png');
        const sitRest = addLayer(createSitRest(material(sitTexture)), sitTexture, 2, 'sitRest');
        const originalTexture = await load('/pet-mesh/cat-side.png');
        const original = new Mesh(makeGeometry(), material(originalTexture)); geometries.push(original.geometry); scene.add(original);
        const floor = makeFloor(); scene.add(floor.line, floor.shadow); geometries.push(floor.line.geometry, floor.shadow.geometry); materials.push(floor.line.material, floor.shadow.material);
        const resize = () => {
            const { width, height } = host.getBoundingClientRect(), aspect = Math.max(1, width) / Math.max(1, height);
            const halfHeight = Math.max(320, (SIZE.width * .5 + 80) / aspect);
            camera.top = halfHeight; camera.bottom = -halfHeight; camera.left = -halfHeight * aspect; camera.right = halfHeight * aspect;
            camera.updateProjectionMatrix(); renderer.setSize(width, height, false); dirty = true;
        };
        observer = new ResizeObserver(resize); observer.observe(host); resize();
        const draw = (state, time) => {
            if (!dirty && state === lastState && time === lastTime) return;
            const pose = sampleMotion(time, state.action, state.amount), motion = state.mode === 'motion';
            // Solve in unmirrored local space; apply the shared body translation afterwards.
            character.position.set(0, 0, 0); character.scale.set(1, 1, 1); character.updateMatrixWorld(true);
            for (const layer of layers) {
                const rest = state.action === 'side' || state.action === 'sit';
                if (layer === sideRest || layer === sitRest) {
                    const active = layer === sideRest ? state.action === 'side' : state.action === 'sit';
                    if (active) (layer === sideRest ? applySideRest : applySitRest)(layer.skin, pose);
                    layer.skin.mesh.visible = motion && active && state.body;
                    layer.wire.visible = motion && active && state.wire; layer.helper.visible = motion && active && state.bones;
                    continue;
                }
                const lying = state.action === 'lie';
                const posture = POSTURES.includes(state.action), postureLayer = layer === postureBody || layer.type.posture;
                if (postureLayer) {
                    if (posture) {
                        if (layer === postureBody) applyPostureBody(layer.skin, pose);
                        else if (layer.type.kind === 'front') applyPostureFront(layer.skin, pose, layer.type.side);
                        else applyPostureFarHind(layer.skin, pose);
                    }
                    const visible = motion && posture && !rest;
                    layer.skin.mesh.visible = visible; layer.wire.visible = visible && state.wire; layer.helper.visible = visible && state.bones;
                    continue;
                }
                const replaced = layer.type === 'body' || layer.type.id === 'nearHind';
                if (layer.type === 'connected') { if (lying) applyLyingSkin(layer.skin, pose); }
                else if (!lying || !replaced) {
                    if (layer.type === 'body') applyMotionBody(layer.skin, pose);
                    else applyMotionLimb(layer.skin, pose, pose.feet[layer.type.id], layer.type.kind);
                }
                const visible = motion && !posture && !rest && (layer === connected ? lying : !lying || !replaced) && (layer.type !== 'body' || state.body);
                layer.skin.mesh.visible = visible; layer.wire.visible = visible && state.wire; layer.helper.visible = visible && state.bones;
            }
            connected.skin.mesh.material.map = state.body ? connectedTexture : hindTexture;
            connected.wire.material.map = state.body ? connectedTexture : hindTexture;
            postureBody.skin.mesh.material.map = state.body ? connectedTexture : hindTexture;
            postureBody.wire.material.map = state.body ? connectedTexture : hindTexture;
            character.position.set(pose.x * UNIT, -pose.down * UNIT, 0); character.scale.x = pose.mirror ? -1 : 1; character.updateMatrixWorld(true);
            original.material.map = state.action === 'side' ? sideTexture : state.action === 'sit' ? sitTexture : originalTexture;
            original.visible = !motion; floor.shadow.position.x = motion ? character.position.x : 0; floor.shadow.scale.x = 1.5 + (motion ? pose.lie * .35 : 0);
            renderer.render(scene, camera); dirty = false; lastState = state; lastTime = time;
        };
        return { renderer, scene, camera, draw, invalidate: () => { dirty = true; }, dispose };
    } catch (error) { dispose(); throw error; }
    function dispose() {
        observer?.disconnect();
        for (const helper of helpers) { helper.geometry.dispose(); helper.material.dispose(); }
        for (const skin of skins) { skin.geometry.dispose(); skin.skeleton.dispose(); }
        for (const geometry of geometries) geometry.dispose(); for (const item of materials) item.dispose(); for (const texture of textures) texture.dispose();
        renderer.dispose(); renderer.domElement.remove();
    }
}
function makeFloor() {
    const y = (256 - 498) * UNIT;
    const line = new Line(new BufferGeometry().setFromPoints([new Vector3(-1000, y, 0), new Vector3(1000, y, 0)]), new LineBasicMaterial({ color: '#d6e2d9', transparent: true, opacity: .7 }));
    const shadow = new Mesh(new CircleGeometry(80, 48), new MeshBasicMaterial({ color: '#a6bdaa', transparent: true, opacity: .22, depthTest: false, depthWrite: false }));
    line.renderOrder = -3; shadow.renderOrder = -2; shadow.position.set(0, y + 3, 0); shadow.scale.set(1.5, .065, 1);
    return { line, shadow };
}
