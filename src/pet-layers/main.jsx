import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { Bone, Eye, EyeOff, Footprints, Grid3X3, Heart, Layers, Pause, Play, RotateCcw } from 'lucide-react';
import { createPreview as createLegacyPreview } from './renderer.js';
import { createPreview as createCompletePreview } from './complete-renderer.js';
import { MotionApp } from './motion-app.jsx';
import '../pet-rig/style.css';
import '../pet-mesh/style.css';

const legacy = new URLSearchParams(location.search).has('legacy');
const createPreview = legacy ? createLegacyPreview : createCompletePreview;
const initial = { mode: 'layers', completion: legacy ? 'ai' : 'copy', target: 'both', action: 'swing', amount: .8, front: true, wire: false, bones: false, manual: { hip: 0, knee: 0 } };
const actions = { rest: '原位', forward: '前伸', tuck: '收腿', swing: '摆动' };

function App() {
    const host = useRef(null), preview = useRef(null), stateRef = useRef(initial), clock = useRef(0);
    const [state, setState] = useState(initial), [status, setStatus] = useState('loading'), [error, setError] = useState('');
    const [playing, setPlaying] = useState(!matchMedia('(prefers-reduced-motion: reduce)').matches), playback = useRef(playing);
    stateRef.current = state; playback.current = playing;
    useEffect(() => {
        let alive = true, frame = 0, last = 0, lastDraw = 0;
        const lost = event => { event.preventDefault(); setStatus('error'); setError('画面暂时不可用'); };
        const restored = () => { setStatus('ready'); preview.current?.invalidate(); };
        createPreview(host.current).then(result => {
            if (!alive) { result.dispose(); return; }
            preview.current = result; setStatus('ready');
            result.renderer.domElement.addEventListener('webglcontextlost', lost);
            result.renderer.domElement.addEventListener('webglcontextrestored', restored);
            const tick = now => {
                if (!alive) return;
                const dt = last ? Math.min((now - last) / 1000, .05) : 0; last = now;
                if (playback.current && !document.hidden && stateRef.current.mode === 'layers') clock.current += dt;
                if (!document.hidden && now - lastDraw >= 1000 / 30) { result.draw(stateRef.current, clock.current); lastDraw = now; }
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
    const disabled = state.mode !== 'layers';
    return <main className="rig-app mesh-app" data-status={status}>
        <header className="rig-header"><div><Heart size={19} /><h1>{legacy ? '远侧腿分层试验' : '完整前后腿试验'}</h1></div><nav aria-label="试验切换"><a className="icon-button" href="/pet-layers.html?motion=1" title="查看整身动作" aria-label="查看整身动作"><Footprints size={19} /></a><a className="icon-button" href={legacy ? '/pet-layers.html' : '/pet-layers.html?legacy=1'} title={legacy ? '查看完整前后腿版' : '查看旧后腿版'} aria-label={legacy ? '查看完整前后腿版' : '查看旧后腿版'}><Layers size={19} /></a></nav></header>
        <section className="stage" aria-label={legacy ? '远后腿动作预览' : '完整前后腿动作预览'}>
            <div className="stage-top"><span>小橘</span><div className="segmented" aria-label="分层对照">{['original', 'layers'].map(mode => <button key={mode} aria-pressed={state.mode === mode} onClick={() => patch({ mode })}>{mode === 'original' ? '原图' : '分层'}</button>)}</div></div>
            <div className="mesh-canvas" ref={host} />
            {status === 'loading' && <p className="stage-message" role="status">加载中</p>}
            {status === 'error' && <p className="stage-message" role="alert">{error}<button onClick={() => location.reload()}>重新加载</button></p>}
            <div className="stage-tools"><button className="icon-button" title="显示前景" aria-label="显示前景" disabled={disabled} aria-pressed={state.front} onClick={() => patch({ front: !state.front })}>{state.front ? <Eye size={19} /> : <EyeOff size={19} />}</button><button className="icon-button" title="显示网格" aria-label="显示网格" disabled={disabled} aria-pressed={state.wire} onClick={() => patch({ wire: !state.wire })}><Grid3X3 size={19} /></button><button className="icon-button" title="显示骨骼" aria-label="显示骨骼" disabled={disabled} aria-pressed={state.bones} onClick={() => patch({ bones: !state.bones })}><Bone size={19} /></button></div>
        </section>
        <section className="controls" aria-label={legacy ? '远后腿控制' : '完整前后腿控制'}>
            <div className="settings-row">{legacy ? <label>补全<select aria-label="补全来源" value={state.completion} disabled={disabled} onChange={event => patch({ completion: event.target.value })}><option value="ai">模型补图</option><option value="donor">同图取样</option><option value="before">清理前对照</option></select></label> : <><label>来源<select aria-label="完整腿来源" value={state.completion} disabled={disabled} onChange={event => patch({ completion: event.target.value })}><option value="copy">同图复制</option><option value="ai">旧模型版</option></select></label><label>部位<select aria-label="控制部位" value={state.target} disabled={disabled} onChange={event => patch({ target: event.target.value })}><option value="both">前后腿</option><option value="front">前腿</option><option value="hind">后腿</option></select></label></>}</div>
            <div className="settings-row"><label>动作<select aria-label="动作" value={state.action} disabled={disabled} onChange={event => patch({ action: event.target.value })}>{Object.entries(actions).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label><label className="amount">幅度<input aria-label="动作幅度" type="range" min="0" max="100" value={Math.round(state.amount * 100)} disabled={disabled} onChange={event => patch({ amount: Number(event.target.value) / 100 })} /></label><button className="icon-button" title={playing ? '暂停' : '播放'} aria-label={playing ? '暂停' : '播放'} disabled={disabled} onClick={() => setPlaying(!playing)}>{playing ? <Pause size={18} /> : <Play size={18} />}</button><button className="icon-button" title="复位" aria-label="复位" onClick={reset}><RotateCcw size={17} /></button></div>
            <div className="joint-controls">{[['hip', '腿根', -25, 25], ['knee', '屈膝', 0, 40]].map(([joint, label, min, max]) => <label key={joint}><span>{label}</span><input aria-label={label} type="range" min={min} max={max} value={state.manual[joint]} disabled={disabled} onChange={event => patch({ manual: { ...state.manual, [joint]: Number(event.target.value) } })} /><output>{state.manual[joint]}°</output></label>)}</div>
        </section>
    </main>;
}
createRoot(document.getElementById('root')).render(new URLSearchParams(location.search).has('motion') ? <MotionApp /> : <App />);
