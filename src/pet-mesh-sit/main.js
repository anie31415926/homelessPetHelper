import {BufferGeometry, Line, LineBasicMaterial, OrthographicCamera, Scene, SRGBColorSpace, TextureLoader, Vector3, WebGLRenderer} from 'three';
import {createChibiSkin} from '../pet-sit-chibi/skin.js';
import {attachJoinedFore, attachJoinedHind, liftForepaw} from './join.js';
import {createContour} from './contour.js';
import {attachRumpCorrective} from './rump.js';
import {initSideStudy} from './side.js';
import {attachDeink} from './deink.js';
import {attachRepartition} from './partition.js';
import {createSeatContact} from './seat-contact.js';
import {createPawContact} from './paw-contact.js';
import './style.css';

const main = document.querySelector('main');
const hosts = [...document.querySelectorAll('.stage')];
const slider = document.querySelector('#progress');
const output = document.querySelector('output');
const play = document.querySelector('#play');
const action = document.querySelector('#action');
const grid = document.querySelector('#grid');
const dark = document.querySelector('#dark');
const ink = document.querySelector('#ink');
const rump = document.querySelector('#rump');
const deink = document.querySelector('#deink');
const partition = document.querySelector('#partition');
const seat = document.querySelector('#seat');
const pawSeat = document.querySelector('#paw-seat');
const pressed = button => button.getAttribute('aria-pressed') === 'true';
const setPressed = (button, value) => button.setAttribute('aria-pressed', String(value));
initSideStudy(document.querySelector('#side-study'));

function target(host) {
    const renderer = new WebGLRenderer({alpha: true, antialias: true, preserveDrawingBuffer: true});
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.domElement.setAttribute('aria-label', host.getAttribute('aria-label'));
    host.appendChild(renderer.domElement);
    const camera = new OrthographicCamera(-3, 3, 5.3, -.35, .1, 30);
    camera.position.z = -12; camera.lookAt(0, 0, 0);
    function resize() {
        const {clientWidth: w, clientHeight: h} = host, aspect = w / h;
        const half = Math.max(2.85, 2.74 / aspect);
        camera.left = -half * aspect; camera.right = half * aspect;
        camera.top = 2.48 + half; camera.bottom = 2.48 - half;
        camera.updateProjectionMatrix();
        host.style.setProperty('--ground', (camera.top / (half * 2) * 100) + '%');
        renderer.setSize(w, h, false);
    }
    return {camera, renderer, resize};
}

function bonesFor(scene, rig) {
    return rig.segments.map(({from, to}) => {
        const line = new Line(new BufferGeometry().setFromPoints([new Vector3(), new Vector3()]),
            new LineBasicMaterial({color: to.startsWith('far') ? '#80978a' : '#158259', depthTest: false}));
        line.renderOrder = 30; line.frustumCulled = false; scene.add(line);
        return visible => {
            line.visible = visible;
            if (!visible) return;
            const p = line.geometry.attributes.position;
            [from, to].forEach((id, i) => {const v = rig.world(id); p.setXYZ(i, v.x, v.y, 0);});
            p.needsUpdate = true;
        };
    });
}

