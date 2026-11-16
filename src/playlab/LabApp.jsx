import React, { useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowLeft, ArrowRight, ArrowUp, Check, ChevronRight, Droplets, Fish, Footprints, Hand, Heart, Home, MapPin, Meh, Pause, PawPrint, Play, RectangleHorizontal, RotateCcw, Scissors, Smile, ToyBrick, Undo2, X } from 'lucide-react';
import { Pet } from '../Pet.jsx';
import LabScene from './LabScene.jsx';
import { INFO, MODES, bridgeAt, canClean, canRescue, cellAt, center, cleanAt, createExperiment, guideDestination, guideFood, moveGuide, puzzleFood, rescueCleanPet, slideBox, tick, undoPuzzle } from './engine.js';

const STORAGE = 'fluffy-rescue-lab-v1';
const readMode = () => { const value = new URLSearchParams(location.search).get('mode'); return MODES.includes(value) ? value : 'puzzle'; };
const readVotes = () => { try { const value = JSON.parse(localStorage.getItem(STORAGE)); return value && typeof value === 'object' && !Array.isArray(value) ? value : {}; } catch { return {}; } };
const time = value => `${Math.floor(value / 60).toString().padStart(2, '0')}:${Math.floor(value % 60).toString().padStart(2, '0')}`;
const directions = { ArrowUp: [0, -1], ArrowDown: [0, 1], ArrowLeft: [-1, 0], ArrowRight: [1, 0], w: [0, -1], s: [0, 1], a: [-1, 0], d: [1, 0] };

function Tool({ icon: Icon, label, active, count, onClick, disabled, ...rest }) {
  return <button className={`lab-tool ${active ? 'active' : ''}`} onClick={onClick} disabled={disabled} aria-pressed={active} title={label} {...rest}><Icon size={23} strokeWidth={1.8} /><span>{label}{count !== undefined && <b>{count}</b>}</span></button>;
}

