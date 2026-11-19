import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Pause, Play, RotateCcw, Shuffle, Coins, Clock3, ArrowUp, Merge, Route, X, ChevronRight, RefreshCw } from 'lucide-react';
import { CATALOG, t, unitDefinition, animalDefinition, laneDefinition } from './catalog.js';
import { evaluateOrder } from './orders.js';
import { useBattle } from './useBattle.js';
import { Battlefield, UNIT_ICONS } from './Battlefield.jsx';

function initialOptions() {
    const query=new URLSearchParams(location.search), seedText=query.get('seed');
    const seed=seedText!=null && /^\d+$/.test(seedText) && Number(seedText)<=0xFFFFFFFF?Number(seedText):20261119;
    const profile=CATALOG.animalStates.some(item=>item.id===query.get('profile'))?query.get('profile'):'calm';
    return {seed,profile,sessionId:`${seed}-1`};
}
function Meter({label,value,detail,className=''}) {
    return <div className={`siege-meter ${className}`}><div><span>{label}</span><span>{detail}</span></div><div role="progressbar" aria-label={label} aria-valuenow={Math.round(value*100)} aria-valuemin="0" aria-valuemax="100"><span style={{width:`${Math.min(1,value)*100}%`}}/></div></div>;
}
function Dialog({label,onClose,children}) {
    const ref=useRef(null);
    useEffect(()=> {
        const previous=document.activeElement, element=ref.current; element?.focus();
        function key(event) {
            if(event.key==='Escape'&&onClose) onClose();
            if(event.key!=='Tab') return;
            const controls=[...element.querySelectorAll('button,input,select')].filter(item=>!item.disabled);
            if(!controls.length) { event.preventDefault(); return; }
            if(event.shiftKey && (document.activeElement===controls[0]||document.activeElement===element)) {event.preventDefault();controls.at(-1).focus();}
            else if(!event.shiftKey && document.activeElement===controls.at(-1)) {event.preventDefault();controls[0].focus();}
        }
        element?.addEventListener('keydown',key);
        return ()=> {element?.removeEventListener('keydown',key);previous?.focus?.();};
    },[]);
    return <div className="siege-overlay"><section role="dialog" aria-modal="true" aria-label={label} className="siege-dialog" ref={ref} tabIndex={-1}>
        <header><h2>{label}</h2>{onClose&&<button type="button" className="icon-button" aria-label={t('close')} title={t('close')} onClick={onClose}><X size={19}/></button>}</header>
        <div className="dialog-content">{children}</div>
    </section></div>;
}
export default function SiegeApp() {
    const [options,setOptions]=useState(initialOptions), {state,submit,pause,restart}=useBattle(options);
    const serial=useRef(1), [seedInput,setSeedInput]=useState(String(options.seed)),[profileInput,setProfileInput]=useState(options.profile);
    const [cardId,setCardId]=useState(null),[laneId,setLaneId]=useState('mid'),[selectedUnit,setSelectedUnit]=useState(null);
    const [selectedTower,setSelectedTower]=useState(null);
    const [materials,setMaterials]=useState([]),[anchor,setAnchor]=useState(null),[mode,setMode]=useState(null),[reroute,setReroute]=useState(null),[info,setInfo]=useState(null),[error,setError]=useState(null);
    const editable=state.phase==='decision'&&!state.paused;
    const terminal=['won','timeout'].includes(state.phase);
    function clearPlan() {setCardId(null);setMaterials([]);setAnchor(null);setMode(null);setReroute(null);setError(null);}
    useEffect(()=> {clearPlan();setSelectedUnit(null);},[state.turn,state.sessionId]);
    const input=useMemo(()=>({sessionId:state.sessionId,turn:state.turn,
        ...(cardId!=null?{deploy:{instanceId:cardId,laneId}}:{}),
        ...(materials.length===3?{merge:{anchorId:anchor,materialIds:materials}}:{}),
        ...(reroute?{reroute}: {})}),[state.sessionId,state.turn,cardId,laneId,materials,anchor,reroute]);
    const preview=useMemo(()=>evaluateOrder(state,CATALOG,input),[state,input]);
    const chosenCard=state.hand.find(card=>card.instanceId===cardId);
    const chosenDefinition=chosenCard&&unitDefinition(CATALOG,chosenCard.definitionId);
    const activeUnit=state.units.find(unit=>unit.id===selectedUnit);
    const incompleteMerge=mode==='merge'&&materials.length!==3;
    const animal=animalDefinition(CATALOG,state.animal);
    const message=error?t(error):incompleteMerge?`${t('materials')} ${materials.length}/3`:editable&&!preview.ok?t(preview.reason):state.log.at(-1)?t(state.log.at(-1).key):'';
    function onUnit(id) {
        setSelectedUnit(id);
        if(mode==='reroute') {setMode(null);setReroute(null);}
        if(!editable||mode!=='merge') return;
        const unit=state.units.find(item=>item.id===id), base=unitDefinition(CATALOG,unit.definitionId);
        if(!base.upgradeId||unit.transit) {setError('reason.merge');return;}
        const currentAnchor=state.units.find(item=>item.id===anchor);
        if(currentAnchor&&currentAnchor.definitionId!==unit.definitionId) {setMaterials([id]);setAnchor(id);return;}
        if(materials.includes(id)) {const next=materials.filter(value=>value!==id);setMaterials(next);if(anchor===id)setAnchor(next[0]??null);}
        else if(materials.length<3) {setMaterials([...materials,id]);if(anchor==null)setAnchor(id);}
        setError(null);
    }
    function onLane(id) {setLaneId(id);}
    function queueMerge() {
        if(!editable)return;setMode(mode==='merge'?null:'merge');setReroute(null);setMaterials([]);setAnchor(null);setError(null);
    }
    function addNewMaterial() {if(!chosenDefinition)return;if(materials.includes('new')){const next=materials.filter(id=>id!=='new');setMaterials(next);if(anchor==='new')setAnchor(next[0]??null);}else if(materials.length<3){setMaterials([...materials,'new']);if(anchor==null)setAnchor('new');}}
    function execute() {
        if(incompleteMerge)return;
        const result=submit(input); if(!result.ok)setError(result.reason);else clearPlan();
    }
    function changeEncounter(seed,profile) {
        const next={seed,profile,sessionId:`${seed}-${++serial.current}`};setOptions(next);restart(next);setSeedInput(String(seed));setProfileInput(profile);setInfo(null);clearPlan();setSelectedUnit(null);setSelectedTower(null);
        const query=new URLSearchParams({seed:String(seed),profile});history.replaceState(null,'',`${location.pathname}?${query}`);
    }
    function applySettings() {if(!/^\d+$/.test(seedInput)||Number(seedInput)>0xFFFFFFFF){setError('invalidSeed');return;}changeEncounter(Number(seedInput),profileInput);}
    function discard() {if(cardId==null)return;const result=submit({sessionId:state.sessionId,turn:state.turn,discardId:cardId});if(!result.ok)setError(result.reason);else clearPlan();}
    function openInfo(value) {pause(true);setInfo(value);}
    function closeInfo() {setInfo(null);pause(false);}
    const planText=[chosenDefinition?t(chosenDefinition.nameKey):'',materials.length===3?t('merge'):'',reroute?t('reroute'):''].filter(Boolean).join(' + ');
    return <main className="siege-app" data-phase={state.phase} data-turn={state.turn} data-seed={state.seed} data-time={state.elapsedMs}>
        <header className="siege-header"><h1>{t('title')}</h1><span className="encounter-id">#{state.seed}</span><button type="button" className="icon-button" aria-label={t('menu')} title={t('menu')} onClick={()=>openInfo('menu')}><Pause size={19}/></button></header>
        <div className="siege-score"><span><Coins size={15}/><b data-energy>{state.energy}</b><span className="resource-cap">/{CATALOG.rules.energyCap}</span></span><span>{t('goal')} <b>{state.progress}/{CATALOG.rules.target}</b></span><span><Clock3 size={14}/>{Math.ceil((CATALOG.rules.durationMs-state.elapsedMs)/1000)}</span></div>
        <div className="siege-timing"><Meter label={t('player')} value={state.phase==='decision'?1:state.playerChargeMs/CATALOG.rules.playerPeriodMs} detail={state.phase==='decision'?t('ready'):`${Math.max(0,(CATALOG.rules.playerPeriodMs-state.playerChargeMs)/1000).toFixed(1)}s`}/><Meter className="animal-meter" label={t('animal')} value={state.animal.charge} detail={t(animal.nameKey)}/></div>
        <div className="siege-stage"><Battlefield state={state} selectedLane={laneId} selectedUnit={selectedUnit} selectedTower={selectedTower} materials={materials} mergeAnchor={anchor} onLane={onLane} onUnit={onUnit} onTower={id=>setSelectedTower(selectedTower===id?null:id)}/></div>
        <div className="siege-response" aria-live="polite"><span className={`phase-dot ${editable?'decision':''}`}/><span>{state.paused?t('paused'):editable?t('thinking'):t('running')}</span><span className="response-text">{message}</span></div>
        <section className="siege-controls" aria-label={t('plan')}>
            <div className="siege-hand">{state.hand.map(card=> {const definition=unitDefinition(CATALOG,card.definitionId),Icon=UNIT_ICONS[definition.icon];return <button type="button" key={card.instanceId} className={`hand-card ${cardId===card.instanceId?'selected':''}`} disabled={!editable} aria-pressed={cardId===card.instanceId} data-card={card.instanceId} data-definition={definition.id} title={t(definition.detailKey)} onClick={()=>{setCardId(cardId===card.instanceId?null:card.instanceId);setError(null);}}><Icon size={19}/><span>{t(definition.nameKey)}</span><small><Coins size={11}/>{definition.cost}</small></button>;})}</div>
            <div className="siege-order-row"><div className="lane-segment" role="group" aria-label={t('deploy')}>{CATALOG.map.lanes.map(lane=><button type="button" key={lane.id} data-lane-select={lane.id} aria-pressed={laneId===lane.id} disabled={!editable||!lane.open} onClick={()=>onLane(lane.id)}>{t(lane.nameKey)}</button>)}</div>
                <button type="button" className={`icon-button ${mode==='merge'?'active':''}`} aria-label={t('merge')} title={t('merge')} disabled={!editable} onClick={queueMerge}><Merge size={19}/></button>
                <button type="button" className={`icon-button ${mode==='reroute'?'active':''}`} aria-label={t('reroute')} title={t('reroute')} disabled={!editable||!activeUnit||activeUnit.reroutes<1||activeUnit.transit} onClick={()=>{const target=CATALOG.map.lanes.find(lane=>lane.open&&lane.id!==activeUnit.laneId);setMode('reroute');setMaterials([]);setAnchor(null);setReroute(target?{unitId:activeUnit.id,laneId:target.id}:null);}}><Route size={19}/></button>
                <button type="button" className="icon-button" aria-label={t('discard')} title={t('discard')} disabled={!editable||cardId==null} onClick={discard}><Shuffle size={18}/></button>
            </div>
            {mode==='merge'&&<div className="merge-selection"><span>{t('materials')} {materials.length}/3</span><select aria-label={t('pickMaterial')} value="" onChange={event=>onUnit(Number(event.target.value))}><option value="" disabled>{t('pickMaterial')}</option>{state.units.filter(unit=>!unit.transit&&unitDefinition(CATALOG,unit.definitionId).upgradeId).map(unit=><option key={unit.id} value={unit.id} disabled={materials.includes(unit.id)}>{t(unitDefinition(CATALOG,unit.definitionId).nameKey)} #{unit.id} · {t(laneDefinition(CATALOG,unit.laneId).nameKey)}</option>)}</select>{materials.map(id=><button type="button" key={id} title={t('anchor')} className={anchor===id?'is-anchor':''} onClick={()=>setAnchor(id)}>{id==='new'?t('deploy'):`#${id}`}</button>)}{chosenDefinition?.upgradeId&&<button type="button" className={materials.includes('new')?'is-anchor':''} onClick={addNewMaterial}><ArrowUp size={13}/>{t('deploy')}</button>}<button type="button" className="icon-button" aria-label={t('cancel')} onClick={()=>{setMode(null);setMaterials([]);setAnchor(null);}}><X size={14}/></button></div>}
            {mode==='reroute'&&reroute&&<div className="merge-selection"><span>{t('reroute')} #{reroute.unitId}</span><select aria-label={t('rerouteTarget')} value={reroute.laneId} onChange={event=>setReroute({...reroute,laneId:event.target.value})}>{CATALOG.map.lanes.filter(lane=>lane.open&&lane.id!==activeUnit?.laneId).map(lane=><option key={lane.id} value={lane.id}>{t(lane.nameKey)}</option>)}</select><button type="button" className="icon-button" aria-label={t('cancel')} onClick={()=>{setMode(null);setReroute(null);}}><X size={14}/></button></div>}
            <div className="siege-submit"><span className="plan-caption" title={planText}>{planText||`${t('turn')} ${state.turn}`} {preview.ok&&preview.cost>0?`· ${t('cost')} ${preview.cost}`:''}</span><button type="button" className="execute-button" data-execute disabled={!editable||!preview.ok||incompleteMerge||(mode==='reroute'&&!reroute)} onClick={execute}>{planText?t('execute'):t('wait')}<ChevronRight size={17}/></button></div>
        </section>
        {(info||state.paused)&&!terminal&&<Dialog label={t('menu')} onClose={closeInfo}>
                <span className="trial-label">{t('trial')} · {t('allLanes')}</span>
                <span className="trial-label">{t('repairBudget')} {state.animal.repairsLeft}/{CATALOG.rules.animalRepairs} · {t('buildBudget')} {state.animal.buildsLeft}/{CATALOG.rules.animalBuilds}</span>
                <label>{t('state')}<select value={profileInput} onChange={event=>setProfileInput(event.target.value)}>{CATALOG.animalStates.map(profile=><option key={profile.id} value={profile.id}>{t(profile.nameKey)}</option>)}</select></label>
                <label>{t('seed')}<input value={seedInput} inputMode="numeric" onChange={event=>setSeedInput(event.target.value)}/></label>
                {error&&<p role="alert">{t(error)}</p>}
                <button type="button" className="dialog-primary" onClick={applySettings}><RefreshCw size={16}/>{t('apply')}</button>
                <button type="button" onClick={()=>changeEncounter(state.seed,options.profile)}><RotateCcw size={16}/>{t('restart')}</button>
                <button type="button" onClick={()=>changeEncounter(crypto.getRandomValues(new Uint32Array(1))[0],options.profile)}><Shuffle size={16}/>{t('new')}</button>
                <button type="button" onClick={closeInfo}><Play size={16}/>{t('resume')}</button>
        </Dialog>}
        {terminal&&<Dialog label={t(state.phase==='won'?'win':'timeout')}>
            <img className="result-pet" src={CATALOG.map.animalImage} alt={t('animal')}/><p>{t('resultNote')}</p><div className="result-stats">{['deployed','merges','towers','delivered'].map((key,i)=><span key={key}>{t(['actions','merges','towers','delivered'][i])}<b>{state.stats[key]}</b></span>)}</div>
            <button type="button" className="dialog-primary" onClick={()=>changeEncounter(state.seed,options.profile)}><RotateCcw size={16}/>{t('restart')}</button><button type="button" onClick={()=>changeEncounter(crypto.getRandomValues(new Uint32Array(1))[0],options.profile)}><Shuffle size={16}/>{t('new')}</button>
        </Dialog>}
    </main>;
}
