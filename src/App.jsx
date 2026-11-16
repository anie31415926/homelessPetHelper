import React, { Suspense, lazy, useEffect, useRef, useState } from 'react';
import { ArrowLeft, ArrowRight, BookOpen, Brush, Cat, Check, ChevronRight, Coins, Dog, Droplets, Flower2, Gift, Heart, House, Moon, Package, PawPrint, Pencil, Pill, Plus, RotateCcw, RotateCw, Scissors, Settings2, ShowerHead, Sparkles, Stethoscope, Sun, Thermometer, Utensils, Volume2, VolumeX, Wind, X } from 'lucide-react';
import { Pet, FoodArt } from './Pet.jsx';
import { CARE_TASKS, FOOD, SAVE_KEY, care, freshGame, loadGame, nextDay, nightBlocker, stageOf } from './game.js';

const PetWorld = lazy(() => import('./PetWorld.jsx'));
class SceneBoundary extends React.Component {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    return this.state.failed ? <div className="webgl-fallback"><p>立体小窝暂时没有打开</p><button onClick={this.props.onFailure}>回到绘本小窝</button></div> : this.props.children;
  }
}
function initialView() {
  const query = new URLSearchParams(location.search).get('view');
  if (query === '2d' || query === '3d') return query;
  try { return localStorage.getItem('fluffy-days-view') === '2d' ? '2d' : '3d'; } catch { return '3d'; }
}

const STEPS = {
  bath: [
    { title: '把小泥巴揉进泡泡里', label: '打泡泡', count: 5, icon: Droplets },
    { title: '冲掉泡泡，露出软软的毛', label: '冲干净', count: 5, icon: ShowerHead },
    { title: '暖暖的风，蓬蓬的毛', label: '吹蓬松', count: 3, icon: Wind },
  ],
  groom: [{ title: '把打结的毛毛慢慢梳开', label: '梳毛毛', count: 6, icon: Brush }, { title: '再修一修，就漂漂亮亮啦', label: '修整毛毛', count: 3, icon: Scissors }],
  doctor: [{ title: '先量一量体温', label: '测体温', count: 1, icon: Thermometer }, { title: '听听小心脏的声音', label: '听心跳', count: 1, icon: Stethoscope }, { title: '医生配好了营养药剂', label: '吃药药', count: 1, icon: Pill }],
  play: [{ title: '追到小毛线球，就开心一点', label: '追毛线球', count: 5, icon: Heart }],
};
const ACTIONS = [
  { id: 'feed', label: '喂食', sub: '吃饱饱', icon: Utensils, color: 'honey' },
  { id: 'bath', label: '洗澡', sub: '洗香香', icon: Droplets, color: 'aqua' },
  { id: 'groom', label: '护理', sub: '变漂亮', icon: Brush, color: 'rose' },
  { id: 'doctor', label: '就医', sub: '身体棒', icon: Stethoscope, color: 'lilac' },
  { id: 'play', label: '陪玩', sub: '好开心', icon: Heart, color: 'leaf' },
];
const TASK_ICONS = { feed: Utensils, bath: Droplets, groom: Brush, doctor: Stethoscope };

function Modal({ title, children, onClose, className = '' }) {
  const ref = useRef(null);
  useEffect(() => { ref.current.showModal(); }, []);
  return <dialog ref={ref} className={`modal ${className}`} aria-label={title} onCancel={event => { event.preventDefault(); onClose(); }} onClick={event => { if (event.target === ref.current) { const r = ref.current.getBoundingClientRect(); if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) onClose(); } }}>
    <div className="modal-heading"><h2>{title}</h2><button className="icon-button" onClick={onClose} aria-label="关闭" title="关闭"><X size={20} /></button></div>
    {children}
  </dialog>;
}

function TaskList({ pet, onClaim, compact = false }) {
  const count = CARE_TASKS.filter(task => pet.tasks.includes(task.id)).length;
  return <div className={`task-list ${compact ? 'compact' : ''}`}>
    {CARE_TASKS.map(task => { const Icon = TASK_ICONS[task.id]; const done = pet.tasks.includes(task.id); return <div className={`task-row ${done ? 'done' : ''}`} key={task.id}><span className="task-icon"><Icon size={17} /></span><span>{task.label}</span><span className="task-check">{done ? <Check size={14} /> : <span />}</span></div>; })}
    <button className="reward-button" onClick={onClaim} disabled={count < 4 || pet.claimed}><Gift size={16} />{pet.claimed ? '今日奖励已领取' : count === 4 ? '领取 45 枚爪爪币' : `${count} / 4 完成 · 奖励 45`}<Coins size={14} /></button>
  </div>;
}

