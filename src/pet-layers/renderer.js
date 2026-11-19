import { Color, DoubleSide, Mesh, MeshBasicMaterial, OrthographicCamera, Scene, SkeletonHelper, SRGBColorSpace, TextureLoader, WebGLRenderer } from 'three';
import { createSkin, makeGeometry } from '../pet-mesh/skin.js';
import { applyFarPose, createFarSkin, poseAt } from './skin.js';

export async function createPreview(host) {
    const renderer = new WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2)); renderer.outputColorSpace = SRGBColorSpace;
    renderer.setClearColor(new Color('#f0f6f1'), 0); host.appendChild(renderer.domElement);
    renderer.domElement.setAttribute('aria-label', '同源远侧后腿分层动作预览');
    const textures = [], skins = [], materials = [];
    const scene = new Scene(), camera = new OrthographicCamera(-320, 320, 270, -270, .1, 100); camera.position.z = 10;
    const load = async path => { const texture = await new TextureLoader().loadAsync(path); texture.colorSpace = SRGBColorSpace; textures.push(texture); return texture; };
    const material = (texture, wire = false) => {
        const result = new MeshBasicMaterial({ map: texture, transparent: true, side: DoubleSide, depthTest: false, depthWrite: false,
            alphaTest: .01, wireframe: wire, color: wire ? '#155d43' : '#ffffff', opacity: wire ? .4 : 1 });
        materials.push(result); return result;
    };
    let helper, original;
    try {
        const sourceTexture = await load('/pet-mesh/cat-side.png');
        const frontTexture = await load('/pet-layers/foreground-clean.png');
        const beforeFrontTexture = await load('/pet-layers/foreground.png');
        const farTexture = await load('/pet-layers/far-hind-clean.png');
        const aiTexture = await load('/pet-layers/far-hind-ai-clean.png');
        const beforeTexture = await load('/pet-layers/far-hind-ai.png');
        original = new Mesh(makeGeometry(), material(sourceTexture)); original.renderOrder = 0; scene.add(original);
        const far = createFarSkin(material(farTexture)), front = createSkin(material(frontTexture));
        const farWire = createFarSkin(material(farTexture, true)), frontWire = createSkin(material(frontTexture, true));
        skins.push(far, front, farWire, frontWire);
        for (const [i, skin] of skins.entries()) { skin.mesh.renderOrder = i; scene.add(skin.mesh); }
        helper = new SkeletonHelper(far.mesh); helper.setColors(new Color('#b06464'), new Color('#d78484')); helper.renderOrder = 5; scene.add(helper);
        let dirty = true, lastTime, lastState;
        const resize = () => {
            const { width, height } = host.getBoundingClientRect();
            const aspect = Math.max(1, width) / Math.max(1, height);
            camera.left = -270 * aspect; camera.right = 270 * aspect; camera.updateProjectionMatrix();
            renderer.setSize(width, height, false); dirty = true;
        };
        const observer = new ResizeObserver(resize); observer.observe(host); resize();
        const draw = (state, time) => {
            if (!dirty && time === lastTime && state === lastState) return;
            const pose = poseAt(time, state.action, state.amount, state.manual);
            applyFarPose(far, pose); applyFarPose(farWire, pose);
            const layers = state.mode === 'layers';
            const map = state.completion === 'donor' ? farTexture : state.completion === 'before' ? beforeTexture : aiTexture;
            far.mesh.material.map = map; farWire.mesh.material.map = map;
            const frontMap = state.completion === 'before' ? beforeFrontTexture : frontTexture;
            front.mesh.material.map = frontMap; frontWire.mesh.material.map = frontMap;
            original.visible = !layers; far.mesh.visible = layers; front.mesh.visible = layers && state.front;
            farWire.mesh.visible = layers && state.wire; frontWire.mesh.visible = layers && state.front && state.wire;
            helper.visible = layers && state.bones;
            renderer.render(scene, camera); dirty = false; lastTime = time; lastState = state;
        };
        const dispose = () => { observer.disconnect(); cleanup(); };
        return { draw, dispose, renderer, invalidate: () => { dirty = true; } };
    } catch (error) { cleanup(); throw error; }
    function cleanup() {
        helper?.geometry.dispose(); helper?.material.dispose(); original?.geometry.dispose();
        for (const skin of skins) { skin.geometry.dispose(); skin.skeleton.dispose(); }
        for (const entry of materials) entry.dispose(); for (const entry of textures) entry.dispose();
        renderer.dispose(); renderer.domElement.remove();
    }
}
