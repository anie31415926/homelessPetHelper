import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { ArrowLeft, Flower2, Hand, RotateCcw } from 'lucide-react';
import roomUrl from '../../art-source/station-room-20261123/T_StationRoom_Background.jpg';
import { createReaction } from './reaction.js';
import { tailSwing } from './rig.js';
import { createPetRenderer } from './renderer.js';
import { FlowerBurst } from './flowers.jsx';
import './style.css';

const modes = [
    { id: 'still', text: '原图' },
    { id: 'face', text: '换表情' },
    { id: 'mesh', text: '表情＋微动' },
];

function usePetView(host, reaction, settings, lean, onLaugh) {
    const [ready, setReady] = useState('loading');
    const [face, setFace] = useState('calm');
    const [hasMesh, setHasMesh] = useState(true);
    useEffect(() => {
        const abort = new AbortController();
        const reduced = matchMedia('(prefers-reduced-motion: reduce)');
        let renderer, frame, last = 0, soft = 0, happy = 0, tilt = 0, amount = 0, wag = 0;
        let shown = 'calm', previousFace = 'calm';
        function render(now) {
            const dt = Math.min(45, now - (last || now));
            last = now;
            const state = reaction.current.sample(now);
            if (state.face === 'happy' && previousFace !== 'happy') onLaugh();
            previousFace = state.face;
            const { mode } = settings.current;
            const next = mode === 'still' ? 'calm' : state.face;
            if (next !== shown) { shown = next; setFace(next); }
            const ease = reduced.matches ? 1 : 1 - Math.exp(-dt / 34);
            soft += (Number(next === 'soft') - soft) * ease;
            happy += (Number(next === 'happy') - happy) * ease;
            const moving = !reduced.matches && mode === 'mesh';
            const motionEase = 1 - Math.exp(-dt / 90);
            tilt = moving ? tilt + (lean.current.direction - tilt) * motionEase : 0;
            amount = moving ? amount + (state.intensity * lean.current.amount - amount) * motionEase : 0;
            wag = moving ? wag + (state.wag - wag) * ease : 0;
            if (amount < .001) amount = 0;
            if (wag < .002) wag = 0;
            renderer.draw(soft, happy, tilt, amount, tailSwing(now, wag), moving);
            frame = requestAnimationFrame(render);
        }
        createPetRenderer(host.current, abort.signal).then(value => {
            if (abort.signal.aborted) { value.dispose(); return; }
            renderer = value;
            setHasMesh(value.hasMesh);
            setReady('ready');
            frame = requestAnimationFrame(render);
        }).catch(error => {
            if (!abort.signal.aborted) { console.error(error); setReady('error'); }
        });
        return () => {
            abort.abort();
            cancelAnimationFrame(frame);
            renderer?.dispose();
        };
    }, [host, reaction, settings, lean, onLaugh]);
    return { ready, face, hasMesh };
}

const zones = [
    { name: 'head', label: '抚摸豆豆的头', x: .388, y: .283, rx: .232, ry: .208 },
    { name: 'body', label: '抚摸豆豆的身子', x: .514, y: .604, rx: .330, ry: .237 },
];

function zoneAt({ x, y }) {
    return zones.find(zone => ((x - zone.x) / zone.rx) ** 2 + ((y - zone.y) / zone.ry) ** 2 <= 1);
}