function GrowthTrack({ pet }) {
  const stage = stageOf(pet.xp);
  return <div className="growth-track">{['幼年', '成长', '成年'].map((label, index) => <div className={index <= stage.index ? 'reached' : ''} key={label}><span>{index < stage.index ? <Check size={12} /> : <PawPrint size={12} />}</span><small>{label}</small></div>)}</div>;
}

export default function App() {
  const [state, setState] = useState(() => loadGame(window.localStorage));
  const [panel, setPanel] = useState(null);
  const [bagTab, setBagTab] = useState('food');
  const [session, setSession] = useState(null);
  const [toast, setToast] = useState(null);
  const [burst, setBurst] = useState(null);
  const [meal, setMeal] = useState('kibble');
  const [draftName, setDraftName] = useState('');
  const [saveError, setSaveError] = useState(false);
  const [view, setView] = useState(initialView);
  const [cameraTurn, setCameraTurn] = useState(0);
  const [cameraReset, setCameraReset] = useState(0);
  const stroke = useRef(0);
  const audio = useRef(null);
  const pet = state.pets[state.selected];
  const stage = stageOf(pet.xp);
  const taskCount = CARE_TASKS.filter(task => pet.tasks.includes(task.id)).length;
  const currentStep = session ? STEPS[session.type][session.step] : null;
  const doneProgress = session ? (session.step + session.hits / currentStep.count) / STEPS[session.type].length : 0;
  const dirty = (100 - pet.clean) / 100;
  const cleanedSpots = session?.type === 'bath' ? (session.step === 0 ? session.hits : session.step === 1 ? 5 + session.hits : 8) : 0;
  const messy = !pet.groomed && !(session?.type === 'groom' && doneProgress > .6);
  const scene = session?.type === 'bath' ? 'bathroom' : session?.type === 'doctor' ? 'clinic' : 'room';
  const location = { room: '暖暖小窝', bathroom: '泡泡浴室', clinic: '森林诊所' }[scene];

  function changeView(next) {
    setView(next);
    try { localStorage.setItem('fluffy-days-view', next); } catch { /* View selection remains usable without local storage. */ }
  }

  useEffect(() => {
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(state)); setSaveError(false); }
    catch { setSaveError(true); }
  }, [state]);
  useEffect(() => { if (!toast) return; const timer = setTimeout(() => setToast(null), 2900); return () => clearTimeout(timer); }, [toast]);
  useEffect(() => { if (!burst) return; const timer = setTimeout(() => setBurst(null), 1200); return () => clearTimeout(timer); }, [burst]);
  function notify(text) { setToast({ text, id: Date.now() }); }
  function chime() {
    if (!state.sound) return;
    try {
      const Context = window.AudioContext || window.webkitAudioContext;
      audio.current ||= new Context();
      const ctx = audio.current;
      if (ctx.state === 'suspended') ctx.resume();
      [660, 880].forEach((frequency, index) => {
        const oscillator = ctx.createOscillator(); const gain = ctx.createGain();
        oscillator.type = 'sine'; oscillator.frequency.value = frequency;
        gain.gain.setValueAtTime(0, ctx.currentTime + index * .09);
        gain.gain.linearRampToValueAtTime(.035, ctx.currentTime + index * .09 + .02);
        gain.gain.exponentialRampToValueAtTime(.001, ctx.currentTime + index * .09 + .25);
        oscillator.connect(gain); gain.connect(ctx.destination);
        oscillator.start(ctx.currentTime + index * .09); oscillator.stop(ctx.currentTime + index * .09 + .3);
      });
    } catch { /* Sound is optional when a browser blocks Web Audio. */ }
  }
  function selectPet(id) {
    if (!state.pets[id].adopted) { setDraftName(state.pets[id].name); setPanel('adopt'); return; }
    setState(previous => ({ ...previous, selected: id })); setSession(null); setBurst(null);
  }
  function completeCare(type, foodId) {
    const next = care(state, type, foodId);
    if (next === state) { notify(pet.hunger >= 100 ? '已经吃饱啦，留一点下次吃吧' : '食物不够了，去背包补充一点吧'); return; }
    if (type === 'feed') setMeal(foodId);
    setState(next); setSession(null); setPanel(null); setBurst(type); chime();
    const xp = next.pets[state.selected].xp - pet.xp;
    notify({ feed: '肚子暖暖的，心也暖暖的', bath: '洗干净啦！是香香的小可爱', groom: '毛毛蓬松又整齐，真漂亮', doctor: '恢复元气啦，勇敢的小家伙', play: '今天又更喜欢你一点了' }[type] + (xp ? `  +${xp} 成长` : ''));
    if (stageOf(next.pets[state.selected].xp).index > stage.index) setPanel('milestone');
  }
  function startAction(type) {
    if (type === 'feed') { setPanel('feed'); return; }
    if (type === 'groom' && pet.clean < 60) { notify('先把小泥巴洗干净，再来梳毛吧'); type = 'bath'; }
    setSession({ type, step: 0, hits: 0 }); setBurst(null);
  }
  function careHit() {
    stroke.current = Date.now();
    if (!session) {
      setState(previous => ({ ...previous, pets: { ...previous.pets, [previous.selected]: { ...previous.pets[previous.selected], mood: Math.min(100, previous.pets[previous.selected].mood + 2) } } }));
      setBurst('pet'); chime(); return;
    }
    setBurst(`stroke-${Date.now()}`);
    if (session.hits + 1 < currentStep.count) { setSession({ ...session, hits: session.hits + 1 }); return; }
    if (session.step + 1 < STEPS[session.type].length) {
      if (session.type === 'doctor') notify(session.step === 0 ? (pet.health < 75 ? '39.8°C，有一点发热' : '38.5°C，体温正常') : '扑通扑通，心跳很有力');
      setSession({ ...session, step: session.step + 1, hits: 0 }); chime();
    } else completeCare(session.type);
  }
  function claimReward() {
    if (taskCount < 4 || pet.claimed) return;
    setState(previous => ({ ...previous, coins: previous.coins + 45, pets: { ...previous.pets, [previous.selected]: { ...previous.pets[previous.selected], claimed: true } } }));
    notify('今天照顾得真好！爪爪币 +45'); chime();
  }
  function buyFood(food) {
    if (state.coins < food.cost) { notify('爪爪币不够啦，完成照护或进入新一天可获得'); return; }
    setState(previous => ({ ...previous, coins: previous.coins - food.cost, inventory: { ...previous.inventory, [food.id]: previous.inventory[food.id] + 1 } }));
    notify(`${food.name} +1`); chime();
  }
  function sleep() {
    const reason = nightBlocker(pet);
    if (reason) { notify(reason); return; }
    setSession(null); setPanel('night');
  }
  function advanceDay() {
    const next = nextDay(state);
    setState(next); setPanel(null); setSession(null); notify('早安！今天的照护基金 +60'); chime();
    if (stageOf(next.pets[state.selected].xp).index > stage.index) setPanel('milestone');
  }
  function saveName() {
    const name = draftName.trim().slice(0, 8);
    if (!name) return;
    if (panel === 'adopt') {
      setState(previous => ({ ...previous, selected: 'dog', pets: { ...previous.pets, dog: { ...previous.pets.dog, name, adopted: true } }, journal: [{ day: previous.day, pet: 'dog', text: `${name}来到了新家，从今天起不再流浪。`, type: 'adopt' }, ...previous.journal] }));
      setSession(null); notify(`${name}，欢迎回家`);
    } else setState(previous => ({ ...previous, pets: { ...previous.pets, [previous.selected]: { ...previous.pets[previous.selected], name } } }));
    setPanel(null); chime();
  }
  const speech = session ? { bath: ['咕噜咕噜，好多泡泡～', '小泥巴，拜拜啦！', '呼呼，好暖和呀～'][session.step], groom: ['轻一点哦，毛毛打结了…', '我是不是变漂亮了？'][session.step], doctor: ['有你在，我就不害怕啦', '扑通，扑通…', '吃完就能好起来啦'][session.step], play: ['再陪我玩一会儿嘛～'][0] }[session.type] : pet.health < 75 ? '今天有一点点不舒服…' : pet.hunger < 60 ? '肚子在咕噜咕噜啦…' : pet.clean < 60 ? '想洗一个香香的澡～' : !pet.groomed ? '帮我梳梳毛好不好？' : '最喜欢和你在一起啦！';
  const growthPercent = Math.min(100, (pet.xp - stage.start) / (stage.next - stage.start) * 100);

  return <>
    <header className="site-header">
      <a className="brand" href="/" aria-label="毛茸茸小日子首页"><span className="brand-mark"><PawPrint size={25} strokeWidth={1.6} /></span><span>毛茸茸<span className="brand-small">小日子<span className="brand-flower">✿</span></span></span></a>
      <span className="header-note">把平凡的每一天，养成喜欢的模样。</span>
      <div className="header-tools"><button onClick={() => setPanel('growth')} className="album-button"><BookOpen size={17} /><span>成长相册</span></button><button className="icon-button" title={state.sound ? '关闭音效' : '开启音效'} aria-label={state.sound ? '关闭音效' : '开启音效'} onClick={() => setState(previous => ({ ...previous, sound: !previous.sound }))}>{state.sound ? <Volume2 size={19} /> : <VolumeX size={19} />}</button><button className="icon-button settings" onClick={() => setPanel('settings')} title="设置" aria-label="设置"><Settings2 size={18} /></button></div>
    </header>

    <main className="game-layout">
      <aside className="left-diary">
        <div className="section-title"><PawPrint size={16} /><h2>家里的小可爱</h2><span>{Object.values(state.pets).filter(item => item.adopted).length.toString().padStart(2, '0')} / 02</span></div>
        <div className="pet-roster">{['cat', 'dog'].map(id => <button key={id} className={`roster-pet ${state.selected === id ? 'selected' : ''}`} onClick={() => selectPet(id)} aria-pressed={state.selected === id} aria-label={state.pets[id].adopted ? `选择${state.pets[id].name}` : `收养${state.pets[id].name}`}><span className={`roster-art ${id}`}><Pet kind={id} /></span><span className="roster-copy"><strong>{state.pets[id].name}</strong><small>{state.pets[id].adopted ? `${id === 'cat' ? '布偶猫' : '可卡犬'} · ${stageOf(state.pets[id].xp).name}` : '在等一个温暖的家'}</small></span><span className="roster-indicator">{state.pets[id].adopted ? state.selected === id ? <Heart size={14} fill="currentColor" /> : <ChevronRight size={15} /> : <Plus size={16} />}</span></button>)}</div>
        <section className="daily-section"><div className="section-title"><Sun size={17} /><h2>今日小心愿</h2><span>每天一点爱</span></div><TaskList pet={pet} onClaim={claimReward} /></section>
        <div className="daily-note"><Flower2 size={26} strokeWidth={1.2} /><p>不用着急长大，<br />每一天都值得被好好照顾。</p><span className="note-line" /></div>
        <div className="weather"><Sun size={27} strokeWidth={1.3} /><div>小镇的午后<small>晴天 · 22°C</small></div><span>好适合晒晒太阳</span></div>
      </aside>

      <section className="game" aria-label="养宠小窝">
        <div className="pet-topbar"><div><div className="pet-name">{pet.name}<button aria-label="修改名字" title="修改名字" onClick={() => { setDraftName(pet.name); setPanel('rename'); }}><Pencil size={13} /></button><span>{state.selected === 'cat' ? '小女生' : '小男生'}</span></div><div className="pet-age"><span>{stage.name}</span><i />{stage.age}<i /><Heart size={11} fill="currentColor" /> 亲密 {pet.mood}</div></div><div className="currency"><Coins size={19} /><strong>{state.coins}</strong><button title="打开背包" aria-label="打开背包" onClick={() => setPanel('bag')}><Plus size={14} /></button></div></div>
        <div className={`scene ${scene} view-${view} ${burst && !session ? 'celebrating' : ''}`} style={view === '2d' ? { backgroundImage: `url('/${scene}.svg')` } : undefined}>
          {view === '3d' && <SceneBoundary onFailure={() => changeView('2d')}><Suspense fallback={<div className="scene-loading"><PawPrint size={24} /><span>小可爱正在跑来…</span></div>}><PetWorld kind={state.selected} name={pet.name} stage={stage.index} dirty={dirty} cleanedSpots={cleanedSpots} messy={messy} accessory={pet.accessory} happy={pet.mood >= 85 || Boolean(burst)} session={session} burst={burst} meal={meal} onHit={careHit} turn={cameraTurn} reset={cameraReset} onFailure={() => changeView('2d')} /></Suspense></SceneBoundary>}
          <div className="scene-top"><span className="location"><House size={12} />{location}</span><div className="view-switch" role="group" aria-label="画面模式"><button aria-pressed={view === '3d'} onClick={() => changeView('3d')}>3D</button><button aria-pressed={view === '2d'} onClick={() => changeView('2d')}>2D</button></div><span className="day-badge"><Sun size={13} />相伴第 <b>{state.day}</b> 天</span></div>
          {view === '3d' && <div className="camera-tools"><button aria-label="转动视角" title="转动视角" onClick={() => setCameraTurn(previous => previous >= 2 ? -2 : previous + 1)}><RotateCw size={17} /></button><button aria-label="恢复正面" title="恢复正面" onClick={() => { setCameraTurn(0); setCameraReset(previous => previous + 1); }}><RotateCcw size={15} /></button></div>}
          <div className="scene-pet-switch" aria-label="切换宠物">{['cat', 'dog'].map(id => <button key={id} title={state.pets[id].adopted ? state.pets[id].name : `收养${state.pets[id].name}`} aria-label={state.pets[id].adopted ? `切换到${state.pets[id].name}` : `收养${state.pets[id].name}`} onClick={() => selectPet(id)} className={state.selected === id ? 'active' : ''}>{id === 'cat' ? <Cat size={19} /> : <Dog size={19} />}{!state.pets[id].adopted && <small>+</small>}</button>)}</div>
          <div className="speech-bubble" key={speech}>{speech}<Heart size={12} /></div>
          <button className={view === '3d' ? 'pet-keyboard-target' : `pet-target stage-${stage.index} ${session ? 'caring' : ''}`} aria-label={session ? currentStep.label : `抚摸${pet.name}`} onClick={() => { if (Date.now() - stroke.current > 100) careHit(); }} onPointerMove={event => { if (view === '2d' && session && event.buttons === 1 && Date.now() - stroke.current > 170) careHit(); }}>
            {view === '2d' && <>
            <Pet kind={state.selected} dirty={dirty > .1 ? dirty : 0} cleanedSpots={cleanedSpots} messy={messy} accessory={pet.accessory} happy={pet.mood >= 85 || Boolean(burst)} />
            {session?.type === 'bath' && session.step < 2 && <div className={`foam foam-${session.step}`} style={{ '--foam': Math.max(.1, session.step === 0 ? session.hits / 5 : 1 - session.hits / 5) }}><span /><span /><span /><span /><span /><span /><span /></div>}
            {session?.type === 'doctor' && <span className="medical-tool">{React.createElement(currentStep.icon, { size: 33, strokeWidth: 1.6 })}</span>}
            {session?.type === 'groom' && <span className="groom-tool" key={`${session.hits}-${session.step}`}>{React.createElement(currentStep.icon, { size: 36, strokeWidth: 1.6 })}</span>}
            </>}
          </button>
          {view === '2d' && burst === 'feed' && <div className="meal-reaction"><FoodArt type={meal} /><span>啊呜，好香！</span></div>}
          {['bath', 'groom'].includes(burst) && <div className="coat-sparkles" aria-hidden="true"><Sparkles size={25} /><Sparkles size={18} /><Sparkles size={21} /></div>}
          {view === '2d' && session?.type === 'play' && <button className={`yarn yarn-${session.hits}`} onClick={careHit} aria-label="追毛线球"><svg viewBox="0 0 60 60" aria-hidden="true"><path d="M39 40q20 4 13 14" fill="none" stroke="#a58daf" strokeWidth="2" /><circle cx="28" cy="28" r="20" fill="#d2bbd6" stroke="#a58daf" strokeWidth="2" /><path d="M12 17q26 0 33 17M9 25q24 0 30 18M17 12q22 7 18 34M27 9q14 18-4 38M10 32q17-19 35-8" fill="none" stroke="#a58daf" strokeWidth="1.4" /></svg></button>}
          {burst && <div className="pet-effects" key={String(burst)} aria-hidden="true">{[0, 1, 2, 3, 4].map(index => <span key={index} style={{ '--i': index }}>{session?.type === 'bath' ? <Droplets size={19} /> : <Heart size={index % 2 ? 15 : 22} fill="currentColor" />}</span>)}</div>}
          {!session && <span className="scene-caption"><span />{pet.clean >= 85 && pet.groomed ? '今天也是漂亮的小可爱' : '小小的你，大大的牵挂'}</span>}
          {session && <button className="leave-care" onClick={() => { setSession(null); notify('先休息一下，护理进度未保存'); }}><ArrowLeft size={13} />回小窝</button>}
        </div>

        <div className="stats">{[
          { label: '饱食', value: pet.hunger, icon: Utensils, color: 'honey' },
          { label: '洁净', value: pet.clean, icon: Droplets, color: 'aqua' },
          { label: '健康', value: pet.health, icon: Heart, color: 'rose' },
          { label: '心情', value: pet.mood, icon: Sun, color: 'lilac' },
        ].map(stat => <div className={`stat ${stat.color}`} key={stat.label}><div><stat.icon size={13} /><span>{stat.label}</span><b>{stat.value}</b></div><div className="stat-track" role="progressbar" aria-label={stat.label} aria-valuenow={stat.value} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${stat.value}%` }} /></div></div>)}</div>

        <div className="care-panel">
          {session ? <>
            <div className="care-session-heading"><div><Sparkles size={15} /><strong>{currentStep.title}</strong></div><span>{session.hits} / {currentStep.count}</span></div>
            <div className="care-tools"><div className="step-list">{STEPS[session.type].map((step, index) => <div className={session.step === index ? 'active' : session.step > index ? 'done' : ''} key={step.label}><span>{session.step > index ? <Check size={17} /> : React.createElement(step.icon, { size: 20 })}</span><small>{step.label}</small></div>)}</div><button className="primary-button care-hit" onClick={careHit}>{React.createElement(currentStep.icon, { size: 18 })}{currentStep.label}<ChevronRight size={16} /></button></div>
            <div className="care-progress"><span style={{ width: `${doneProgress * 100}%` }} /></div>
          </> : <>
            <button className="care-prompt" onClick={() => setPanel('daily')}><span><Sparkles size={16} /><strong>{taskCount === 4 ? '被爱包围的一天，真好呀' : pet.clean < 60 ? '小泥团，准备变漂亮！' : '给小可爱多一点点宠爱'}</strong></span><small>今日照护 {taskCount}/4<ChevronRight size={13} /></small></button>
            <div className="action-grid">{ACTIONS.map(action => <button className={`care-action ${action.color}`} key={action.id} onClick={() => startAction(action.id)} aria-label={action.label}><span className="action-icon"><action.icon size={23} strokeWidth={1.6} />{pet.tasks.includes(action.id) && <i><Check size={9} strokeWidth={3} /></i>}</span><strong>{action.label}</strong><small>{action.sub}</small></button>)}</div>
          </>}
        </div>
        <div className="game-footer"><div><span className="save-dot" />{saveError ? '暂时无法保存进度' : '小日子已悄悄记下'}</div><button onClick={sleep}><Moon size={14} />晚安，明天见<ArrowRight size={13} /></button></div>
        <nav className="bottom-nav" aria-label="主导航"><button className={!['bag', 'growth'].includes(panel) ? 'active' : ''} onClick={() => { setPanel(null); setSession(null); }}><House size={21} strokeWidth={1.7} /><span>小窝</span></button><button className={panel === 'bag' ? 'active' : ''} onClick={() => setPanel('bag')}><Package size={21} strokeWidth={1.7} /><span>小背包</span></button><button className={panel === 'growth' ? 'active' : ''} onClick={() => setPanel('growth')}><BookOpen size={21} strokeWidth={1.7} /><span>成长手记</span></button></nav>
      </section>

      <aside className="right-diary">
        <div className="section-title"><BookOpen size={17} /><h2>慢慢长大的你</h2><Flower2 size={15} /></div>
        <button className="polaroid" onClick={() => setPanel('growth')} aria-label="查看成长相册"><span className="tape" /><div className="photo-art"><div className="photo-sun" /><Pet kind={state.selected} accessory={pet.accessory} happy /><Flower2 className="photo-flower" size={28} strokeWidth={1.4} /><span className="photo-day">Day {state.day.toString().padStart(2, '0')}</span></div><span className="photo-title">{pet.name}的第 {state.day} 个小日子<Heart size={13} /></span></button>
        <div className="growth-summary"><div><strong>{stage.name}</strong><span>{stage.index === 2 ? '每一天，依然在成长' : '下一站，' + (stage.index === 0 ? '成长期' : '成年期')}</span></div><div className="growth-bar"><span style={{ width: `${growthPercent}%` }} /></div><div className="growth-exp"><PawPrint size={11} />{pet.xp} 成长值<span>{stage.index === 2 ? '已长大' : `还差 ${stage.next - pet.xp}`}</span></div><GrowthTrack pet={pet} /></div>
        <section className="journal-preview"><div className="section-title"><h2>我们的小事</h2><span>把爱写下来</span></div>{state.journal.filter(entry => entry.pet === state.selected).slice(0, 3).map((entry, index) => <div className="journal-entry" key={`${entry.type}-${entry.day}-${index}`}><span className="journal-dot" /><small>第 {entry.day} 天</small><p>{entry.text}</p></div>)}<button className="text-button" onClick={() => setPanel('growth')}>翻翻成长手记<ArrowRight size={14} /></button></section>
      </aside>
    </main>
    <footer className="site-footer"><PawPrint size={12} />一点陪伴，一点长大。</footer>
    {toast && <div role="status" className="toast" key={toast.id}><Heart size={16} />{toast.text}</div>}

    {(panel === 'feed' || panel === 'bag') && <Modal title={panel === 'feed' ? `${pet.name}，开饭啦` : '小背包'} onClose={() => setPanel(null)}>
      {panel === 'feed' ? <div className="feeding-state"><Utensils size={16} />肚子里的幸福<span>{pet.hunger} / 100</span></div> : <div className="panel-tabs" role="tablist" aria-label="背包分类"><button role="tab" aria-selected={bagTab === 'food'} className={bagTab === 'food' ? 'active' : ''} onClick={() => setBagTab('food')}><Utensils size={16} />食物</button><button role="tab" aria-selected={bagTab === 'closet'} className={bagTab === 'closet' ? 'active' : ''} onClick={() => setBagTab('closet')}><Sparkles size={16} />小衣橱</button></div>}
      {(panel === 'feed' || bagTab === 'food') ? <div className="food-list">{FOOD.map(food => <div className="food-row" key={food.id}><div className="food-art"><FoodArt type={food.id} /></div><div className="food-copy"><strong>{food.name}<small>×{state.inventory[food.id]}</small></strong><span>{food.note}</span><small className="food-bonus">饱食 +{food.amount}</small></div>{panel === 'feed' ? <button className="small-primary" disabled={!state.inventory[food.id] || pet.hunger === 100} onClick={() => completeCare('feed', food.id)}>喂食<Heart size={13} /></button> : <button className="buy-button" disabled={state.coins < food.cost} onClick={() => buyFood(food)}><Plus size={13} /><Coins size={13} />{food.cost}</button>}</div>)}</div> : <div className="closet-list">{[{ id: 'none', name: '自然的可爱', note: '原本的样子就很好' }, { id: 'bow', name: '莓果蝴蝶结', note: '一点甜甜的小心思' }, { id: 'scarf', name: '丁香小方巾', note: '软乎乎的温柔' }].map(item => <button className={`closet-item ${pet.accessory === item.id ? 'equipped' : ''}`} key={item.id} onClick={() => { setState(previous => ({ ...previous, pets: { ...previous.pets, [previous.selected]: { ...previous.pets[previous.selected], accessory: item.id } } })); chime(); }}><Pet kind={state.selected} accessory={item.id} /><strong>{item.name}</strong><small>{item.note}</small><span>{pet.accessory === item.id ? <><Check size={13} />已穿戴</> : '穿戴'}</span></button>)}</div>}
      <div className="modal-footnote">{panel === 'feed' ? <><Heart size={13} />好好吃饭，才有力气慢慢长大。</> : <><Coins size={14} />爪爪币 {state.coins}<span>每日照护和晚安可获得</span></>}</div>
      {panel === 'feed' && FOOD.every(food => state.inventory[food.id] === 0) && <button className="primary-button full" onClick={() => { setBagTab('food'); setPanel('bag'); }}>去补充食物<ArrowRight size={16} /></button>}
    </Modal>}

    {(panel === 'adopt' || panel === 'rename') && <Modal title={panel === 'adopt' ? '带一个小可爱回家' : '藏着爱的小名字'} onClose={() => setPanel(null)}>
      {panel === 'adopt' && <><div className="adopt-portrait"><Pet kind="dog" dirty={.6} messy /></div><div className="adopt-description"><strong>一只等了很久的小可卡</strong><p>2 个月 · 温顺亲人 · 喜欢追毛线球</p></div></>}
      <form onSubmit={event => { event.preventDefault(); saveName(); }}><label className="name-label" htmlFor="pet-name">{panel === 'adopt' ? '给它取个名字吧' : '它叫什么名字呢'}</label><input id="pet-name" value={draftName} onChange={event => setDraftName(event.target.value)} maxLength={8} autoComplete="off" required /><button className="primary-button full" disabled={!draftName.trim()} type="submit"><Heart size={16} />{panel === 'adopt' ? `带${draftName.trim() || '它'}回家` : '就是这个名字'}</button></form>
    </Modal>}

    {(panel === 'growth' || panel === 'milestone') && <Modal title={panel === 'milestone' ? '你看，它长大啦' : `${pet.name}的成长手记`} className="growth-modal" onClose={() => setPanel(null)}>
      {panel === 'milestone' && <div className="milestone-note"><Sparkles size={20} /><p>{pet.name}进入了<strong>{stage.name}</strong>，谢谢你一直以来的照顾。</p></div>}
      <div className="album-stages">{['小小的一团', '长大一点点', '最好的陪伴'].map((label, index) => <div className={index <= stage.index ? 'unlocked' : 'locked'} key={label}><div><Pet kind={state.selected} happy accessory={pet.accessory} className={`album-pet-${index}`} />{index > stage.index && <span><PawPrint size={20} /></span>}</div><strong>{label}</strong><small>{index <= stage.index ? ['2 个月 · 初次相遇', '6 个月 · 一起成长', '1 岁 · 往后的每一天'][index] : index === 1 ? '75 成长值解锁' : '180 成长值解锁'}</small></div>)}</div>
      <div className="album-growth"><span><PawPrint size={15} />{pet.xp} 成长值</span><span>{stage.name} · {stage.age}</span></div>
      <div className="full-journal">{state.journal.filter(entry => entry.pet === state.selected).map((entry, index) => <div key={`${entry.day}-${entry.type}-${index}`}><span className="journal-symbol">{entry.type === 'adopt' ? <House size={15} /> : entry.type === 'grow' ? <Sun size={15} /> : <Heart size={14} />}</span><div><small>相伴第 {entry.day} 天</small><p>{entry.text}</p></div></div>)}</div>
    </Modal>}

    {panel === 'daily' && <Modal title="今日小心愿" onClose={() => setPanel(null)}><p className="modal-description">属于{pet.name}的，平凡又幸福的一天。</p><TaskList pet={pet} onClaim={claimReward} /></Modal>}
    {panel === 'night' && <Modal title="晚安，我的小可爱" onClose={() => setPanel(null)}><div className="night-art"><Moon size={42} strokeWidth={1.2} /><Pet kind={state.selected} accessory={pet.accessory} happy /></div><p className="night-copy">今天也被好好照顾了。<br />睡个好觉，明天又会长大一点。</p><div className="night-reward"><Coins size={17} />新一天的照护基金<strong>+60</strong></div><button className="primary-button full" onClick={advanceDay}><Moon size={16} />睡醒，迎接第 {state.day + 1} 天</button></Modal>}
    {panel === 'settings' && <Modal title="小窝设置" onClose={() => setPanel(null)}><div className="settings-row"><span><Volume2 size={18} />轻轻的音效</span><button className={`toggle ${state.sound ? 'on' : ''}`} role="switch" aria-label="音效" aria-checked={state.sound} onClick={() => setState(previous => ({ ...previous, sound: !previous.sound }))}><span /></button></div><div className="settings-row"><span><BookOpen size={18} />小日子存档</span><small>{saveError ? '浏览器存储不可用' : '已保存在当前浏览器'}</small></div><button className="reset-button" onClick={() => setPanel('reset')}><RotateCcw size={16} />重新开始一个小日子</button></Modal>}
    {panel === 'reset' && <Modal title="重新开始吗？" onClose={() => setPanel(null)}><p className="modal-description">当前的成长、背包和照护记录会被清空，小可爱们会回到初次相遇的那一天。</p><div className="confirm-actions"><button className="secondary-button" onClick={() => setPanel('settings')}>继续这个小日子</button><button className="primary-button" onClick={() => { setState(freshGame()); setSession(null); setPanel(null); notify('从相遇的第一天，重新开始'); }}>确认重新开始</button></div></Modal>}
  </>;
}