export default function LabApp() {
  const [mode, setMode] = useState(readMode);
  const games = useRef(Object.fromEntries(MODES.map(value => [value, createExperiment(value)])));
  const [frame, setFrame] = useState(0);
  const [paused, setPaused] = useState(false);
  const [result, setResult] = useState(null);
  const [votes, setVotes] = useState(readVotes);
  const [finished, setFinished] = useState(() => Object.fromEntries(Object.keys(readVotes()).filter(value => MODES.includes(value)).map(value => [value, true])));
  const modeRef = useRef(mode);
  const blocked = useRef(false);
  const direction = useRef(null);
  const gesture = useRef(null);
  const svgRef = useRef(null);
  const modalRef = useRef(null);
  const game = games.current[mode];
  const overlay = paused ? 'pause' : result === mode ? 'result' : null;
  const redraw = () => setFrame(value => value + 1);
  modeRef.current = mode;
  blocked.current = Boolean(overlay);
  const clearInput = () => {
    direction.current = null;
    gesture.current = null;
    for (const value of Object.values(games.current)) { value.brush = null; value.drag = null; }
  };

  function selectMode(value) {
    if (!MODES.includes(value)) return;
    clearInput();
    setPaused(false); setResult(null); setMode(value);
    const url = new URL(location.href); url.searchParams.set('mode', value);
    history.replaceState(null, '', url);
  }

  function restart() {
    clearInput();
    games.current[mode] = createExperiment(mode);
    setPaused(false); setResult(null); redraw();
  }

  function pointFromEvent(event) {
    const matrix = svgRef.current?.getScreenCTM();
    return matrix ? new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse()) : null;
  }

  function activateCell(col, row) {
    if (blocked.current || game.done) return;
    if (mode === 'puzzle') {
      if (game.tool === 'bridge') bridgeAt(game, col, row);
      else if (game.tool === 'food') puzzleFood(game, col, row);
      else if (game.selected) slideBox(game, game.selected, col, row);
    } else if (mode === 'guide') guideDestination(game, col, row);
    redraw();
  }

  function activateBox(id, col, row) {
    if (blocked.current || game.done) return;
    if (col !== undefined) slideBox(game, id, col, row);
    else { game.selected = id; game.tool = 'move'; }
    redraw();
  }

  function cleanByKey(item) {
    if (blocked.current || game.done) return;
    if (game.tool !== item.type) game.message = '这个工具对它不太合适';
    else if (!canClean(game, item)) game.message = '这块杂物还藏在后面';
    else cleanAt(game, center(item.col, item.row), 1);
    redraw();
  }

  function collectPet(id) {
    if (blocked.current || game.done) return;
    if (!rescueCleanPet(game, id)) game.message = '门口到它身边，还缺一条通道';
    redraw();
  }

  const handlers = {
    onPointerDown(event) {
      if (blocked.current || game.done || (event.button !== 0 && event.pointerType !== 'touch')) return;
      event.preventDefault();
      const point = pointFromEvent(event);
      if (!point) return;
      event.currentTarget.setPointerCapture(event.pointerId);
      const box = event.target.closest('[data-box]')?.dataset.box;
      const pet = event.target.closest('[data-pet]')?.dataset.pet;
      if (mode === 'cleanup' && pet) { collectPet(pet); return; }
      if (mode === 'cleanup') {
        const id = event.target.closest('[data-junk]')?.dataset.junk;
        const item = game.items.find(value => value.id === id);
        if (item && !canClean(game, item)) game.message = '这块杂物还藏在后面';
        else if (item && item.type !== game.tool) game.message = '这个工具对它不太合适';
        gesture.current = { type: 'clean', pointer: event.pointerId };
        game.brush = { x: point.x, y: point.y };
        cleanAt(game, point, .08);
      } else if (mode === 'puzzle') {
        if (box && game.tool === 'move') {
          game.selected = box;
          gesture.current = { type: 'box', id: box, start: point, pointer: event.pointerId };
        } else gesture.current = { type: 'ground', start: point, pointer: event.pointerId };
      } else { const cell = cellAt(point); activateCell(cell.col, cell.row); }
      redraw();
    },
    onPointerMove(event) {
      if (!gesture.current || blocked.current || gesture.current.pointer !== event.pointerId) return;
      const point = pointFromEvent(event);
      if (!point) return;
      if (gesture.current.type === 'clean') game.brush = { x: point.x, y: point.y };
      else if (gesture.current.type === 'box' && Math.hypot(point.x - gesture.current.start.x, point.y - gesture.current.start.y) > 8) game.drag = { x: point.x, y: point.y };
    },
    onPointerUp(event) {
      const value = gesture.current;
      if (!value || value.pointer !== event.pointerId) return;
      const point = pointFromEvent(event);
      game.drag = null; game.brush = null; gesture.current = null;
      if (point && value.type === 'box') {
        const cell = cellAt(point);
        slideBox(game, value.id, cell.col, cell.row);
      } else if (point && value.type === 'ground') {
        const cell = cellAt(point); activateCell(cell.col, cell.row);
      }
      redraw();
    },
    onPointerCancel() { clearInput(); redraw(); },
    onLostPointerCapture() { if (gesture.current) { clearInput(); redraw(); } },
  };

  useEffect(() => {
    let request;
    let prior = performance.now();
    let lastRender = 0;
    const loop = now => {
      const dt = Math.min((now - prior) / 1000, .05);
      prior = now;
      if (!blocked.current && !document.hidden) {
        const current = games.current[modeRef.current];
        if (direction.current && current.mode === 'guide') moveGuide(current, ...direction.current, dt);
        if (current.brush && current.mode === 'cleanup') cleanAt(current, current.brush, dt);
        tick(current, dt);
        if (current.done && !current.notified) { current.notified = true; clearInput(); setFinished(value => ({ ...value, [current.mode]: true })); setResult(current.mode); }
        if (now - lastRender >= 32) { redraw(); lastRender = now; }
      }
      request = requestAnimationFrame(loop);
    };
    request = requestAnimationFrame(loop);
    const down = event => {
      if (blocked.current || event.ctrlKey || event.metaKey || event.altKey || event.target.matches('input,textarea,select')) return;
      const current = games.current[modeRef.current];
      if (current.mode !== 'guide' || current.done) return;
      const value = directions[event.key] || directions[event.key.toLowerCase()];
      if (value) { event.preventDefault(); direction.current = value; }
    };
    const up = event => { if (directions[event.key] || directions[event.key.toLowerCase()]) direction.current = null; };
    const clear = () => clearInput();
    window.addEventListener('keydown', down); window.addEventListener('keyup', up);
    window.addEventListener('blur', clear); document.addEventListener('visibilitychange', clear);
    const pop = () => { clearInput(); setMode(readMode()); setPaused(false); setResult(null); };
    window.addEventListener('popstate', pop);
    return () => { cancelAnimationFrame(request); window.removeEventListener('keydown', down); window.removeEventListener('keyup', up); window.removeEventListener('blur', clear); document.removeEventListener('visibilitychange', clear); window.removeEventListener('popstate', pop); };
  }, []);

  useEffect(() => {
    if (!overlay) return;
    clearInput();
    const prior = document.activeElement;
    const modal = modalRef.current;
    modal?.querySelector('button')?.focus();
    const trap = event => {
      if (event.key === 'Escape') { setPaused(false); setResult(null); }
      if (event.key !== 'Tab') return;
      const buttons = [...modal.querySelectorAll('button:not(:disabled)')];
      const first = buttons[0]; const last = buttons.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    window.addEventListener('keydown', trap);
    return () => { window.removeEventListener('keydown', trap); if (prior instanceof HTMLElement && prior.isConnected) prior.focus(); };
  }, [overlay]);

  function vote(value) {
    const next = { ...votes, [mode]: { vote: value, elapsed: game.elapsed, actions: game.actions } };
    setVotes(next);
    try { localStorage.setItem(STORAGE, JSON.stringify(next)); } catch { /* Preference feedback is optional. */ }
  }

  function startDirection(event, value) {
    if (blocked.current || game.done) return;
    event.preventDefault();
    if (event.pointerId !== undefined) event.currentTarget.setPointerCapture(event.pointerId);
    direction.current = value;
    moveGuide(game, ...value, .08);
    redraw();
  }

  const selectedBox = game.boxes?.find(box => box.id === game.selected);
  const arrowDefs = [[ArrowUp, [0, -1], '向上'], [ArrowLeft, [-1, 0], '向左'], [ArrowRight, [1, 0], '向右'], [ArrowDown, [0, 1], '向下']];
  const completed = MODES.filter(value => finished[value]).length;
  const safe = game.pets?.filter(pet => pet.safe).length || 0;
  return <main className={`lab-shell lab-${mode}`} data-frame={frame}>
    <div className="lab-content" inert={overlay ? true : undefined}>
      <header className="lab-header"><div className="lab-brand"><span><PawPrint size={23} /></span><div><h1>毛茸茸救助站</h1><p>三个现场，三种相遇</p></div></div><div className="lab-header-tools"><button className="lab-icon" aria-label="暂停" title="暂停" disabled={game.done} onClick={() => { clearInput(); setPaused(true); }}><Pause size={18} /></button><button className="lab-icon" aria-label="重新开始当前玩法" title="重新开始当前玩法" onClick={restart}><RotateCcw size={18} /></button></div></header>
      <nav className="mode-tabs" aria-label="选择救助玩法">{MODES.map((value, index) => <button key={value} aria-current={mode === value ? 'page' : undefined} onClick={() => selectMode(value)}><span className="mode-number">{finished[value] ? <Check size={11} /> : String.fromCharCode(65 + index)}</span>{INFO[value].name}{votes[value] && <span className="vote-dot" />}</button>)}</nav>
      <section className="lab-mission"><div><p><MapPin size={11} />{INFO[mode].place}</p><h2>{INFO[mode].animal}</h2></div><div className="mission-meta"><strong>{time(game.elapsed)}</strong><span>{mode === 'puzzle' ? `${game.actions} 次操作` : mode === 'cleanup' ? `${safe} / 2 已回家` : game.attention}</span></div></section>
      <section className="lab-stage" aria-label={INFO[mode].name}>
        <LabScene game={game} svgRef={svgRef} handlers={handlers} onCell={activateCell} onBox={activateBox} onJunk={cleanByKey} onPet={collectPet} />
        {mode === 'cleanup' && <div className="stage-meter"><span>清理 {game.cleaned} / {game.total}</span><div><i style={{ width: `${game.cleaned / game.total * 100}%` }} /></div></div>}
        {mode === 'guide' && <div className="stage-meter trust-meter"><Heart size={13} /><div><i style={{ width: `${game.trust * 100}%` }} /></div><span>{Math.round(game.trust * 100)}%</span></div>}
        {game.done && <span className="scene-complete"><Check size={15} />已平安回家</span>}
      </section>
      <div className="lab-status" role="status" aria-live="polite"><span />{game.message}</div>
      <section className={`lab-controls controls-${mode}`} aria-label="救助工具">
        {game.done ? <div className="finished-controls"><Heart size={20} /><button className="lab-primary" onClick={restart}><RotateCcw size={16} />再玩一次</button><button className="lab-primary secondary" onClick={() => selectMode(MODES[(MODES.indexOf(mode) + 1) % 3])}>下一种 <ArrowRight size={16} /></button></div> : mode === 'puzzle' ? <>
          <div className="puzzle-tools"><Tool icon={Hand} label="搬移" active={game.tool === 'move'} onClick={() => { game.tool = 'move'; redraw(); }} /><Tool icon={RectangleHorizontal} label="木板" active={game.tool === 'bridge'} count="1" onClick={() => { game.tool = 'bridge'; redraw(); }} /><Tool icon={Fish} label="食物" active={game.tool === 'food'} onClick={() => { game.tool = 'food'; redraw(); }} /><button className="lab-icon undo-button" aria-label="撤销一步" title="撤销一步" disabled={!game.history.length} onClick={() => { undoPuzzle(game); redraw(); }}><Undo2 size={21} /></button></div>
          <div className="box-arrows"><span>木箱{selectedBox ? ` ${selectedBox.id.toUpperCase()}` : ''}</span>{arrowDefs.map(([Icon, value, name]) => <button key={name} className="lab-arrow" aria-label={`木箱${name}`} title={`木箱${name}`} disabled={!selectedBox || Boolean(game.animal.path.length)} onClick={() => activateBox(selectedBox.id, selectedBox.col + value[0], selectedBox.row + value[1])}><Icon size={20} /></button>)}</div>
        </> : mode === 'cleanup' ? <>
          {[['litter', Hand, '捡拾'], ['weeds', Scissors, '修剪'], ['mud', Droplets, '冲洗']].map(([value, Icon, label]) => <Tool key={value} icon={Icon} label={label} active={game.tool === value} count={game.items.filter(item => item.type === value && item.hp > 0).length} onClick={() => { game.tool = value; redraw(); }} />)}
          <button className="collect-button" aria-label="接回动物" title="接回动物" disabled={!game.pets.some(pet => canRescue(game, pet))} onClick={() => { const pet = game.pets.find(value => canRescue(game, value)); if (pet) collectPet(pet.id); }}><Home size={25} /><span>接回</span></button>
        </> : <>
          <div className="guide-dpad">{arrowDefs.map(([Icon, value, name]) => <button key={name} className={`lab-arrow guide-${name}`} aria-label={`${name}移动`} title={`${name}移动`} onPointerDown={event => startDirection(event, value)} onPointerUp={() => { direction.current = null; }} onPointerCancel={() => { direction.current = null; }} onLostPointerCapture={() => { direction.current = null; }} onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') startDirection(event, value); }} onKeyUp={() => { direction.current = null; }} onBlur={() => { direction.current = null; }} onClick={event => { if (event.detail === 0) { moveGuide(game, ...value, .15); redraw(); } }}><Icon size={21} /></button>)}<span><PawPrint size={15} /></span></div>
          <div className="guide-tools"><Tool icon={ToyBrick} label="玩具" active={game.toy} onClick={() => { game.toy = !game.toy; game.actions++; game.message = game.toy ? '小狗的耳朵朝你转过来了' : '先把玩具收起来'; redraw(); }} /><Tool icon={Footprints} label="慢走" active={game.slow} onClick={() => { game.slow = !game.slow; game.actions++; redraw(); }} /><button className="guide-food" disabled={Boolean(game.bait) || game.food === 0} onClick={() => { guideFood(game); redraw(); }} aria-label={`放下食物，剩余 ${game.food} 份`}><Fish size={20} />食物 <b>{game.food}</b></button></div>
        </>}
      </section>
      <footer className="lab-footer"><span>已体验 {completed} / 3</span><div className="lab-footer-links"><a href="/outing.html">回合救助 <ChevronRight size={11} /></a><a href="/rescue.html">原网兜玩法 <ChevronRight size={11} /></a></div></footer>
    </div>
    {overlay && <div className="lab-overlay"><section className="lab-modal" role="dialog" aria-modal="true" aria-labelledby="lab-modal-title" ref={modalRef}>
      <button className="lab-icon modal-close" aria-label="关闭弹窗" title="关闭" onClick={() => { setPaused(false); setResult(null); }}><X size={18} /></button>
      {overlay === 'pause' ? <><span className="pause-mark"><Pause size={27} /></span><h2 id="lab-modal-title">在这里歇一会儿</h2><button className="lab-primary" onClick={() => setPaused(false)}><Play size={16} />继续救助</button><button className="modal-restart" onClick={restart}><RotateCcw size={15} />重新开始这一关</button></> : <>
        <div className="result-art"><Pet kind={INFO[mode].kind} happy /><Heart size={25} fill="currentColor" /></div><p className="result-kicker"><Check size={13} />{mode === 'cleanup' ? '两个小家伙都安全了' : '小家伙安全了'}</p><h2 id="lab-modal-title">这次，是你帮到了它。</h2>
        <div className="lab-result-stats"><span><strong>{time(game.elapsed)}</strong>本次用时</span><span><strong>{game.actions}</strong>{mode === 'cleanup' ? '处清理 / 救助' : '次操作'}</span></div>
        <fieldset className="feedback"><legend>这个玩法，还想继续玩吗？</legend>{[[1, Meh, '不太想'], [2, Smile, '还行'], [3, Heart, '想再玩']].map(([value, Icon, label]) => <button key={value} aria-pressed={votes[mode]?.vote === value} onClick={() => vote(value)}><Icon size={19} /><span>{label}</span></button>)}</fieldset>
        <button className="lab-primary" onClick={() => selectMode(MODES[(MODES.indexOf(mode) + 1) % 3])}>试试下一种 <ArrowRight size={16} /></button><button className="modal-restart" onClick={restart}><RotateCcw size={14} />再玩一次</button>
      </>}
    </section></div>}
  </main>;
}
