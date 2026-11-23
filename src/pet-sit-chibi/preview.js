import {BufferGeometry, Color, DoubleSide, LinearFilter, Line, LineBasicMaterial, Mesh, MeshBasicMaterial, OrthographicCamera, PlaneGeometry, Scene, SRGBColorSpace, TextureLoader, Vector3, WebGLRenderer} from 'three';
import {createChibiSkin} from './skin.js';

export async function createPreview(host) {
    const renderer = new WebGLRenderer({antialias: true, preserveDrawingBuffer: true}); renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2)); renderer.outputColorSpace = SRGBColorSpace;
    renderer.domElement.setAttribute('aria-label', '短腿萌系原图比例坐下预览'); host.append(renderer.domElement);
    const scene = new Scene(), camera = new OrthographicCamera(-3, 3, 5.5, -.5, .1, 50); camera.position.z = -12; camera.lookAt(0, 0, 0);
    let skin, texture, source, sourceMesh, observer, current = {progress: 0, mode: 'skin', dark: false}, disposed = false; const overlays = [];
    try {
        const response = await fetch('/pet-sit-chibi/atlas.json'); if (!response.ok) throw new Error('萌系拆件加载失败'); const manifest = await response.json();
        texture = await new TextureLoader().loadAsync('/pet-sit-chibi/atlas.png'); texture.colorSpace = SRGBColorSpace; texture.minFilter = LinearFilter; texture.generateMipmaps = false;
        source = await new TextureLoader().loadAsync('/pet-mesh/cat-side.png'); source.colorSpace = SRGBColorSpace; source.minFilter = LinearFilter; source.generateMipmaps = false;
        if (disposed) {texture.dispose(); source.dispose(); return;}
        skin = createChibiSkin(manifest, texture); scene.add(skin.rig.group);
        const geometry = new PlaneGeometry(4.52, 5.12), positions = geometry.attributes.position;
        for (let i = 0; i < positions.count; i++) positions.setX(i, -positions.getX(i));
        sourceMesh = new Mesh(geometry, new MeshBasicMaterial({map: source, side: DoubleSide, transparent: true, depthWrite: false, depthTest: false})); sourceMesh.position.y = 2.4; sourceMesh.renderOrder = 11; scene.add(sourceMesh);
        for (const segment of skin.rig.segments) {
            const geometry = new BufferGeometry().setFromPoints([new Vector3(), new Vector3()]), material = new LineBasicMaterial({color: segment.to.startsWith('far') ? '#80978a' : segment.to.startsWith('near') ? '#13745d' : '#394b43', transparent: true, opacity: .95, depthTest: false, depthWrite: false});
            const line = new Line(geometry, material); line.renderOrder = 30; line.frustumCulled = false; scene.add(line); overlays.push({line, segment});
        }
        scene.add(new Line(new BufferGeometry().setFromPoints([new Vector3(-3.2, 0, 0), new Vector3(3.2, 0, 0)]), new LineBasicMaterial({color: '#c6cfca'})));
        function render(state = current) {
            if (disposed) return; current = state; skin.apply(state.progress); renderer.setClearColor(new Color(state.dark ? '#3a4544' : '#f5f7f6'));
            sourceMesh.visible = state.mode === 'source'; for (const mesh of skin.meshes) mesh.visible = state.mode !== 'bones' && state.mode !== 'source';
            for (const {line, segment} of overlays) {line.visible = state.mode === 'overlay' || state.mode === 'bones'; const positions = line.geometry.attributes.position; [segment.from, segment.to].forEach((name, i) => {const point = skin.rig.world(name); positions.setXYZ(i, point.x, point.y, 0);}); positions.needsUpdate = true;}
            renderer.domElement.dataset.progress = String(state.progress); renderer.domElement.dataset.mode = state.mode; renderer.render(scene, camera);
        }
        const resize = () => {
            const {width, height} = host.getBoundingClientRect(), aspect = Math.max(1, width) / Math.max(1, height), half = Math.max(3.0, 2.95 * aspect);
            camera.left = -half; camera.right = half; camera.top = 2.38 + half / aspect; camera.bottom = 2.38 - half / aspect; camera.updateProjectionMatrix(); renderer.setSize(width, height, false); render();
        };
        observer = new ResizeObserver(resize); observer.observe(host); resize(); return {render, skin, dispose};
    } catch (error) {dispose(); throw error;}
    function dispose() {
        disposed = true; observer?.disconnect(); scene.traverse(object => {if (object.isLine) {object.geometry.dispose(); object.material.dispose();}});
        skin?.dispose(); sourceMesh?.geometry.dispose(); sourceMesh?.material.dispose(); texture?.dispose(); source?.dispose(); renderer.dispose(); renderer.domElement.remove();
    }
}
