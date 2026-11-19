import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { Pause, Play, RotateCcw, Bone, Layers, Images, X, Heart } from 'lucide-react';
import { loadArt, renderRig, STAGE } from './renderer.js';
import { JOINT_LIMITS } from './rig.js';
import './style.css';

const text = {
    title: '猫咪动作试验', name: '小橘', front: '正面', side: '侧面',
    idle: '放松', walk: '踏步', happy: '开心', listen: '听声音', crouch: '伏低',
    standard: '标准', short: '短腿', long: '修长',
    head: '歪头', ears: '耳朵', tail: '尾巴', legs: '前爪',
};
const zero = { head: 0, ears: 0, tail: 0, legs: 0 };
const initial = { view: 'side', action: 'idle', proportion: 'standard', manual: zero, bones: false, exploded: false };

function App() {
    const canvas = useRef(null), current = useRef(initial), clock = useRef(0), art = useRef(null);
    const [state, setState] = useState(initial), [playing, setPlaying] = useState(!matchMedia('(prefers-reduced-motion: reduce)').matches);
    const playback = useRef(playing), [speed, setSpeed] = useState(1), speedRef = useRef(1);
    const [status, setStatus] = useState('loading'), [source, setSource] = useState(false), [error, setError] = useState('');
    const sourceButton = useRef(null), modalClose = useRef(null);
    current.current = state; playback.current = playing; speedRef.current = speed;
    useEffect(() => {
        let alive = true, frame, previous = 0;
        const ctx = canvas.current.getContext('2d');
        const resize = () => {
            const ratio = Math.min(devicePixelRatio || 1, 2);
            const width = STAGE.width * ratio, height = STAGE.height * ratio;
            if (canvas.current.width !== width) canvas.current.width = width;
            if (canvas.current.height !== height) canvas.current.height = height;
        };
        resize(); window.addEventListener('resize', resize);
        loadArt().then(result => {
            if (!alive) return;
            art.current = result; setStatus('ready');
            let lastState, lastTime = -1, lastWidth = 0;
            const tick = now => {
                if (!alive) return;
                const dt = previous ? Math.min((now - previous) / 1000, .05) : 0;
                previous = now;
                if (playback.current && !document.hidden) clock.current += dt * speedRef.current;
                if (lastState !== current.current || lastTime !== clock.current || lastWidth !== canvas.current.width) {
                    renderRig(ctx, result, current.current, clock.current);
                    lastState = current.current; lastTime = clock.current; lastWidth = canvas.current.width;
                }
                frame = requestAnimationFrame(tick);
            };
            frame = requestAnimationFrame(tick);
        }).catch(cause => { if (alive) { setStatus('error'); setError(cause.message); } });
        return () => { alive = false; cancelAnimationFrame(frame); window.removeEventListener('resize', resize); };
    }, []);
    useEffect(() => {
        if (source) modalClose.current?.focus();
    }, [source]);
    const patch = value => setState(old => ({ ...old, ...value }));
    const reset = () => { setState(initial); clock.current = 0; setSpeed(1); };
    const closeSource = () => { setSource(false); sourceButton.current?.focus(); };
    return <main className="rig-app" data-status={status}>
        <header className="rig-header"><div><Heart size={19} /><h1>{text.title}</h1></div><button ref={sourceButton} className="icon-button" title="查看参考图" aria-label="查看参考图" onClick={() => setSource(true)}><Images size={19} /></button></header>
        <section className="stage" aria-label="动物动作预览">
            <div className="stage-top"><span>{text.name}</span><div className="segmented" aria-label="视角">{['front', 'side'].map(view => <button key={view} aria-pressed={state.view === view} onClick={() => patch({ view })}>{text[view]}</button>)}</div></div>
            <canvas ref={canvas} width="640" height="540" aria-label="由九个图片部件组成的橘白猫" />
            {status === 'loading' && <p className="stage-message" role="status">加载中</p>}
            {status === 'error' && <p className="stage-message" role="alert">{error}<button onClick={() => location.reload()}>重新加载</button></p>}
            <div className="stage-tools"><button className="icon-button" title="显示骨骼" aria-label="显示骨骼" aria-pressed={state.bones} onClick={() => patch({ bones: !state.bones })}><Bone size={19} /></button><button className="icon-button" title="展开部件" aria-label="展开部件" aria-pressed={state.exploded} onClick={() => patch({ exploded: !state.exploded })}><Layers size={19} /></button></div>
        </section>
        <section className="controls" aria-label="动作控制">
            <div className="action-row"><div className="segmented action-tabs" aria-label="动作">{['idle', 'walk', 'happy', 'listen', 'crouch'].map(action => <button key={action} aria-pressed={state.action === action} onClick={() => patch({ action })}>{text[action]}</button>)}</div><button className="icon-button" title={playing ? '暂停' : '播放'} aria-label={playing ? '暂停' : '播放'} onClick={() => setPlaying(!playing)}>{playing ? <Pause size={18} /> : <Play size={18} />}</button></div>
            <div className="settings-row"><label>比例<select aria-label="比例预设" value={state.proportion} onChange={event => patch({ proportion: event.target.value })}>{['standard', 'short', 'long'].map(value => <option key={value} value={value}>{text[value]}</option>)}</select></label><label>速度<select aria-label="播放速度" value={speed} onChange={event => setSpeed(Number(event.target.value))}>{[.5, 1, 1.5].map(value => <option key={value} value={value}>{value}x</option>)}</select></label><button className="icon-button reset" title="复位" aria-label="复位" onClick={reset}><RotateCcw size={17} /></button></div>
            <div className="joint-controls">{Object.entries(JOINT_LIMITS).map(([joint, limit]) => <label key={joint}><span>{text[joint]}</span><input type="range" aria-label={text[joint]} min={-limit} max={limit} value={state.manual[joint]} onChange={event => patch({ manual: { ...state.manual, [joint]: Number(event.target.value) } })} /><output>{state.manual[joint]}°</output></label>)}</div>
        </section>
        {source && <div className="modal-backdrop" onClick={closeSource}><section role="dialog" aria-modal="true" aria-label="参考与分件" className="source-modal" onClick={event => event.stopPropagation()} onKeyDown={event => {
            if (event.key === 'Escape') closeSource();
            if (event.key === 'Tab') { event.preventDefault(); modalClose.current?.focus(); }
        }}><header><h2>参考与分件</h2><button ref={modalClose} className="icon-button" title="关闭" aria-label="关闭" onClick={closeSource}><X size={20} /></button></header><img src="/pet-rig/turnaround.jpg" alt="本地生成的正面与侧面猫咪参考" /><div className="atlas-pair"><img src="/pet-rig/front-atlas.png" alt="正面分件图集" /><img src="/pet-rig/side-atlas.png" alt="侧面分件图集" /></div></section></div>}
    </main>;
}
createRoot(document.getElementById('root')).render(<App />);
