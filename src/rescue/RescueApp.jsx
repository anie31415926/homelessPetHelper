import React, { useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowLeft, ArrowRight, ArrowUp, Check, ChevronRight, Fish, Footprints, Heart, Home, MapPin, Pause, PawPrint, Play, RotateCcw, ScanLine, Settings2, ShoppingBasket, X } from 'lucide-react';
import { Pet } from '../Pet.jsx';
import Scene from './Scene.jsx';
import { MAP_X, MAP_Y, TILE, castNet, createGame, moveMaze, moveRescuer, tapMaze, tell, throwFood, updateGame } from './game.js';

const keyDirections = { ArrowUp: 'up', ArrowDown: 'down', ArrowLeft: 'left', ArrowRight: 'right', w: 'up', s: 'down', a: 'left', d: 'right' };
const formatTime = time => `${Math.floor(time / 60).toString().padStart(2, '0')}:${Math.floor(time % 60).toString().padStart(2, '0')}`;

function DirectionButton({ direction, Icon, onPress, onRelease, disabled }) {
  const names = { up: '向上移动', down: '向下移动', left: '向左移动', right: '向右移动' };
  return <button className={`direction direction-${direction}`} disabled={disabled} aria-label={names[direction]} title={names[direction]}
    onPointerDown={event => { event.preventDefault(); event.currentTarget.setPointerCapture(event.pointerId); onPress(direction); }}
    onPointerUp={onRelease} onPointerCancel={onRelease} onLostPointerCapture={onRelease}
    onKeyDown={event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onPress(direction); } }}
    onKeyUp={onRelease} onBlur={onRelease}
    onClick={event => { if (event.detail === 0) { onPress(direction); onRelease(); } }}><Icon size={21} strokeWidth={2.5} /></button>;
}