function App() {
    const host = useRef(null);
    const figure = useRef(null);
    const reaction = useRef(null);
    if (!reaction.current) reaction.current = createReaction();
    const [mode, setMode] = useState('mesh');
    const [effects, setEffects] = useState(true);
    const [bursts, setBursts] = useState([]);
    const settings = useRef({ mode, effects });
    const nextBurst = useRef(0);
    const pointer = useRef(null);
    const lean = useRef({ direction: 0, amount: 0 });
    const showFlowers = useCallback(() => {
        if (!settings.current.effects) return;
        const burst = { id: ++nextBurst.current, lean: lean.current.direction };
        setBursts(previous => [...previous.slice(-2), burst]);
    }, []);
    const { ready, face, hasMesh } = usePetView(host, reaction, settings, lean, showFlowers);
    const selected = !hasMesh && mode === 'mesh' ? 'face' : mode;

    function cancelPointer() {
        const gesture = pointer.current;
        pointer.current = null;
        lean.current = { direction: 0, amount: 0 };
        reaction.current.release(performance.now());
        if (gesture?.target.hasPointerCapture(gesture.id)) gesture.target.releasePointerCapture(gesture.id);
    }

    function reset() {
        cancelPointer();
        reaction.current.reset();
        setBursts([]);
    }

    useEffect(() => {
        const stop = () => {
            if (document.hidden || !document.hasFocus()) {
                cancelPointer();
                setBursts([]);
            }
        };
        window.addEventListener('blur', stop);
        document.addEventListener('visibilitychange', stop);
        return () => {
            window.removeEventListener('blur', stop);
            document.removeEventListener('visibilitychange', stop);
        };
    }, []);

    function select(id) {
        reset();
        settings.current.mode = id;
        setMode(id);
    }

    function toggleEffects() {
        settings.current.effects = !settings.current.effects;
        setEffects(settings.current.effects);
        if (!settings.current.effects) setBursts([]);
    }

    function position(event) {
        const rect = figure.current.getBoundingClientRect();
        return { x: (event.clientX - rect.left) / rect.width, y: (event.clientY - rect.top) / rect.height };
    }

    function start(event) {
        if (ready !== 'ready' || event.button !== 0 || pointer.current) return;
        const point = position(event);
        if (!zoneAt(point)) return;
        event.currentTarget.setPointerCapture(event.pointerId);
        pointer.current = { id: event.pointerId, last: point, target: event.currentTarget };
        reaction.current.touch(performance.now());
    }

    function move(event) {
        const gesture = pointer.current;
        if (!gesture || gesture.id !== event.pointerId) return;
        const point = position(event);
        const zone = zoneAt(point);
        if (!zone) {
            gesture.last = null;
            lean.current = { direction: 0, amount: 0 };
            return;
        }
        if (gesture.last) {
            const distance = Math.hypot(point.x - gesture.last.x, point.y - gesture.last.y) * 512;
            lean.current = zone.name === 'head'
                ? { direction: Math.max(-1, Math.min(1, (point.x - zone.x) * 7)), amount: 1 }
                : { direction: 0, amount: .36 };
            const now = performance.now();
            const wasLaughing = reaction.current.sample(now).face === 'happy';
            if (reaction.current.stroke(now, distance) && wasLaughing) showFlowers();
        }
        gesture.last = point;
    }

    function end(event) {
        if (pointer.current?.id === event.pointerId) cancelPointer();
    }

    function pet() {
        if (ready !== 'ready' || pointer.current) return;
        if (reaction.current.preview(performance.now())) lean.current = { direction: .24, amount: 1 };
    }

    return (
        <main className="house" data-ready={ready} data-face={face} data-mode={selected}>
            <header className="house-header">
                <a className="icon-button" href="./index.html" title="返回" aria-label="返回"><ArrowLeft size={20} /></a>
                <h1>摸摸小屋</h1>
                <button className="icon-button effect-button" type="button" aria-pressed={effects}
                    onClick={toggleEffects} title="彩虹小花" aria-label="彩虹小花"><Flower2 size={21} /></button>
            </header>
            <section className="room" aria-label="豆豆的小屋" style={{ '--room-art': `url("${roomUrl}")` }}>
                <div className="pet-name"><span aria-hidden="true" /><h2>豆豆</h2></div>
                <div className="pet" ref={figure}>
                    <div className="contact-shadow" aria-hidden="true" />
                    <div className="pet-image" ref={host} role="img" aria-label="一只橙白水彩柴犬" />
                    <div className="flowers" aria-hidden="true">
                        {bursts.map(burst => <FlowerBurst key={burst.id} burst={burst}
                            onDone={id => setBursts(all => all.filter(item => item.id !== id))} />)}
                    </div>
                    {zones.slice().reverse().map(zone => (
                        <button type="button" key={zone.name} className={`pet-zone pet-${zone.name}`} aria-label={zone.label}
                            style={{
                                left: `${(zone.x - zone.rx) * 100}%`, top: `${(zone.y - zone.ry) * 100}%`,
                                width: `${zone.rx * 200}%`, height: `${zone.ry * 200}%`,
                            }}
                            disabled={ready !== 'ready'} onPointerDown={start} onPointerMove={move}
                            onPointerUp={end} onPointerCancel={end} onLostPointerCapture={end}
                            onClick={event => { if (event.detail === 0) pet(); }} />
                    ))}
                </div>
                {ready === 'error' && <p className="room-message" role="alert">豆豆的图片没能打开。<button onClick={() => location.reload()}>重试</button></p>}
            </section>
            <footer className="bench">
                <div className="segmented" role="group" aria-label="表现方式">
                    {modes.map(item => <button type="button" key={item.id} aria-pressed={item.id === selected}
                        disabled={item.id === 'mesh' && !hasMesh} title={item.id === 'mesh' && !hasMesh ? '此设备不支持网格渲染' : undefined}
                        onClick={() => select(item.id)}>{item.text}</button>)}
                </div>
                <button className="pet-button" type="button" disabled={ready !== 'ready'} onClick={pet}><Hand size={18} />摸摸</button>
                <button className="icon-button reset-button" type="button" onClick={reset} title="复位" aria-label="复位"><RotateCcw size={18} /></button>
            </footer>
        </main>
    );
}

createRoot(document.getElementById('root')).render(<App />);
