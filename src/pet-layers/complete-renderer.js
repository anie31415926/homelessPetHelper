import { Color, DoubleSide, Mesh, MeshBasicMaterial, OrthographicCamera, Scene, SkeletonHelper, SRGBColorSpace, TextureLoader, WebGLRenderer } from 'three';
import { makeGeometry } from '../pet-mesh/skin.js';
import { applyLimbPose, createLimbSkin, limbPose } from './limbs.js';

export async function createPreview(host) {
    const renderer = new WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2)); renderer.outputColorSpace = SRGBColorSpace;
    renderer.setClearColor(new Color('#f0f6f1'), 0); host.appendChild(renderer.domElement);
    renderer.domElement.setAttribute('aria-label', '完整远侧前后腿动作预览');
    const scene = new Scene(), camera = new OrthographicCamera(-320, 320, 270, -270, .1, 100); camera.position.z = 10;
    const textures = [], materials = [], skins = [], helpers = [], staticMeshes = [];
    const load = async path => { const texture = await new TextureLoader().loadAsync(path); texture.colorSpace = SRGBColorSpace; textures.push(texture); return texture; };
    const material = (map, wireframe = false) => {
        const entry = new MeshBasicMaterial({ map, transparent: true, side: DoubleSide, depthTest: false, depthWrite: false, alphaTest: .01,
            wireframe, color: wireframe ? '#155d43' : '#ffffff', opacity: wireframe ? .4 : 1 });
        materials.push(entry); return entry;
    };
    let observer;
    try {
        const sourceTexture = await load('/pet-mesh/cat-side.png'), foregroundTexture = await load('/pet-limbs/foreground.png');
        const original = new Mesh(makeGeometry(), material(sourceTexture)), foreground = new Mesh(makeGeometry(), material(foregroundTexture));
        const frontWire = new Mesh(makeGeometry(), material(foregroundTexture, true));
        staticMeshes.push(original, foreground, frontWire); original.renderOrder = 0; foreground.renderOrder = 4; frontWire.renderOrder = 7;
        for (const mesh of staticMeshes) scene.add(mesh);
        const entries = {};
        for (const version of ['copy', 'ai']) for (const [index, kind] of ['hind', 'front'].entries()) {
            const texture = await load(`/pet-limbs/far-${kind}${version === 'copy' ? '-copy' : ''}.png`);
            const skin = createLimbSkin(material(texture), kind, version), wire = createLimbSkin(material(texture, true), kind, version);
            skin.mesh.renderOrder = index; wire.mesh.renderOrder = 5 + index; scene.add(skin.mesh, wire.mesh); skins.push(skin, wire);
            const helper = new SkeletonHelper(skin.mesh); helper.material.depthTest = false; helper.renderOrder = 8;
            helper.setColors(new Color(kind === 'front' ? '#638b73' : '#b06464'), new Color('#d78484'));
            helpers.push(helper); scene.add(helper); entries[`${version}-${kind}`] = { skin, wire, helper, version, kind };
        }
        let dirty = true, lastState, lastTime;
        const resize = () => {
            const { width, height } = host.getBoundingClientRect(), aspect = Math.max(1, width) / Math.max(1, height);
            camera.left = -270 * aspect; camera.right = 270 * aspect; camera.updateProjectionMatrix(); renderer.setSize(width, height, false); dirty = true;
        };
        observer = new ResizeObserver(resize); observer.observe(host); resize();
        const draw = (state, time) => {
            if (!dirty && state === lastState && time === lastTime) return;
            const layers = state.mode === 'layers';
            original.visible = !layers; foreground.visible = layers && state.front; frontWire.visible = layers && state.front && state.wire;
            for (const entry of Object.values(entries)) {
                const { kind, version } = entry;
                const selected = state.target === 'both' || state.target === kind;
                const visible = layers && version === state.completion && (state.front || selected);
                const pose = limbPose(time, selected ? state.action : 'rest', state.amount, selected ? state.manual : {}, kind);
                applyLimbPose(entry.skin, pose); applyLimbPose(entry.wire, pose);
                entry.skin.mesh.visible = visible; entry.wire.mesh.visible = visible && state.wire; entry.helper.visible = visible && state.bones;
            }
            renderer.render(scene, camera); dirty = false; lastState = state; lastTime = time;
        };
        return { draw, renderer, invalidate: () => { dirty = true; }, dispose: cleanup };
    } catch (error) { cleanup(); throw error; }
    function cleanup() {
        observer?.disconnect();
        for (const helper of helpers) { helper.geometry.dispose(); helper.material.dispose(); }
        for (const skin of skins) { skin.geometry.dispose(); skin.skeleton.dispose(); }
        for (const mesh of staticMeshes) mesh.geometry.dispose();
        for (const entry of materials) entry.dispose(); for (const texture of textures) texture.dispose();
        renderer.dispose(); renderer.domElement.remove();
    }
}
