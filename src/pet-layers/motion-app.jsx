import React, { useEffect, useRef, useState } from 'react';
import { Bone, Eye, EyeOff, Grid3X3, Heart, Layers, Pause, Play, RotateCcw } from 'lucide-react';
import { createMotionPreview } from './motion-renderer.js';
import { MOTIONS } from './motion.js';
import './motion-style.css';

const requested = new URLSearchParams(location.search).get('action');
const legacy = new URLSearchParams(location.search).has('legacyPose');
const actions = Object.keys(MOTIONS).filter(action => legacy || !['lie', 'sitMesh'].includes(action));
const initial = { mode: 'motion', action: actions.includes(requested) ? requested : 'tread', amount: .8, speed: 1, body: true, wire: false, bones: false };
const names = { idle: '呼吸休息', look: '张望摆尾', tread: '原地踏步', walk: '来回走动', sit: '坐姿休息', side: '侧卧休息', sitMesh: '旧网格坐姿', lie: '旧趴下对照' };
export function MotionApp() {
    const host = useRef(null), preview = useRef(null), stateRef = useRef(initial), clock = useRef(0), timeline = useRef(null);
    const [state, setState] = useState(initial), [status, setStatus] = useState('loading'), [error, setError] = useState('');
    const [playing, setPlaying] = useState(!matchMedia('(prefers-reduced-motion: reduce)').matches), playback = useRef(playing);
    stateRef.current = state; playback.current = playing;
    useEffect(() => {
        let alive = true, frame = 0, last = 0, lastDraw = 0;
        const lost = event => { event.preventDefault(); setStatus('error'); setError('画面暂时不可用'); setPlaying(false); };
        const restored = () => { setStatus('ready'); preview.current?.invalidate(); };
        const visibility = () => { last = 0; };
        const media = matchMedia('(prefers-reduced-motion: reduce)'), preference = () => { if (media.matches) setPlaying(false); };
        document.addEventListener('visibilitychange', visibility); media.addEventListener('change', preference);
        createMotionPreview(host.current).then(result => {
            if (!alive) { result.dispose(); return; }
            preview.current = result; setStatus('ready');
            result.renderer.domElement.addEventListener('webglcontextlost', lost); result.renderer.domElement.addEventListener('webglcontextrestored', restored);
            const tick = now => {
                if (!alive) return;
                const dt = last ? Math.min((now - last) / 1000, .05) : 0; last = now;
                if (playback.current && !document.hidden && stateRef.current.mode === 'motion') clock.current += dt * stateRef.current.speed;
                if (!document.hidden && now - lastDraw >= 1000 / 30) {
                    result.draw(stateRef.current, clock.current); lastDraw = now;
                    if (timeline.current) timeline.current.value = String(Math.round(clock.current % MOTIONS[stateRef.current.action] / MOTIONS[stateRef.current.action] * 1000));
                }
                frame = requestAnimationFrame(tick);
            };
            frame = requestAnimationFrame(tick);
        }).catch(cause => { if (alive) { setError(cause.message); setStatus('error'); } });
        return () => {
            alive = false; cancelAnimationFrame(frame); document.removeEventListener('visibilitychange', visibility); media.removeEventListener('change', preference);
            if (preview.current) {
                preview.current.renderer.domElement.removeEventListener('webglcontextlost', lost); preview.current.renderer.domElement.removeEventListener('webglcontextrestored', restored);
                preview.current.dispose(); preview.current = null;
            }
        };
    }, []);
    const patch = value => setState(old => ({ ...old, ...value }));
    const chooseAction = action => { clock.current = 0; patch({ action, body: ['side', 'sit'].includes(action) ? true : state.body }); preview.current?.invalidate(); };
    const reset = () => { clock.current = 0; setState(initial); preview.current?.invalidate(); };
    const disabled = state.mode !== 'motion';
    return <main className="rig-app mesh-app motion-app" data-status={status}>
        <header className="rig-header"><div><Heart size={19} /><h1>整身动作试验</h1></div><a className="icon-button" href="/pet-layers.html" title="返回腿部对照" aria-label="返回腿部对照"><Layers size={19} /></a></header>
        <section className="stage" aria-label="整身动作预览">
            <div className="stage-top"><span>小橘</span><div className="segmented" aria-label="动作对照">{['original', 'motion'].map(mode => <button key={mode} aria-pressed={state.mode === mode} onClick={() => patch({ mode })}>{mode === 'original' ? '原图' : '动作'}</button>)}</div></div>
            <div className="mesh-canvas" ref={host} />
            {status === 'loading' && <p className="stage-message" role="status">加载中</p>}
            {status === 'error' && <p className="stage-message" role="alert">{error}<button onClick={() => location.reload()}>重新加载</button></p>}
        </section>
        <section className="controls" aria-label="整身动作控制">
            <div className="settings-row"><label>动作<select aria-label="整身动作" value={state.action} disabled={disabled} onChange={event => chooseAction(event.target.value)}>{actions.map(id => <option key={id} value={id}>{names[id]}</option>)}</select></label><label className="amount">幅度<input aria-label="动作幅度" type="range" min="0" max="100" value={Math.round(state.amount * 100)} disabled={disabled} onChange={event => patch({ amount: Number(event.target.value) / 100 })} /></label><button className="icon-button" title={playing ? '暂停' : '播放'} aria-label={playing ? '暂停' : '播放'} disabled={disabled} onClick={() => setPlaying(!playing)}>{playing ? <Pause size={18} /> : <Play size={18} />}</button><button className="icon-button" title="复位" aria-label="复位" onClick={reset}><RotateCcw size={17} /></button></div>
            <div className="settings-row"><label className="amount">速度<input aria-label="播放速度" type="range" min="50" max="150" step="5" value={Math.round(state.speed * 100)} disabled={disabled} onChange={event => patch({ speed: Number(event.target.value) / 100 })} /></label><output>{state.speed.toFixed(2)}x</output></div>
            <div className="settings-row"><label className="amount">进度<input ref={timeline} aria-label="动作进度" type="range" min="0" max="1000" defaultValue="0" disabled={disabled} onChange={event => { setPlaying(false); clock.current = MOTIONS[state.action] * Number(event.target.value) / 1000; preview.current?.invalidate(); }} /></label><div className="motion-view-tools"><button className="icon-button" title="显示身体" aria-label="显示身体" disabled={disabled} aria-pressed={state.body} onClick={() => patch({ body: !state.body })}>{state.body ? <Eye size={19} /> : <EyeOff size={19} />}</button><button className="icon-button" title="显示网格" aria-label="显示网格" disabled={disabled} aria-pressed={state.wire} onClick={() => patch({ wire: !state.wire })}><Grid3X3 size={19} /></button><button className="icon-button" title="显示骨骼" aria-label="显示骨骼" disabled={disabled} aria-pressed={state.bones} onClick={() => patch({ bones: !state.bones })}><Bone size={19} /></button></div></div>
        </section>
    </main>;
}
