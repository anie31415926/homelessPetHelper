import {
    Color, DoubleSide, Mesh, MeshBasicMaterial, OrthographicCamera,
    Scene, SkeletonHelper, SRGBColorSpace, TextureLoader, WebGLRenderer,
} from 'three';
import { applyPose, createSkin, samplePose } from './skin.js';

export async function createPreview(host) {
    const renderer = new WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2));
    renderer.outputColorSpace = SRGBColorSpace;
    renderer.setClearColor(new Color('#f0f6f1'), 0);
    renderer.domElement.setAttribute('aria-label', '单张完整猫咪图片的连续网格形变');
    host.appendChild(renderer.domElement);
    let texture;
    try { texture = await new TextureLoader().loadAsync('/pet-mesh/cat-side.png'); }
    catch (error) { renderer.dispose(); renderer.domElement.remove(); throw error; }
    texture.colorSpace = SRGBColorSpace;
    const scene = new Scene(), camera = new OrthographicCamera(-320, 320, 270, -270, .1, 100);
    camera.position.z = 10;
    const material = new MeshBasicMaterial({ map: texture, transparent: true, side: DoubleSide, depthWrite: false, alphaTest: .01 });
    const skin = createSkin(material); scene.add(skin.mesh);
    const original = new Mesh(skin.geometry, material); original.visible = false; scene.add(original);
    const wireMaterial = new MeshBasicMaterial({ map: texture, color: '#155d43', wireframe: true, transparent: true, opacity: .4, side: DoubleSide, depthTest: false, depthWrite: false, alphaTest: .01 });
    const wire = createSkin(wireMaterial); wire.mesh.renderOrder = 2; scene.add(wire.mesh);
    const helper = new SkeletonHelper(skin.mesh); helper.material.depthTest = false; helper.renderOrder = 3; scene.add(helper);
    helper.setColors(new Color('#b06464'), new Color('#d78484'));
    const resize = () => {
        const { width, height } = host.getBoundingClientRect();
        const aspect = Math.max(1, width) / Math.max(1, height);
        camera.left = -270 * aspect; camera.right = 270 * aspect;
        camera.updateProjectionMatrix(); renderer.setSize(width, height, false);
    };
    const observer = new ResizeObserver(resize); observer.observe(host); resize();
    let lastTime, lastState, dirty = true;
    const invalidate = () => { dirty = true; };
    const draw = (state, time) => {
        if (!dirty && state === lastState && time === lastTime) return;
        const pose = samplePose(time, state.action, state.amount, state.manual);
        applyPose(skin, pose); applyPose(wire, pose);
        const visible = state.mode === 'mesh';
        skin.mesh.visible = visible; original.visible = !visible;
        wire.mesh.visible = visible && state.wire; helper.visible = visible && state.bones;
        renderer.render(scene, camera);
        lastTime = time; lastState = state; dirty = false;
    };
    const resizeObserver = new ResizeObserver(invalidate); resizeObserver.observe(host);
    const dispose = () => {
        observer.disconnect(); resizeObserver.disconnect();
        helper.geometry.dispose(); helper.material.dispose(); wire.geometry.dispose(); wire.skeleton.dispose();
        skin.geometry.dispose(); skin.skeleton.dispose(); material.dispose(); wireMaterial.dispose(); texture.dispose();
        renderer.dispose(); renderer.domElement.remove();
    };
    return { draw, dispose, invalidate, renderer, skin };
}
