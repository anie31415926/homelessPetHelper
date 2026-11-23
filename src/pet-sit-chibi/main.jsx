import React, {useEffect, useRef, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {Pause, Play, RotateCcw, StepBack, StepForward} from 'lucide-react';
import {DURATION, KEY_POSES} from './rig.js';
import {samplePose} from '../pet-sit-study/rig.js';
import {createPreview} from './preview.js';
import '../pet-sit-study/style.css';
import './style.css';

function App() {
    const host = useRef(null), preview = useRef(null);
    const [progress, setProgress] = useState(0), [playing, setPlaying] = useState(false), [speed, setSpeed] = useState(.5), [mode, setMode] = useState('skin'), [dark, setDark] = useState(false), [ready, setReady] = useState(false), [error, setError] = useState('');
    useEffect(() => {let cancelled = false; createPreview(host.current).then(result => {if (cancelled) result?.dispose(); else {preview.current = result; setReady(true);}}).catch(reason => {if (!cancelled) setError(`预览无法启动：${reason.message}`);}); return () => {cancelled = true; preview.current?.dispose(); preview.current = null;};}, []);
    useEffect(() => {preview.current?.render({progress, mode, dark});}, [progress, mode, dark, ready]);
    useEffect(() => {if (!playing) return; let frame, previous; const tick = now => {const delta = previous === undefined ? 0 : Math.min(.06, (now - previous) / 1000); previous = now; setProgress(value => Math.min(1, value + delta * speed / DURATION)); frame = requestAnimationFrame(tick);}; frame = requestAnimationFrame(tick); return () => cancelAnimationFrame(frame);}, [playing, speed]);
    useEffect(() => {if (progress === 1) setPlaying(false);}, [progress]);
    useEffect(() => {const pause = () => {if (document.hidden) setPlaying(false);}; document.addEventListener('visibilitychange', pause); return () => document.removeEventListener('visibilitychange', pause);}, []);
    const seek = value => {setPlaying(false); setProgress(Math.max(0, Math.min(1, value))); if (mode === 'source') setMode('skin');};
    const changeMode = value => {setPlaying(false); setMode(value);};
    return <main>
        <header><h1>猫坐下 <span>原图萌系比例</span></h1><span className="phase">{mode === 'source' ? '原图' : samplePose(progress).name}</span></header>
        <div className="stage"><div className="canvas-host" ref={host}/><div className="view-modes" role="group" aria-label="预览模式">{[['skin', '贴图'], ['overlay', '叠骨'], ['bones', '骨架'], ['source', '原图']].map(([value, name]) => <button key={value} aria-pressed={mode === value} onClick={() => changeMode(value)}>{name}</button>)}</div>{error && <p role="alert">{error}</p>}</div>
        <section className="transport" aria-label="动作控制">
            <div className="key-poses" role="group" aria-label="关键姿态">{KEY_POSES.map((key, i) => <button key={key.name} aria-pressed={Math.abs(progress - key.at) < .002 && mode !== 'source'} onClick={() => seek(key.at)}><small>{String(i + 1).padStart(2, '0')}</small>{key.name}</button>)}</div>
            <div className="timeline"><input aria-label="动作进度" type="range" min="0" max="1" step="0.001" value={progress} onChange={event => seek(Number(event.target.value))}/><output>{(progress * DURATION).toFixed(2)} / {DURATION.toFixed(2)} s</output></div>
            <div className="controls"><div className="playback">
                <button className="icon" title="回到站立" aria-label="回到站立" onClick={() => seek(0)}><RotateCcw size={20}/></button><button className="icon" title="上一帧" aria-label="上一帧" onClick={() => seek(progress - 1 / (DURATION * 30))}><StepBack size={20}/></button>
                <button className="icon primary" disabled={!ready || !!error} title={playing ? '暂停' : '播放'} aria-label={playing ? '暂停' : '播放'} onClick={() => {if (mode === 'source') {setMode('skin'); setProgress(0);} else if (progress === 1) setProgress(0); setPlaying(value => !value);}}>{playing ? <Pause size={22}/> : <Play size={22}/>}</button>
                <button className="icon" title="下一帧" aria-label="下一帧" onClick={() => seek(progress + 1 / (DURATION * 30))}><StepForward size={20}/></button>
            </div><div className="options"><label className="speed">速度<select aria-label="播放速度" value={speed} onChange={event => setSpeed(Number(event.target.value))}><option value="0.25">0.25×</option><option value="0.5">0.5×</option><option value="1">1×</option></select></label><label><input type="checkbox" checked={dark} onChange={event => setDark(event.target.checked)}/>深底</label></div></div>
        </section>
    </main>;
}
createRoot(document.getElementById('root')).render(<App/>);
