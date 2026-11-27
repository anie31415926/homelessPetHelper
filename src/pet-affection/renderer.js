import {
    CanvasTexture, LinearFilter, Mesh, MeshBasicMaterial, OrthographicCamera,
    PlaneGeometry, Scene, SRGBColorSpace, Texture, WebGLRenderer,
} from 'three';
import { deformPoint, mountTailPlane, tailArt } from './rig.js';

const size = 512;
const patch = { x: 108, y: 107, width: 181, height: 133 };

async function loadImage(filename) {
    const image = new Image();
    image.src = `${import.meta.env.BASE_URL}pet-affection/${filename}`;
    await image.decode();
    return image;
}

function makeSurface(base, atlas, layers) {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = size;
    const context = canvas.getContext('2d');
    const paint = (soft, happy, split = false) => {
        context.globalAlpha = 1;
        context.clearRect(0, 0, size, size);
        context.drawImage(split ? layers : base, 0, 0, size, size, 0, 0, size, size);
        for (const [cell, opacity] of [soft, happy].entries()) {
            if (opacity < .008) continue;
            context.globalAlpha = opacity;
            context.drawImage(atlas, cell * patch.width, 0, patch.width, patch.height,
                patch.x, patch.y, patch.width, patch.height);
        }
        context.globalAlpha = 1;
    };
    return { canvas, paint };
}

function makeTail(layers) {
    const { crop, atlas } = tailArt;
    const map = new Texture(layers);
    map.colorSpace = SRGBColorSpace;
    map.minFilter = LinearFilter;
    map.generateMipmaps = false;
    map.needsUpdate = true;
    const material = new MeshBasicMaterial({ map, transparent: true, depthWrite: false, depthTest: false });
    function makeLayer(top, order) {
        const geometry = new PlaneGeometry(crop.width / size, crop.height / size);
        const uv = geometry.getAttribute('uv');
        for (let i = 0; i < uv.count; i++) {
            uv.setXY(i, (atlas.tailX + uv.getX(i) * crop.width) / atlas.width,
                1 - (top + (1 - uv.getY(i)) * crop.height) / atlas.height);
        }
        const layer = new Mesh(geometry, material);
        layer.renderOrder = order;
        return layer;
    }

    const plane = makeLayer(0, 1);
    const fixedFur = makeLayer(atlas.rootY, 2);
    fixedFur.position.set((crop.x + crop.width / 2) / size - .5,
        .5 - (crop.y + crop.height / 2) / size, 0);
    return {
        ...mountTailPlane(plane),
        fixedFur,
        dispose() { map.dispose(); material.dispose(); plane.geometry.dispose(); fixedFur.geometry.dispose(); },
    };
}

function mountMesh(host, canvas, layers) {
    const renderer = new WebGLRenderer({ alpha: true, antialias: true });
    const texture = new CanvasTexture(canvas);
    texture.colorSpace = SRGBColorSpace;
    texture.minFilter = LinearFilter;
    texture.generateMipmaps = false;
    const material = new MeshBasicMaterial({ map: texture, transparent: true, depthWrite: false });
    const geometry = new PlaneGeometry(1, 1, 32, 32);
    const scene = new Scene();
    const body = new Mesh(geometry, material);
    const tail = makeTail(layers);
    tail.root.visible = false;
    tail.fixedFur.visible = false;
    scene.add(body, tail.root, tail.fixedFur);
    const camera = new OrthographicCamera(-.5, .5, .5, -.5, .01, 10);
    camera.position.z = 2;
    renderer.setPixelRatio(Math.min(devicePixelRatio || 1, 2));
    host.replaceChildren(renderer.domElement);
    const observer = new ResizeObserver(([entry]) => {
        const { width, height } = entry.contentRect;
        renderer.setSize(Math.max(1, width), Math.max(1, height), false);
        renderer.render(scene, camera);
    });
    observer.observe(host);
    const position = geometry.getAttribute('position');
    const uv = geometry.getAttribute('uv');
    return {
        draw(lean, intensity, swing, split, changed, headMoved) {
            if (headMoved) {
                for (let i = 0; i < position.count; i++) {
                    const point = deformPoint(uv.getX(i), 1 - uv.getY(i), lean, intensity);
                    position.setXY(i, point.x - .5, .5 - point.y);
                }
                position.needsUpdate = true;
            }
            tail.root.visible = split;
            tail.fixedFur.visible = split;
            tail.pose(swing);
            if (changed) texture.needsUpdate = true;
            renderer.render(scene, camera);
        },
        dispose() {
            observer.disconnect();
            tail.dispose();
            texture.dispose();
            material.dispose();
            geometry.dispose();
            renderer.dispose();
            renderer.domElement.remove();
        },
    };
}

export async function createPetRenderer(host, signal) {
    const [base, atlas, layers] = await Promise.all([
        loadImage('shiba.webp'), loadImage('faces.webp'), loadImage('shiba-tail-layers.webp'),
    ]);
    signal.throwIfAborted();
    const surface = makeSurface(base, atlas, layers);
    surface.paint(0, 0);
    let mesh;
    try { mesh = mountMesh(host, surface.canvas, layers); }
    catch (error) {
        if (!String(error.message).includes('WebGL')) throw error;
        host.replaceChildren(surface.canvas);
    }
    let previousSoft = -1, previousHappy = -1, previousLean = -1, previousAmount = -1;
    let lastSwing = 0, lastSplit = false;
    return {
        hasMesh: !!mesh,
        draw(soft, happy, lean, amount, swing, moving) {
            const split = !!mesh && moving;
            const changed = split !== lastSplit || Math.abs(soft - previousSoft) + Math.abs(happy - previousHappy) > .005;
            const headMoved = Math.abs(lean - previousLean) + Math.abs(amount - previousAmount) > .002
                || (amount === 0 && previousAmount !== 0);
            const tailMoved = Math.abs(swing - lastSwing) > .002 || (swing === 0 && lastSwing !== 0);
            if (changed) {
                surface.paint(soft, happy, split);
                previousSoft = soft;
                previousHappy = happy;
                lastSplit = split;
            }
            if (mesh && (changed || headMoved || tailMoved)) {
                mesh.draw(lean, amount, swing, split, changed, headMoved);
                if (headMoved) { previousLean = lean; previousAmount = amount; }
                lastSwing = swing;
            }
        },
        dispose() { mesh?.dispose(); surface.canvas.remove(); },
    };
}
