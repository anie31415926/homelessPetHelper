import React,{useEffect,useRef,useState} from 'react';
import {createRoot} from 'react-dom/client';
import {Pause,Play,RotateCcw,StepBack,StepForward} from 'lucide-react';
import {createPreview} from './preview.js';
import {DURATION,sampleTransition} from './rig.js';
import './style.css';

function App() {
    const host=useRef(null),preview=useRef(null),timeRef=useRef(0),playingRef=useRef(false),speedRef=useRef(.7),view=useRef({bones:false,wire:false});
    const [time,setTime]=useState(0),[playing,setPlaying]=useState(false),[speed,setSpeed]=useState(.7),[ready,setReady]=useState(false),[error,setError]=useState('');
    const [bones,setBones]=useState(false),[wire,setWire]=useState(false),[background,setBackground]=useState('light');
    const phase=sampleTransition(time);
    function draw(){preview.current?.render({...view.current,...sampleTransition(timeRef.current)});}
    function seek(next){timeRef.current=Math.max(0,Math.min(DURATION-.00001,next));setTime(timeRef.current);draw();}
    function toggle(next){playingRef.current=next;setPlaying(next);}
    useEffect(()=>{
        let disposed=false,frame,last;
        createPreview(host.current).then(result=>{if(disposed){result.dispose();return;}preview.current=result;setReady(true);draw();}).catch(reason=>{if(!disposed)setError(reason.message);});
        function tick(now){const dt=last===undefined?0:Math.min(.05,(now-last)/1000);last=now;if(playingRef.current&&!document.hidden){timeRef.current=(timeRef.current+dt*speedRef.current)%DURATION;setTime(timeRef.current);draw();}frame=requestAnimationFrame(tick);}
        frame=requestAnimationFrame(tick);
        return()=>{disposed=true;cancelAnimationFrame(frame);preview.current?.dispose();};
    },[]);
    useEffect(()=>{view.current={bones,wire};draw();},[bones,wire]);
    return <main className="app">
        <header><h1>动物站坐过渡</h1><span>橘猫 / 01</span></header>
        <section className={`stage ${background}`} aria-busy={!ready}>
            <div className="viewport" ref={host}/>
            {!ready&&<div className="load-state" role="status">{error||'加载中'}</div>}
            <div className="stage-state"><span>{phase.phase}</span><output>{Math.round(phase.progress*100)}%</output></div>
        </section>
        <aside aria-label="动画控制">
            <div className="transport">
                <button title={playing?'暂停':'播放'} aria-label={playing?'暂停':'播放'} disabled={!ready} onClick={()=>toggle(!playing)}>{playing?<Pause/>:<Play/>}</button>
                <button title="复位" aria-label="复位" disabled={!ready} onClick={()=>{toggle(false);seek(0);}}><RotateCcw/></button>
                <button title="上一帧" aria-label="上一帧" disabled={!ready} onClick={()=>{toggle(false);seek(timeRef.current-1/30);}}><StepBack/></button>
                <button title="下一帧" aria-label="下一帧" disabled={!ready} onClick={()=>{toggle(false);seek(timeRef.current+1/30);}}><StepForward/></button>
                <output className="time">{time.toFixed(2)} / {DURATION.toFixed(2)} s</output>
            </div>
            <label className="timeline">进度<input type="range" min="0" max={DURATION-.00001} step=".001" value={time} disabled={!ready} onChange={e=>{toggle(false);seek(Number(e.target.value));}}/></label>
            <div className="postures" role="group" aria-label="姿势"><button onClick={()=>{toggle(false);seek(0);}} disabled={!ready}>站立</button><button onClick={()=>{toggle(false);seek(2.5);}} disabled={!ready}>坐姿</button></div>
            <label className="speed">速度<select aria-label="速度" value={speed} onChange={e=>{const n=Number(e.target.value);speedRef.current=n;setSpeed(n);}}><option value="0.35">0.35×</option><option value="0.7">0.7×</option><option value="1">1×</option></select></label>
            <fieldset><legend>显示</legend><label><input type="checkbox" checked={bones} onChange={e=>setBones(e.target.checked)}/>骨骼</label><label><input type="checkbox" checked={wire} onChange={e=>setWire(e.target.checked)}/>网格</label></fieldset>
            <fieldset className="swatches"><legend>背景</legend>{[['light','浅色'],['dark','深色'],['gray','灰色']].map(([id,label])=><button key={id} className={id} title={label} aria-label={label} aria-pressed={background===id} onClick={()=>setBackground(id)}/>)}</fieldset>
        </aside>
    </main>;
}
createRoot(document.getElementById('root')).render(<App/>);