export default function RescueApp() {
  const gameRef = useRef(createGame());
  const [frame, setFrame] = useState(0);
  const [paused, setPaused] = useState(false);
  const [settings, setSettings] = useState(false);
  const [entrance, setEntrance] = useState('bottom');
  const [kind, setKind] = useState('cat');
  const [saved, setSaved] = useState(0);
  const directionRef = useRef(null);
  const queueRef = useRef([]);
  const blocked = useRef(false);
  const sceneRef = useRef(null);
  const modalRef = useRef(null);
  const inputRef = useRef(null);
  const game = gameRef.current;
  blocked.current = paused || settings;
  const redraw = () => setFrame(value => value + 1);
  const clearMovement = () => { directionRef.current = null; queueRef.current = []; };
  function restart(nextEntrance = entrance, nextKind = kind) {
    clearMovement();
    gameRef.current = createGame({ entrance: nextEntrance, kind: nextKind });
    setPaused(false);
    setSettings(false);
    redraw();
  }
  function press(direction) {
    if (blocked.current) return;
    queueRef.current = [];
    directionRef.current = direction;
    if (gameRef.current.phase === 'maze') moveMaze(gameRef.current, direction);
    else moveRescuer(gameRef.current, direction, .08);
    redraw();
  }
  function net() {
    if (blocked.current) return;
    clearMovement();
    castNet(gameRef.current);
    redraw();
  }
  function food() {
    if (blocked.current) return;
    throwFood(gameRef.current);
    redraw();
  }
  inputRef.current = { press, net, food };

  useEffect(() => {
    let request;
    let previous = performance.now();
    let lastRender = 0;
    const animate = now => {
      const dt = Math.min((now - previous) / 1000, .05);
      previous = now;
      if (!blocked.current && !document.hidden) {
        const current = gameRef.current;
        if (directionRef.current) {
          if (current.phase === 'maze') moveMaze(current, directionRef.current);
          else moveRescuer(current, directionRef.current, dt);
        } else if (current.phase === 'maze' && queueRef.current.length && current.moveCooldown === 0) {
          const [col, row] = queueRef.current[0];
          const direction = col > current.player.col ? 'right' : col < current.player.col ? 'left' : row > current.player.row ? 'down' : 'up';
          if (moveMaze(current, direction)) queueRef.current.shift();
          else queueRef.current = [];
        }
        const oldPhase = current.phase;
        updateGame(current, dt);
        if (current.phase !== oldPhase) clearMovement();
        if (now - lastRender >= 32) { redraw(); lastRender = now; }
      }
      request = requestAnimationFrame(animate);
    };
    request = requestAnimationFrame(animate);
    const keydown = event => {
      if (blocked.current || event.ctrlKey || event.metaKey || event.altKey || event.target.matches('input,select,textarea')) return;
      const direction = keyDirections[event.key] || keyDirections[event.key.toLowerCase()];
      if (direction) { event.preventDefault(); if (!event.repeat) inputRef.current.press(direction); }
      if (event.code === 'Space' && !event.target.closest('button,a')) { event.preventDefault(); if (!event.repeat) inputRef.current.net(); }
      if (event.key.toLowerCase() === 'f' && !event.repeat) inputRef.current.food();
    };
    const keyup = event => {
      const direction = keyDirections[event.key] || keyDirections[event.key.toLowerCase()];
      if (direction && directionRef.current === direction) directionRef.current = null;
    };
    const clear = () => clearMovement();
    window.addEventListener('keydown', keydown);
    window.addEventListener('keyup', keyup);
    window.addEventListener('blur', clear);
    document.addEventListener('visibilitychange', clear);
    return () => { cancelAnimationFrame(request); window.removeEventListener('keydown', keydown); window.removeEventListener('keyup', keyup); window.removeEventListener('blur', clear); document.removeEventListener('visibilitychange', clear); };
  }, []);

  const overlay = settings ? 'settings' : paused ? 'paused' : game.phase === 'success' ? 'success' : null;
  useEffect(() => {
    if (!overlay) return;
    clearMovement();
    const prior = document.activeElement;
    const modal = modalRef.current;
    modal?.querySelector('button:not(:disabled)')?.focus();
    const trap = event => {
      if (event.key === 'Escape' && overlay !== 'success') { setPaused(false); setSettings(false); }
      if (event.key !== 'Tab' || !modal) return;
      const buttons = [...modal.querySelectorAll('button:not(:disabled)')];
      const first = buttons[0];
      const last = buttons[buttons.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    };
    window.addEventListener('keydown', trap);
    return () => { window.removeEventListener('keydown', trap); if (prior instanceof HTMLElement && prior.isConnected) prior.focus(); };
  }, [overlay]);

  function ground(event) {
    if (blocked.current) return;
    const current = gameRef.current;
    if (current.phase === 'station') {
      const count = current.petCount + 1;
      current.petCount = count;
      current.effects.push({ x: 175 + Math.random() * 55, y: 305, at: current.clock });
      tell(current, count < 3 ? '它轻轻蹭了蹭你的手' : '它在这里安心下来了', 3);
      redraw();
      return;
    }
    if (current.phase !== 'maze') return;
    const svg = sceneRef.current;
    const matrix = svg.getScreenCTM();
    if (!matrix) return;
    const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
    const col = Math.floor((point.x - MAP_X) / TILE);
    const row = Math.floor((point.y - MAP_Y) / TILE);
    directionRef.current = null;
    queueRef.current = tapMaze(current, col, row);
  }
  function returnToStation() {
    if (gameRef.current.phase !== 'success') return;
    gameRef.current.phase = 'station';
    setSaved(value => value + 1);
    tell(gameRef.current, '欢迎回家，小家伙', 30);
    redraw();
  }
  const stageIndex = game.phase === 'maze' ? 0 : game.phase === 'station' ? 2 : 1;
  const phaseTitle = ['穿过花园', '把它安全接住', '平安到站'][stageIndex];
  const message = game.clock < game.statusUntil ? game.status : game.phase === 'maze' ? '小家伙就在花园的另一头' : game.phase === 'rescue' ? game.animal.eating > 0 ? '它正安心地吃东西' : '小家伙正在草地上走动' : '欢迎回家，小家伙';
  const disabled = Boolean(game.net) || Boolean(overlay);
  return <main className="rescue-shell" data-frame={frame}>
    <header className="rescue-header">
      <div className="rescue-brand"><span className="brand-icon"><PawPrint size={23} /></span><div><h1>毛茸茸救助站</h1><p>河畔花园 · 外出救助</p></div></div>
      <div className="header-actions">
        <button className="icon-button" aria-label="暂停" title="暂停" disabled={Boolean(overlay) || game.phase === 'station'} onClick={() => { clearMovement(); setPaused(true); }}><Pause size={18} /></button>
        <button className="icon-button" aria-label="试玩设置" title="试玩设置" disabled={Boolean(overlay)} onClick={() => { clearMovement(); setSettings(true); }}><Settings2 size={19} /></button>
      </div>
    </header>
    <nav className="journey" aria-label="救助进度">
      {[['外出寻路', Footprints], ['动物救助', ShoppingBasket], ['安全回站', Home]].map(([label, Icon], index) => <React.Fragment key={label}>{index > 0 && <ChevronRight size={12} className="journey-arrow" />}<span className={index === stageIndex ? 'current' : index < stageIndex ? 'complete' : ''} aria-current={index === stageIndex ? 'step' : undefined}>{index < stageIndex ? <Check size={13} /> : <Icon size={13} />}{label}</span></React.Fragment>)}
    </nav>
    <section className="playfield" aria-label={phaseTitle}>
      <div className="scene-hud"><span><MapPin size={14} />{phaseTitle}</span><span className="elapsed" aria-label={`用时 ${formatTime(game.elapsed)}`}>{formatTime(game.elapsed)}</span></div>
      <Scene game={game} onGround={ground} sceneRef={sceneRef} moving={Boolean(directionRef.current || queueRef.current.length)} />
      {game.phase === 'rescue' && <div className="range-tag"><ScanLine size={14} /><span>网兜 2.1 m</span></div>}
      {overlay && <div className="overlay">
        <section className={`modal ${overlay === 'success' ? 'success-modal' : ''}`} role="dialog" aria-modal="true" aria-labelledby="modal-title" ref={modalRef}>
          {overlay === 'settings' ? <>
            <button className="icon-button close-modal" aria-label="关闭设置" title="关闭" onClick={() => setSettings(false)}><X size={19} /></button>
            <h2 id="modal-title">这次去哪里找它？</h2>
            <fieldset><legend>花园入口</legend><div className="segmented">{[['bottom', '南门 · 下方'], ['top', '北门 · 上方']].map(([value, label]) => <button key={value} aria-pressed={entrance === value} onClick={() => setEntrance(value)}>{label}</button>)}</div></fieldset>
            <fieldset><legend>遇见的小动物</legend><div className="segmented">{[['cat', '小猫'], ['dog', '小狗']].map(([value, label]) => <button key={value} aria-pressed={kind === value} onClick={() => setKind(value)}>{label}</button>)}</div></fieldset>
            <button className="primary-button" onClick={() => restart()}><RotateCcw size={17} />开始新一轮</button>
          </> : overlay === 'paused' ? <>
            <span className="modal-mark"><Pause size={25} /></span><h2 id="modal-title">休息一下</h2>
            <button className="primary-button" onClick={() => setPaused(false)}><Play size={17} />继续救助</button>
            <button className="quiet-button" onClick={() => restart()}><RotateCcw size={15} />重新出发</button>
          </> : <>
            <div className="success-animal"><Pet kind={game.kind} happy /><span><Heart size={22} fill="currentColor" /></span></div>
            <p className="success-kicker"><Check size={14} />救助成功</p><h2 id="modal-title">接住你啦。</h2><p className="success-copy">别怕，我们带你回家。</p>
            <div className="result-stats"><span><strong>{formatTime(game.elapsed)}</strong>本次用时</span><span><strong>{game.casts}</strong>次出网</span><span><strong>{game.foodUsed}</strong>份食物</span></div>
            <button className="primary-button" onClick={returnToStation}><Home size={17} />带它回站</button>
          </>}
        </section>
      </div>}
    </section>
    <div className="status-line" role="status" aria-live="polite"><span className="status-dot" />{message}</div>
    <section className={`control-deck ${game.phase === 'station' ? 'station-deck' : ''}`} aria-label="救助操作" inert={overlay ? true : undefined}>
      {game.phase === 'station' ? <>
        <div className="station-note"><PawPrint size={21} /><div><strong>又一个小家伙，安全到家</strong><span>本次试玩已救助 {saved} 只</span></div></div>
        <button className="primary-button" onClick={() => { const next = game.kind === 'cat' ? 'dog' : 'cat'; setKind(next); restart(entrance, next); }}><Footprints size={18} />再去救助</button>
      </> : <>
        <div className="dpad" aria-label="方向控制">
          <DirectionButton direction="up" Icon={ArrowUp} onPress={press} onRelease={() => { directionRef.current = null; }} disabled={disabled} />
          <DirectionButton direction="left" Icon={ArrowLeft} onPress={press} onRelease={() => { directionRef.current = null; }} disabled={disabled} />
          <span className="dpad-center"><PawPrint size={15} /></span>
          <DirectionButton direction="right" Icon={ArrowRight} onPress={press} onRelease={() => { directionRef.current = null; }} disabled={disabled} />
          <DirectionButton direction="down" Icon={ArrowDown} onPress={press} onRelease={() => { directionRef.current = null; }} disabled={disabled} />
        </div>
        <div className="rescue-tools">
          <button className="food-button" onClick={food} disabled={game.phase !== 'rescue' || disabled || Boolean(game.bait) || game.foodLeft === 0} title="投食" aria-label={`投食，剩余 ${game.foodLeft} 份`}><Fish size={24} /><span>投食 <b>{game.foodLeft}</b></span></button>
          <button className="net-button" onClick={net} disabled={game.phase !== 'rescue' || disabled} title="出网" aria-label="出网"><ShoppingBasket size={30} strokeWidth={1.7} /><span>{game.net ? '收网中' : '出网'}</span></button>
        </div>
      </>}
    </section>
    <footer className="rescue-footer"><span>外出救助 · 玩法试作 01</span><a href="/" title="打开原有养宠原型">原养宠原型 <ChevronRight size={11} /></a></footer>
  </main>;
}
