import {BufferGeometry, Color, LinearFilter, Line, LineBasicMaterial, OrthographicCamera, Scene, SRGBColorSpace, TextureLoader, Vector3, WebGLRenderer} from 'three';
import {createSkin} from './skin.js';

export async function createPreview(host) {
    const renderer = new WebGLRenderer({antialias: true, preserveDrawingBuffer: true});
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2)); renderer.outputColorSpace = SRGBColorSpace; renderer.setClearColor(new Color('#f5f7f6'));
    renderer.domElement.setAttribute('aria-label', '同源橘猫站坐贴图预览'); host.append(renderer.domElement);
    const scene = new Scene(), camera = new OrthographicCamera(-4, 4, 6, -.5, .1, 50); camera.position.z = -12; camera.lookAt(0, 0, 0);
    let skin, texture, observer, current = {progress: 0, mode: 'skin', dark: false}, disposed = false;
    const overlays = [];
    try {
        const response = await fetch('/pet-sit-skin/atlas.json'); if (!response.ok) throw new Error('拆件清单加载失败');
        const manifest = await response.json(); texture = await new TextureLoader().loadAsync('/pet-sit-skin/atlas.png'); texture.colorSpace = SRGBColorSpace; texture.minFilter = LinearFilter; texture.generateMipmaps = false;
        if (disposed) {texture.dispose(); return;}
        skin = createSkin(manifest, texture); scene.add(skin.rig.group);
        for (const segment of skin.rig.segments) {
            const geometry = new BufferGeometry().setFromPoints([new Vector3(), new Vector3()]);
            const material = new LineBasicMaterial({color: segment.to.startsWith('far') ? '#80978a' : segment.to.startsWith('near') ? '#13745d' : '#394b43', transparent: true, opacity: .95, depthTest: false, depthWrite: false});
            const line = new Line(geometry, material); line.renderOrder = 30; line.frustumCulled = false; scene.add(line); overlays.push({line, segment});
        }
        const floor = new Line(new BufferGeometry().setFromPoints([new Vector3(-3.6, .015, 0), new Vector3(3.6, .015, 0)]), new LineBasicMaterial({color: '#c6cfca'})); scene.add(floor);
        function render(state = current) {
            if (disposed) return; current = state; skin.apply(state.progress);
            renderer.setClearColor(new Color(state.dark ? '#3a4544' : '#f5f7f6'));
            for (const mesh of skin.meshes) mesh.visible = state.mode !== 'bones';
            for (const {line, segment} of overlays) {
                line.visible = state.mode !== 'skin'; const positions = line.geometry.attributes.position;
                [segment.from, segment.to].forEach((name, index) => {const point = skin.rig.world(name); positions.setXYZ(index, point.x, point.y, 0);}); positions.needsUpdate = true;
            }
            renderer.domElement.dataset.progress = String(state.progress); renderer.domElement.dataset.mode = state.mode; renderer.render(scene, camera);
        }
        const resize = () => {
            const {width, height} = host.getBoundingClientRect(), aspect = Math.max(1, width) / Math.max(1, height), half = Math.max(3.6, 3.1 * aspect);
            camera.left = -half; camera.right = half; camera.top = 2.5 + half / aspect; camera.bottom = 2.5 - half / aspect; camera.updateProjectionMatrix();
            renderer.setSize(width, height, false); render();
        };
        observer = new ResizeObserver(resize); observer.observe(host); resize(); return {render, skin, renderer, dispose};
    } catch (error) {dispose(); throw error;}
    function dispose() {
        disposed = true; observer?.disconnect(); scene.traverse(object => {if (object.isLine) {object.geometry.dispose(); object.material.dispose();}});
        skin?.dispose(); texture?.dispose(); renderer.dispose(); renderer.domElement.remove();
    }
}
