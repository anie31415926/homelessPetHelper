import React, { useEffect, useRef, useState } from 'react';
import { ArrowDown, ArrowLeft, ArrowRight, ArrowUp, Check, ChevronRight, Clock3, Droplets, Fish, Footprints, Hand, Heart, Home, MapPin, Pause, PawPrint, Play, RectangleHorizontal, RotateCcw, Scissors, Settings2, Shield, Timer, ToyBrick, Undo2, X } from 'lucide-react';
import LabScene from '../playlab/LabScene.jsx';
import { bridgeAt, canClean, center, cellAt, cleanAt, slideBox, undoPuzzle } from '../playlab/engine.js';
import EncounterScene from './EncounterScene.jsx';
import { DISTANCES, PROFILES, actionOptions, beginNegotiation, chooseAction, createOuting, cueFor, updateOuting } from './engine.js';

const formatTime = seconds => `${Math.floor(seconds / 60).toString().padStart(2, '0')}:${Math.floor(seconds % 60).toString().padStart(2, '0')}`;
const initialOptions = () => {
  const params = new URLSearchParams(location.search);
  const seed = params.has('seed') && /^\d+$/.test(params.get('seed')) ? Number(params.get('seed')) >>> 0 : undefined;
  return { seed, route: params.get('route'), profile: params.get('profile'), direct: params.get('stage') === 'talk' };
};
const Tool = ({ Icon, label, active, onClick }) => <button className={`lab-tool ${active ? 'active' : ''}`} aria-pressed={active} onClick={onClick}><Icon size={23} /><span>{label}</span></button>;
const actionIcons = { food: Fish, wait: Clock3, toy: ToyBrick, approach: Footprints, invite: Home };

