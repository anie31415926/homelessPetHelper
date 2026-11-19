import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { Bone, Grid3X3, Heart, Layers, Pause, Play, RotateCcw } from 'lucide-react';
import { createPreview } from './renderer.js';
import '../pet-rig/style.css';
import './style.css';

const initial = { mode: 'mesh', action: 'together', amount: .75, manual: { head: 0, ears: 0, tail: 0, paw: 0 }, wire: false, bones: false };
const actions = { together: '综合', breathe: '呼吸', head: '歪头', ears: '动耳', tail: '摆尾', paw: '抬前腿' };
const jointLabels = { head: '头颈', ears: '耳根', tail: '尾部', paw: '抬腿' };
const limits = { head: [-10, 10], ears: [-18, 18], tail: [-20, 20], paw: [0, 100] };

function App() {
    const host = useRef(null), preview = useRef(null), stateRef = useRef(initial), clock = useRef(0);
    const [state, setState] = useState(initial), [status, setStatus] = useState('loading'), [error, setError] = useState('');
    const [playing, setPlaying] = useState(!matchMedia('(prefers-reduced-motion: reduce)').matches), playingRef = useRef(playing);
    stateRef.current = state; playingRef.current = playing;
    useEffect(() => {
        let alive = true, frame = 0, last = 0, lastDraw = 0;
        const lost = event => { event.preventDefault(); setStatus('error'); setError('画面暂时不可用'); };
        const restored = () => { setStatus('ready'); setError(''); preview.current?.invalidate(); };
        const element = host.current;
        createPreview(element).then(result => {
            if (!alive) { result.dispose(); return; }
            preview.current = result;
            result.renderer.domElement.addEventListener('webglcontextlost', lost);
            result.renderer.domElement.addEventListener('webglcontextrestored', restored);
            setStatus('ready');
            const tick = now => {
                if (!alive) return;
                const dt = last ? Math.min((now - last) / 1000, .05) : 0; last = now;
                if (playingRef.current && !document.hidden && stateRef.current.mode === 'mesh') clock.current += dt;
                if (!document.hidden && now - lastDraw >= 1000 / 30) {
                    result.draw(stateRef.current, clock.current); lastDraw = now;
                }
                frame = requestAnimationFrame(tick);
            };
            frame = requestAnimationFrame(tick);
        }).catch(cause => { if (alive) { setError(cause.message); setStatus('error'); } });
        return () => {
            alive = false; cancelAnimationFrame(frame);
            if (preview.current) {
                preview.current.renderer.domElement.removeEventListener('webglcontextlost', lost);
                preview.current.renderer.domElement.removeEventListener('webglcontextrestored', restored);
                preview.current.dispose(); preview.current = null;
            }
        };
    }, []);
    const patch = value => setState(old => ({ ...old, ...value }));
    const reset = () => { setState(initial); clock.current = 0; preview.current?.invalidate(); };
    return <main className="rig-app mesh-app" data-status={status}>
        <header className="rig-header"><div><Heart size={19} /><h1>单图网格试验</h1></div><a className="icon-button" href="/pet-rig.html" title="查看旧版分件" aria-label="查看旧版分件"><Layers size={19} /></a></header>
        <section className="stage" aria-label="单图变形预览">
            <div className="stage-top"><span>小橘</span><div className="segmented" aria-label="图片对照">{['original', 'mesh'].map(mode => <button key={mode} aria-pressed={state.mode === mode} onClick={() => patch({ mode })}>{mode === 'original' ? '原图' : '变形'}</button>)}</div></div>
            <div className="mesh-canvas" ref={host} />
            {status === 'loading' && <p className="stage-message" role="status">加载中</p>}
            {status === 'error' && <p className="stage-message" role="alert">{error}<button onClick={() => location.reload()}>重新加载</button></p>}
            <div className="stage-tools"><button className="icon-button" title="显示网格" aria-label="显示网格" aria-pressed={state.wire} disabled={state.mode !== 'mesh'} onClick={() => patch({ wire: !state.wire })}><Grid3X3 size={19} /></button><button className="icon-button" title="显示骨骼" aria-label="显示骨骼" aria-pressed={state.bones} disabled={state.mode !== 'mesh'} onClick={() => patch({ bones: !state.bones })}><Bone size={19} /></button></div>
        </section>
        <section className="controls" aria-label="形变控制">
            <div className="settings-row"><label>动作<select aria-label="动作" value={state.action} disabled={state.mode !== 'mesh'} onChange={event => patch({ action: event.target.value })}>{Object.entries(actions).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label><label className="amount">幅度<input aria-label="动作幅度" type="range" min="0" max="100" value={Math.round(state.amount * 100)} disabled={state.mode !== 'mesh'} onChange={event => patch({ amount: Number(event.target.value) / 100 })} /></label><button className="icon-button" title={playing ? '暂停' : '播放'} aria-label={playing ? '暂停' : '播放'} disabled={state.mode !== 'mesh'} onClick={() => setPlaying(!playing)}>{playing ? <Pause size={18} /> : <Play size={18} />}</button><button className="icon-button" title="复位" aria-label="复位" onClick={reset}><RotateCcw size={17} /></button></div>
            <div className="joint-controls">{Object.entries(limits).map(([joint, [min, max]]) => <label key={joint}><span>{jointLabels[joint]}</span><input type="range" aria-label={jointLabels[joint]} min={min} max={max} value={state.manual[joint]} disabled={state.mode !== 'mesh'} onChange={event => patch({ manual: { ...state.manual, [joint]: Number(event.target.value) } })} /><output>{state.manual[joint]}{joint === 'paw' ? '%' : '°'}</output></label>)}</div>
        </section>
    </main>;
}
createRoot(document.getElementById('root')).render(<App />);