async function init() {
    const response = await fetch('/pet-sit-chibi/atlas.json');
    if (!response.ok) throw Error('拆件数据未加载');
    const manifest = await response.json();
    const texture = await new TextureLoader().loadAsync('/pet-sit-chibi/atlas.png');
    const source = await new TextureLoader().loadAsync('/pet-mesh/cat-side.png');
    texture.colorSpace = SRGBColorSpace;
    const skin = createChibiSkin(manifest, texture);
    const scene = new Scene(); scene.add(skin.rig.group);
    const hind = attachJoinedHind(skin, texture, manifest);
    const joined = [...hind, ...attachJoinedFore(skin, manifest)];
    const repartition = attachRepartition(skin, hind[1], texture, manifest, source.image);
    const rollPaw = createPawContact(skin, manifest);
    const settle = createSeatContact([
        skin.meshes.find(mesh => mesh.name === 'torso'),
        skin.meshes.find(mesh => mesh.name === 'nearThigh'),
        skin.meshes.find(mesh => mesh.name === 'tail'),
        hind[1]
    ], hind[1]);
    const tail = skin.meshes.find(mesh => mesh.name === 'tail'), tailMaterial = tail.material;
    const cleanStrokes = attachDeink(skin, hind[1], texture, manifest);
    const correctRump = attachRumpCorrective(skin, texture, manifest);
    const originals = skin.meshes.filter(mesh => !mesh.visible);
    const views = hosts.map(target), boneLines = bonesFor(scene, skin.rig);
    const drawContour = createContour(hosts[1], skin.skeleton);
    let animation = 0;

    function updateContour() {
        const {renderer, camera} = views[1];
        const active = pressed(ink) && !pressed(deink) && action.value === 'sit' && !pressed(grid);
        drawContour(renderer.domElement, camera, active ? Number(slider.value) / 100 : 0, pressed(rump));
    }

    function draw() {
        const value = Number(slider.value), progress = value / 100;
        skin.apply(action.value === 'sit' ? progress : 0);
        if (action.value === 'paw') {
            liftForepaw(skin.rig, progress);
            skin.skeleton.update();
        }
        const overlay = pressed(grid);
        for (const setVisible of boneLines) setVisible(overlay);
        for (const [index, {camera, renderer}] of views.entries()) {
            const next = index === 1 && pressed(partition);
            rollPaw(next && pressed(pawSeat) && action.value === 'sit', progress);
            tail.material = tailMaterial;
            cleanStrokes(!next && index === 1 && pressed(deink) && action.value === 'sit', progress);
            correctRump(!next && index === 1 && pressed(rump) && action.value === 'sit', progress);
            repartition(next);
            settle(next && pressed(seat) && action.value === 'sit', progress);
            for (const mesh of originals) mesh.visible = index === 0;
            for (const mesh of joined) mesh.visible = index === 1;
            renderer.render(scene, camera);
        }
        updateContour();
        output.textContent = value + '%';
    }

    function pause() {
        cancelAnimationFrame(animation);
        setPressed(play, false); play.textContent = '播放';
    }

    play.addEventListener('click', () => {
        if (pressed(play)) { pause(); return; }
        setPressed(play, true); play.textContent = '暂停';
        const start = performance.now();
        const tick = now => {
            const t = (now - start) % 4100;
            const p = action.value === 'paw' ? Math.min(1, t / 2600)
                : t < 1350 ? t / 1350 : t < 2350 ? 1 : Math.max(0, 1 - (t - 2350) / 1350);
            slider.value = Math.round(p * 100);
            draw(); animation = requestAnimationFrame(tick);
        };
        animation = requestAnimationFrame(tick);
    });
    slider.addEventListener('input', () => {pause(); draw();});
    action.addEventListener('change', () => {pause(); slider.value = '0'; draw();});
    ink.addEventListener('click', () => {
        setPressed(ink, !pressed(ink));
        if (pressed(ink) && (pressed(deink) || pressed(partition))) {
            setPressed(deink, false); setPressed(partition, false); draw();
        }
        else updateContour();
    });
    seat.addEventListener('click', () => {
        setPressed(seat, !pressed(seat));
        if (pressed(seat) && !pressed(partition)) {
            setPressed(partition, true);
            for (const other of [ink, deink, rump]) setPressed(other, false);
        }
        draw();
    });
    for (const control of [grid, dark, rump, deink, partition, pawSeat]) control.addEventListener('click', () => {
        setPressed(control, !pressed(control));
        if (control === deink && pressed(deink)) setPressed(ink, false);
        if (control === partition && pressed(partition)) for (const other of [ink, deink, rump]) setPressed(other, false);
        if ((control === deink || control === rump) && pressed(control)) setPressed(partition, false);
        if (control === dark) for (const host of hosts) host.classList.toggle('dark', pressed(dark));
        draw();
    });
    const resize = () => { for (const view of views) view.resize(); draw(); };
    const observer = new ResizeObserver(resize);
    for (const host of hosts) observer.observe(host);
    resize();
    main.dataset.status = 'ready';
}

init().catch(error => {
    const message = document.querySelector('#error');
    main.dataset.status = 'error';
    message.hidden = false;
    message.textContent = '预览未加载：' + error.message;
    for (const control of [slider, play, action, grid, dark, ink, rump, deink, partition, seat, pawSeat]) control.disabled = true;
});