export default function OutingApp() {
  const gameRef = useRef(null);
  if (!gameRef.current) gameRef.current = createOuting(initialOptions());
  const [frame, setFrame] = useState(0);
  const [overlay, setOverlay] = useState(null);
  const [routeSetting, setRouteSetting] = useState('random');
  const [profileSetting, setProfileSetting] = useState('random');
  const [direct, setDirect] = useState(false);
  const gesture = useRef(null);
  const blocked = useRef(false);
  const svgRef = useRef(null);
  const modalRef = useRef(null);
  const controlsRef = useRef(null);
  const game = gameRef.current;
  const approach = game.approach;
  blocked.current = Boolean(overlay);
  const redraw = () => setFrame(value => value + 1);
  const clear = () => { gesture.current = null; gameRef.current.approach.brush = null; gameRef.current.approach.drag = null; };

  function newOuting(same = false) {
    clear();
    gameRef.current = createOuting(same ? { seed: game.seed, route: game.route, profile: game.profile, direct: game.approachTime === 0 && game.stage !== 'approach' } : { route: routeSetting, profile: profileSetting, direct });
    setOverlay(null); redraw();
  }

  useEffect(() => {
    let request;
    let previous = performance.now();
    let lastFrame = 0;
    const animate = now => {
      const dt = Math.min((now - previous) / 1000, .05);
      previous = now;
      if (!blocked.current && !document.hidden) {
        const current = gameRef.current;
        if (current.stage === 'approach' && current.approach.brush) cleanAt(current.approach, current.approach.brush, dt);
        updateOuting(current, dt);
        if (now - lastFrame >= 32) { redraw(); lastFrame = now; }
      }
      request = requestAnimationFrame(animate);
    };
    request = requestAnimationFrame(animate);
    window.addEventListener('blur', clear);
    document.addEventListener('visibilitychange', clear);
    return () => { cancelAnimationFrame(request); window.removeEventListener('blur', clear); document.removeEventListener('visibilitychange', clear); };
  }, []);

  useEffect(() => {
    if (!overlay) return;
    clear();
    const prior = document.activeElement;
    const modal = modalRef.current;
    modal?.querySelector('button')?.focus();
    const keydown = event => {
      if (event.key === 'Escape') setOverlay(null);
      if (event.key !== 'Tab') return;
      const elements = [...modal.querySelectorAll('button:not(:disabled),input:not(:disabled)')];
      const first = elements[0]; const last = elements.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    window.addEventListener('keydown', keydown);
    return () => { window.removeEventListener('keydown', keydown); if (prior instanceof HTMLElement && prior.isConnected) prior.focus(); };
  }, [overlay]);

  useEffect(() => {
    if (game.stage !== 'approach') controlsRef.current?.querySelector('button:not(:disabled)')?.focus({ preventScroll: true });
  }, [game.stage]);

  function point(event) {
    const matrix = svgRef.current?.getScreenCTM();
    return matrix ? new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse()) : null;
  }
  function cell(col, row) {
    if (blocked.current || game.stage !== 'approach') return;
    if (approach.tool === 'bridge') bridgeAt(approach, col, row);
    else if (approach.selected) slideBox(approach, approach.selected, col, row);
    redraw();
  }
  function box(id, col, row) {
    if (blocked.current || game.stage !== 'approach') return;
    if (col !== undefined) slideBox(approach, id, col, row);
    else { approach.selected = id; approach.tool = 'move'; }
    redraw();
  }
  function cleanItem(item) {
    if (blocked.current || game.stage !== 'approach') return;
    if (approach.tool !== item.type) approach.message = '换个合适的工具吧';
    else if (!canClean(approach, item)) approach.message = '先从靠近门口的地方开路';
    else cleanAt(approach, center(item.col, item.row), 1);
    redraw();
  }
  function meet() {
    if (blocked.current) return;
    clear(); beginNegotiation(game); redraw();
  }
  const handlers = {
    onPointerDown(event) {
      if (blocked.current || game.stage !== 'approach' || (event.button !== 0 && event.pointerType !== 'touch')) return;
      event.preventDefault();
      const p = point(event); if (!p) return;
      event.currentTarget.setPointerCapture(event.pointerId);
      if (event.target.closest('[data-pet]')) { meet(); return; }
      const id = event.target.closest('[data-box]')?.dataset.box;
      if (game.route === 'cleanup') {
        const itemId = event.target.closest('[data-junk]')?.dataset.junk;
        const item = approach.items.find(value => value.id === itemId);
        if (item && !canClean(approach, item)) approach.message = '先从靠近门口的地方开路';
        else if (item && item.type !== approach.tool) approach.message = '换个合适的工具吧';
        approach.brush = { x: p.x, y: p.y };
        gesture.current = { type: 'clean', pointer: event.pointerId };
        cleanAt(approach, p, .06);
      } else if (id && approach.tool === 'move') {
        approach.selected = id;
        gesture.current = { type: 'box', id, pointer: event.pointerId, start: p };
      } else gesture.current = { type: 'ground', pointer: event.pointerId };
      redraw();
    },
    onPointerMove(event) {
      const value = gesture.current;
      if (!value || blocked.current || value.pointer !== event.pointerId) return;
      const p = point(event); if (!p) return;
      if (value.type === 'clean') approach.brush = { x: p.x, y: p.y };
      else if (value.type === 'box' && Math.hypot(p.x - value.start.x, p.y - value.start.y) > 8) approach.drag = { x: p.x, y: p.y };
    },
    onPointerUp(event) {
      const value = gesture.current;
      if (!value || value.pointer !== event.pointerId) return;
      const p = point(event); clear();
      if (p) {
        const destination = cellAt(p);
        if (value.type === 'box') slideBox(approach, value.id, destination.col, destination.row);
        else if (value.type === 'ground') cell(destination.col, destination.row);
      }
      redraw();
    },
    onPointerCancel() { clear(); redraw(); },
    onLostPointerCapture() { if (gesture.current) { clear(); redraw(); } },
  };
  function act(id) { if (!blocked.current) { chooseAction(game, id); redraw(); } }
  const selected = approach.boxes?.find(value => value.id === approach.selected);
  const steps = game.stage === 'approach' ? 0 : game.stage === 'station' ? 2 : 1;
  const turn = game.turn;
  const atStation = game.stage === 'station';
  const success = game.stage === 'success';
  const arrowDefs = [[ArrowUp, 0, -1, '向上'], [ArrowLeft, -1, 0, '向左'], [ArrowRight, 1, 0, '向右'], [ArrowDown, 0, 1, '向下']];
  return <main className={`outing-shell stage-${game.stage} route-${game.route}`} data-frame={frame} data-stage={game.stage} data-route={game.route} data-profile={game.profile} data-seed={game.seed}>
    <div className="outing-content" inert={overlay ? true : undefined}>
      <header className="lab-header"><div className="lab-brand"><span><PawPrint size={23} /></span><div><h1>毛茸茸救助站</h1><p>一次温柔的救助</p></div></div><div className="lab-header-tools"><button className="lab-icon" aria-label="暂停" title="暂停" disabled={success || atStation} onClick={() => setOverlay('pause')}><Pause size={18} /></button><button className="lab-icon" aria-label="试玩设置" title="试玩设置" onClick={() => setOverlay('settings')}><Settings2 size={19} /></button></div></header>
      <nav className="outing-steps" aria-label="本次救助进度">{[['开路寻踪', Footprints], ['建立信任', Heart], ['安全回站', Home]].map(([label, Icon], index) => <React.Fragment key={label}>{index > 0 && <ChevronRight size={12} />}<span className={index === steps ? 'current' : index < steps ? 'complete' : ''} aria-current={index === steps ? 'step' : undefined}>{index < steps ? <Check size={12} /> : <Icon size={12} />}{label}</span></React.Fragment>)}</nav>
      <section className="outing-mission"><div><p><MapPin size={11} />{game.route === 'puzzle' ? '雨后的旧仓库' : '荒草里的小庭院'}</p><h2>{atStation ? '欢迎回家，小家伙' : PROFILES[game.profile].name}</h2></div><div><strong>{formatTime(game.elapsed)}</strong><span>{game.stage === 'approach' ? game.route === 'puzzle' ? '挪箱 · 搭桥' : '清理 · 开路' : `第 ${turn.round} 回合`}</span></div></section>
      <section className="outing-scene" aria-label={game.stage === 'approach' ? '开路现场' : '交涉现场'}>
        {game.stage === 'approach' ? <LabScene game={approach} svgRef={svgRef} handlers={handlers} onCell={cell} onBox={box} onJunk={cleanItem} onPet={meet} /> : <EncounterScene game={game} onPet={() => { game.pats++; redraw(); }} />}
        {game.stage === 'approach' && game.ready && <span className="path-open"><Check size={13} />通道已打通</span>}
        {game.stage === 'negotiate' && <div className="alert-hud"><div><Shield size={14} /><span>警惕</span><strong>{turn.alert}</strong><span className="distance-label">{DISTANCES[turn.distance]}</span></div><div className="alert-track"><i style={{ width: `${turn.alert}%` }} /></div>{turn.phase === 'react' && turn.delta !== 0 && <span className={`alert-delta ${turn.delta > 0 ? 'increase' : ''}`} key={turn.round}>{turn.delta > 0 ? '+' : ''}{turn.delta}</span>}</div>}
      </section>
      <section className="outing-narrative" role="status" aria-live="polite">
        {game.stage === 'approach' ? <p><span className="narrative-dot" />{game.ready ? '路通了。慢慢走过去，和它打个招呼吧。' : approach.message}</p> : success ? <><span className="narrative-who">救助成功</span><p>它自己走进了箱子。我们回家吧。</p></> : atStation ? <><span className="narrative-who">平安到站</span><p>{game.pats > 0 ? '它轻轻蹭着你的手，已经安心下来了。' : '刚才还躲着的小家伙，正安静地依偎在软垫上。'}</p></> : turn.phase === 'react' ? <><span className="narrative-who">它的回应</span><p>{turn.reaction}</p></> : <>{turn.log.length > 0 ? <p className="previous-response">{turn.reaction}</p> : <span className="narrative-who">它现在的样子</span>}<p className="animal-cue">{cueFor(turn)}</p></>}
      </section>
      <section className="outing-controls" aria-label="本回合行动" ref={controlsRef}>
        {game.stage === 'approach' ? <>
          <div className="approach-tools">{game.route === 'puzzle' ? <><Tool Icon={Hand} label="搬移" active={approach.tool === 'move'} onClick={() => { approach.tool = 'move'; redraw(); }} /><Tool Icon={RectangleHorizontal} label="木板" active={approach.tool === 'bridge'} onClick={() => { approach.tool = 'bridge'; redraw(); }} /><button className="lab-icon" aria-label="撤销一步" title="撤销一步" disabled={!approach.history.length} onClick={() => { undoPuzzle(approach); redraw(); }}><Undo2 size={22} /></button></> : [['litter', Hand, '捡拾'], ['weeds', Scissors, '修剪'], ['mud', Droplets, '冲洗']].map(([value, Icon, label]) => <Tool key={value} Icon={Icon} label={label} active={approach.tool === value} onClick={() => { approach.tool = value; redraw(); }} />)}<button className="meet-button" disabled={!game.ready} onClick={meet}><Heart size={22} /><span>走近它</span></button></div>
          {game.route === 'puzzle' && <div className="box-arrows"><span>木箱{selected ? ` ${selected.id.toUpperCase()}` : ''}</span>{arrowDefs.map(([Icon, dx, dy, label]) => <button key={label} className="lab-arrow" aria-label={`木箱${label}`} title={`木箱${label}`} disabled={!selected} onClick={() => box(selected.id, selected.col + dx, selected.row + dy)}><Icon size={20} /></button>)}</div>}
        </> : game.stage === 'negotiate' ? <>
          <div className="turn-heading"><span>{turn.phase === 'choose' ? '轮到你了' : '它正在回应'}</span><span>{turn.phase === 'choose' ? '不着急，慢慢来' : <><Timer size={11} />稍等片刻</>}</span></div>
          <div className="turn-actions">{actionOptions(turn).map(option => { const Icon = actionIcons[option.id]; return <button key={option.id} className={option.id === 'invite' ? 'invite-action' : ''} disabled={turn.phase !== 'choose' || option.disabled} aria-label={option.label} onClick={() => act(option.id)}><Icon size={24} /><span><strong>{option.label}</strong><small>{option.detail}</small></span></button>; })}</div>
        </> : success ? <div className="outing-finish"><div><Heart size={22} /><span><strong>这次，是你获得了它的信任。</strong><small>{turn.log.length} 回合相处 · 开路 {formatTime(game.approachTime)} · 总用时 {formatTime(game.elapsed)}</small></span></div><button className="lab-primary" onClick={() => { game.stage = 'station'; redraw(); }}><Home size={17} />带它回站</button></div> : <div className="outing-finish"><button className="station-stroke" onClick={() => { game.pats++; redraw(); }}><Hand size={21} />轻轻抚摸</button><button className="lab-primary" onClick={() => newOuting()}><Footprints size={17} />再次外出</button></div>}
      </section>
      <footer className="lab-footer"><span>{game.stage === 'approach' ? '外出救助 · 开路阶段' : atStation ? '小小救助站，多了一位新朋友' : '外出救助 · 回合交涉'}</span><a href="/playlab.html">三个旧试玩 <ChevronRight size={11} /></a></footer>
    </div>
    {overlay && <div className="lab-overlay"><section className="lab-modal outing-modal" role="dialog" aria-modal="true" aria-labelledby="outing-modal-title" ref={modalRef}><button className="lab-icon modal-close" aria-label="关闭弹窗" title="关闭" onClick={() => setOverlay(null)}><X size={18} /></button>
      {overlay === 'pause' ? <><span className="pause-mark"><Pause size={26} /></span><h2 id="outing-modal-title">在这里歇一会儿</h2><button className="lab-primary" onClick={() => setOverlay(null)}><Play size={16} />继续救助</button><button className="modal-restart" onClick={() => newOuting(true)}><RotateCcw size={15} />重玩同一次相遇</button></> : <><h2 id="outing-modal-title">下一次相遇</h2><fieldset><legend>开路方式</legend><div className="outing-segmented">{[['random', '随机'], ['puzzle', '场景解谜'], ['cleanup', '清理开路']].map(([value, label]) => <button key={value} aria-pressed={routeSetting === value} onClick={() => setRouteSetting(value)}>{label}</button>)}</div></fieldset><fieldset><legend>动物的初始状态</legend><div className="outing-profiles">{[['random', '随机'], ['hungry', '有些饿'], ['shy', '很谨慎'], ['curious', '爱好奇']].map(([value, label]) => <button key={value} aria-pressed={profileSetting === value} onClick={() => setProfileSetting(value)}>{label}</button>)}</div></fieldset><label className="direct-toggle"><input type="checkbox" checked={direct} onChange={event => setDirect(event.target.checked)} />直接体验回合交涉</label><button className="lab-primary" onClick={() => newOuting()}><Footprints size={17} />开始新的外出</button><button className="modal-restart" onClick={() => newOuting(true)}><RotateCcw size={14} />重玩同一次相遇</button></>}
    </section></div>}
  </main>;
}
