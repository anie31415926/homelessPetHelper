import React, { useEffect, useRef, useState } from 'react';
import {
    Fish, Leaf, Volleyball, House, Volume2, Footprints, VolumeX, Move, Shuffle,
    MessageCircle, Camera, Pause, Play, RotateCcw, X, BookOpen, Pin, PinOff,
    Sparkles, ArrowRight, Check, Flag, Heart, History, ChevronRight, ChevronDown, ShieldCheck,
} from 'lucide-react';
import { animal, createDuel, playMove, previewGroup, togglePin, completeRescue, withdraw,
    CARDS, EVENTS, INTENTS, MODES, TOKENS, GOAL_NAMES } from './engine.js';
import { useHoldConfirm } from '../layout-b/useHoldConfirm.js';
import catImage from '../../unity/HomelessPetHelper/Assets/Art/Station/T_Station_CatTemporary.png';
import dogImage from '../../unity/HomelessPetHelper/Assets/Art/Station/T_Station_DogTemporary.png';
import backdrop from '../../art-source/rescue-layout-b/clean-backdrop.jpg';

const icons = { Fish, Leaf, Volleyball, House, Volume2, Footprints, VolumeX, Move, Shuffle, MessageCircle, Camera };
const defaults = () => {
    const query = new URLSearchParams(location.search);
    const seed = Number(query.get('seed') ?? 20261119);
    return { seed: Number.isInteger(seed) && seed >= 0 && seed <= 0xFFFFFFFF ? seed : 20261119,
        mode: MODES[query.get('mode')] ? query.get('mode') : 'chain' };
};
function Icon({ name, size = 20, ...props }) {
    const Component = icons[name] || Sparkles;
    return <Component size={size} aria-hidden="true" {...props} />;
}
function freshSeed(old) {
    const value = crypto.getRandomValues(new Uint32Array(1))[0];
    return value === old ? (value + 1) >>> 0 : value;
}
function GainLine({ gains, compact = false }) {
    const positive = Object.entries(gains).filter(([, gain]) => gain > 0);
    return positive.length ? positive.map(([type, gain]) => <span key={type} style={{ color: TOKENS[type].color }}>
        <Icon name={TOKENS[type].icon} size={14} /><span aria-label={`${GOAL_NAMES[type]}增加${gain}`}>{compact ? TOKENS[type].name : GOAL_NAMES[type]} +{gain}</span>
    </span>) : null;
}
function ActionCard({ item, onPlay, onPin, disabled, pinDisabled }) {
    const card = CARDS[item.id];
    const { holding, handlers } = useHoldConfirm(onPlay, disabled);
    return <div className={`duel-card ${item.locked ? 'is-pinned' : ''}`} data-card={item.id}>
        <button className={`card-action ${holding ? 'is-holding' : ''}`} disabled={disabled}
            title={card.detail} aria-label={`${card.title}。${card.detail} 按住确认。`}
            {...handlers}>
            <i className="hold-progress" /><Icon name={card.icon} size={18} />
            <span>{card.title}</span>{card.flavor && <small>插曲</small>}
        </button>
        <button className="pin-action icon-button" disabled={disabled || pinDisabled}
            onClick={onPin} aria-pressed={item.locked} title={item.locked ? '取消保留' : '保留到下一手'}
            aria-label={`${item.locked ? '取消保留' : '保留'}${card.title}`}>
            {item.locked ? <Pin size={16} fill="currentColor" /> : <PinOff size={16} />}
        </button>
    </div>;
}
function Modal({ title, onClose, children, className = '' }) {
    const ref = useRef(null);
    useEffect(() => {
        const previous = document.activeElement;
        const dialog = ref.current;
        dialog.showModal();
        const close = event => { event.preventDefault(); onClose(); };
        dialog.addEventListener('cancel', close);
        return () => {
            dialog.removeEventListener('cancel', close);
            dialog.close();
            if (previous?.isConnected) previous.focus({ preventScroll: true });
        };
    }, []);
    return <dialog ref={ref} className={`duel-modal ${className}`} aria-labelledby="dialog-title">
        <header><h2 id="dialog-title">{title}</h2><button className="icon-button" onClick={onClose}
            title="关闭" aria-label="关闭"><X size={20} /></button></header>
        <div className="modal-body">{children}</div>
    </dialog>;
}

export default function DuelApp() {
    const [state, setState] = useState(() => createDuel(defaults()));
    const [dialog, setDialog] = useState(null);
    const [busy, setBusy] = useState(false);
    const [selected, setSelected] = useState(-1);
    const [seedInput, setSeedInput] = useState('');
    const [seedError, setSeedError] = useState('');
    const [reduced, setReduced] = useState(() => matchMedia('(prefers-reduced-motion: reduce)').matches);
    const [showResult, setShowResult] = useState(false);
    const [waveIndex, setWaveIndex] = useState(0);
    const timer = useRef(null);
    const current = useRef(state);
    const lock = useRef(false);
    const paused = useRef(false);
    current.current = state;
    paused.current = dialog;
    const pet = animal(state);
    const finished = ['rescued', 'withdrawn'].includes(state.status);
    const disabled = busy || Boolean(dialog) || state.status !== 'playing';
    const displayedWave = busy && !reduced ? state.last.waves[waveIndex] : null;
    const displayedBoard = displayedWave?.board || state.board;
    const displayedState = displayedWave ? { ...state, board: displayedBoard } : state;
    const preview = selected >= 0 ? previewGroup(state, selected) : null;
    const total = Object.values(state.goals).reduce((a, b) => a + b, 0) + state.homeTarget;
    const progress = Math.min(1, Object.values(state.progress).reduce((a, b) => a + b, 0) / total);

    useEffect(() => {
        const media = matchMedia('(prefers-reduced-motion: reduce)');
        const change = () => setReduced(media.matches);
        media.addEventListener('change', change);
        const background = () => {
            if (document.hidden) {
                clearTimeout(timer.current); lock.current = false; setBusy(false);
                if (!['rescued', 'withdrawn'].includes(current.current.status)) {
                    setSeedInput(String(current.current.seed)); setSeedError(''); setDialog('pause');
                }
            }
        };
        document.addEventListener('visibilitychange', background);
        return () => { clearTimeout(timer.current); media.removeEventListener('change', change);
            document.removeEventListener('visibilitychange', background); };
    }, []);
    useEffect(() => {
        const url = new URL(location.href);
        url.searchParams.set('seed', state.seed);
        url.searchParams.set('mode', state.mode);
        history.replaceState(null, '', url);
    }, [state.seed, state.mode]);

    function reset(seed = state.seed, mode = state.mode) {
        clearTimeout(timer.current); lock.current = false; setBusy(false);
        setState(createDuel({ seed, mode })); setSelected(-1); setDialog(null); setShowResult(false);
    }
    function act(command) {
        if (lock.current || (paused.current && paused.current !== 'hand') || document.hidden) return;
        const before = current.current;
        const next = playMove(before, { ...command, round: before.round });
        if (next === before) return;
        current.current = next; lock.current = true; setDialog(null); setState(next); setSelected(-1); setWaveIndex(0); setBusy(true);
        let frame = 0;
        const advance = () => {
            frame++;
            if (!reduced && frame < next.last.waves.length) {
                setWaveIndex(frame);
                timer.current = setTimeout(advance, 340);
            } else {
                setWaveIndex(frame);
                timer.current = setTimeout(() => { lock.current = false; setBusy(false); }, reduced ? 0 : 180);
            }
        };
        timer.current = setTimeout(advance, reduced ? 60 : 340);
    }
    function boardKey(event, index) {
        const offset = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -5, ArrowDown: 5 }[event.key];
        if (!offset) return;
        event.preventDefault();
        const next = index + offset;
        if (next >= 0 && next < 20 && (Math.abs(offset) !== 1 || Math.floor(index / 5) === Math.floor(next / 5))) {
            document.querySelector(`[data-cell="${next}"]`)?.focus();
        }
    }
    function submitSeed(event) {
        event.preventDefault();
        const trimmed = seedInput.trim();
        const value = Number(trimmed);
        if (!/^\d+$/.test(trimmed) || !Number.isInteger(value) || value > 0xFFFFFFFF) {
            setSeedError('请输入 0 到 4294967295 的整数。'); return;
        }
        reset(value);
    }

    return <main className="duel-shell" data-mode={state.mode} data-round={state.round}
        data-status={state.status} data-seed={state.seed} aria-label="脑内过招救助实验">
        <div className="duel-layout">
        <header className="duel-topbar">
            <button className="mode-picker" aria-label={`切换玩法，当前${MODES[state.mode].name}`} onClick={() => setDialog('modes')}>
                <Heart size={18} /><span><strong>脑内过招</strong><small>{MODES[state.mode].name}<ChevronDown size={13} /></small></span>
            </button>
            <div className="top-tools">
                <button className="icon-button" aria-label="查看行动记录" title="行动记录" onClick={() => setDialog('history')}><History size={18} /></button>
                <button className="icon-button" aria-label="查看规则" title="规则" onClick={() => setDialog('rules')}><BookOpen size={18} /></button>
                <button className="icon-button" aria-label="暂停" title="暂停" onClick={() => { setSeedInput(String(state.seed)); setSeedError(''); setDialog('pause'); }}><Pause size={18} /></button>
            </div>
        </header>

        <div className="duel-observation">
        <section className={`duel-scene ${state.status === 'ready' ? 'is-ready' : ''} ${finished ? 'is-finished' : ''}`}
            style={{ backgroundImage: `url(${backdrop})` }} aria-label="动物的现场回应">
            <div className="pet-caption"><h1>{pet.name}</h1><p>{pet.quirk}</p></div>
            <div className="pet-thought" key={`${state.seed}-${state.round}`}>
                {state.status === 'ready' ? '这里，我自己进。' : state.status === 'rescued' ? '好了，可以歇会儿了。' :
                    state.status === 'withdrawn' ? '先留一点自己的空间。' : INTENTS[state.intent].thought}
            </div>
            <div className={`pet-actor ${busy ? 'is-reacting' : ''}`} style={{ '--approach': `${progress * 19}px` }}>
                <img className="duel-pet" src={pet.kind === 'cat' ? catImage : dogImage} alt={pet.kind === 'cat' ? '正在观察你的猫咪' : '正在观察你的狗狗'} draggable="false" />
            </div>
            <div className="home-place" aria-label={state.homeReady ? '已经愿意检查安置处' : '安置处'}>
                <House size={32} /><span>{state.homeReady ? '愿意靠近了' : '安置处'}</span>
            </div>
            <div className="scene-foot"><span><i />{finished ? '本次相遇已结束' : `第 ${state.round} 手`}</span>
                <button className="trait-label" onClick={() => setDialog('needs')} aria-label="查看个体特性与需要">{state.followUp ? '余味还在 · 下次空间 +2' : pet.trait}<ChevronRight size={12} /></button></div>
        </section>

        <section className="duel-needs" aria-label="本局需要">
            {[...Object.entries(state.goals), ['home', state.homeTarget]].map(([type, target]) =>
                <button key={type} className={`need ${type === 'home' && !state.homeReady ? 'is-waiting' : ''}`}
                    style={{ '--token': TOKENS[type].color }} onClick={() => setDialog('needs')}
                    aria-label={`${GOAL_NAMES[type]} ${state.progress[type]}/${target}，查看需要详情`}>
                    <div><Icon name={TOKENS[type].icon} size={14} /><span>{GOAL_NAMES[type]}</span>
                        <strong data-goal={type} data-value={state.progress[type]} data-target={target}>{state.progress[type]}/{target}</strong></div>
                    <progress aria-label={GOAL_NAMES[type]} max={target} value={state.progress[type]} />
                    <small>{type === 'home' && !state.homeReady ? (state.prepared ? '已熟悉 · 稍后 +2' : '先顾眼下需要') :
                        state.progress[type] >= target ? '已满足' : type === state.intent ? '当前关注' : '仍可推进'}</small>
                </button>)}
        </section>
        </div>

        <section className="duel-puzzle" aria-label="脑内机会棋盘">
            <div className="board-heading">
                <div className="intent-line"><Icon name={TOKENS[state.intent].icon} size={17} /><strong>{INTENTS[state.intent].title}</strong></div>
                <button className={`focus-action ${state.focus === 3 ? 'is-full' : ''}`} disabled={disabled || state.focus < 3}
                    onClick={() => act({ kind: 'insight' })} title="默契满 3 点：接出并消除当前关注的一组机会"
                    aria-label={`默契 ${state.focus}/3${state.focus === 3 ? '，使用默契回应' : ''}`}>
                    <Sparkles size={16} /><span>{state.focus === 3 ? '默契回应' : '默契'}</span>
                    <span className="focus-dots">{[0, 1, 2].map(i => <i key={i} className={i < state.focus ? 'filled' : ''} />)}</span>
                </button>
            </div>
            {state.mode === 'change' && <button className="event-forecast" aria-label="查看现场预告" onClick={() => setDialog('forecast')}>
                {state.shield && <ShieldCheck size={15} aria-label="已准备应对干扰" />}
                <span>{state.round % 2 === 0 ? '这手之后' : '两手之后'}</span>
                <strong title={EVENTS[state.forecast[0]].detail}>{EVENTS[state.forecast[0]].name}</strong>
                <ChevronRight size={13} /><span title={EVENTS[state.forecast[1]].detail}>{EVENTS[state.forecast[1]].name}</span>
            </button>}
            <div className="duel-board" role="group" aria-label="机会棋盘，两个起连，方向键移动焦点" aria-busy={busy}>
                {displayedBoard.map((item, index) => {
                    const own = previewGroup(displayedState, index);
                    const relevant = item.type === 'home' ? state.homeReady : Boolean(state.goals[item.type] > state.progress[item.type]);
                    return <button key={index} data-cell={index} data-type={item.type} data-size={own.cells.length}
                        data-relevant={relevant} className={`duel-tile tile-${item.type} ${preview?.cells.includes(index) ? 'is-linked' : ''} ${busy && state.last.changed.includes(index) ? 'is-changed' : ''}`}
                        style={{ '--token': TOKENS[item.type].color }} disabled={disabled}
                        aria-label={`${index + 1}格 ${TOKENS[item.type].name}，${own.cells.length >= 2 ? `${own.cells.length}个相连${own.prepared ? '，提前熟悉' : `，推进${own.actualGain}`}` : '暂不能消除'}`}
                        aria-disabled={!own.valid || disabled}
                        title={own.valid ? `${TOKENS[item.type].name} ${own.cells.length} 连。${own.text}` : '连成两个再试试'}
                        onMouseEnter={() => setSelected(index)} onMouseLeave={() => setSelected(-1)}
                        onFocus={() => setSelected(index)} onBlur={() => setSelected(-1)}
                        onKeyDown={event => boardKey(event, index)} onClick={() => act({ kind: 'group', index })}>
                        <Icon name={TOKENS[item.type].icon} size={23} /><span>{TOKENS[item.type].name}</span>
                    </button>;
                })}
                {displayedWave && <div className="wave-layer" aria-hidden="true" key={`${state.seed}-${state.round}-${waveIndex}`}>
                    {displayedWave.cells.map(index => <span key={index}
                        className={`wave-cell tile-${displayedWave.board[index].type}`} style={{ '--token': TOKENS[displayedWave.board[index].type].color,
                            '--x': index % 5, '--y': Math.floor(index / 5), '--delay': '0ms' }}>
                        <Icon name={TOKENS[displayedWave.board[index].type].icon} size={23} />
                    </span>)}
                    {displayedWave.depth > 1 && <strong className="chain-burst">{displayedWave.depth} 段连锁</strong>}
                </div>}
            </div>
            <div className="duel-feedback" role="status" aria-live="polite" aria-atomic="true">
                <button className="feedback-detail" onClick={() => setDialog('feedback')} aria-label={`查看本手回应：${state.last.title}`}>
                    <strong>{state.last.title}</strong><ChevronRight size={15} />
                </button>
                <span className="gain-line"><GainLine gains={state.last.gains} compact /></span>
                <p className="response-text">{state.last.eventText || state.last.text}{state.last.opened ? ' 它愿意靠近安置处了。' : ''}</p>
            </div>
        </section>

        <section className="duel-actions" aria-label="现场行动">
            {state.status === 'playing' ? <>
                <button className="compact-hand-trigger secondary-action" disabled={busy} onClick={() => setDialog('hand')}>
                    <Icon name="Move" size={18} />现场行动<span>{state.hand.filter(item => item.locked).length}/2 已保留</span><ChevronRight size={17} />
                </button>
                <div className="action-heading"><span>现场行动</span><small>按住行动 · 图钉保留</small>
                    <span>{state.hand.filter(item => item.locked).length}/2</span></div>
                <div className="duel-hand">{state.hand.map(item => <ActionCard key={item.instanceId} item={item} disabled={disabled}
                    pinDisabled={!item.locked && state.hand.filter(card => card.locked).length >= 2}
                    onPlay={() => act({ kind: 'card', instanceId: item.instanceId })}
                    onPin={() => setState(before => togglePin(before, item.instanceId))} />)}</div>
            </> : state.status === 'ready' ? <div className="ready-action">
                <p><Check size={18} />它已经准备好了。不用再等一张牌。</p>
                <button className="primary-action" disabled={busy || Boolean(dialog)} onClick={() => { setState(completeRescue(state, state.round)); setShowResult(true); }}>
                    <House size={19} />让它自己走进来<ArrowRight size={18} /></button>
            </div> : <div className="finished-actions"><strong>{state.status === 'rescued' ? '它在里面安静坐下了。' : '没有勉强它，这次先到这里。'}</strong>
                <div><button className="secondary-action" onClick={() => setShowResult(true)}><Flag size={17} />本局回顾</button>
                    <button className="primary-action" onClick={() => reset(freshSeed(state.seed))}>再遇一只<ArrowRight size={17} /></button></div></div>}
        </section>
        </div>

        {dialog === 'modes' && <Modal title="换个玩法试试" onClose={() => setDialog(null)}>
            <nav className="mode-options" aria-label="实验玩法">
                {Object.entries(MODES).map(([id, mode]) => <button key={id} aria-pressed={state.mode === id}
                    onClick={() => id === state.mode ? setDialog(null) : setDialog(`mode:${id}`)}>
                    <span><strong>{mode.name}</strong><small>{mode.note}</small></span>{state.mode === id ? <Check size={19} /> : <ChevronRight size={19} />}
                </button>)}
            </nav>
        </Modal>}
        {dialog === 'needs' && <Modal title={pet.name} onClose={() => setDialog(null)}>
            <p>{pet.quirk}。{state.followUp ? '余味还在，下次空间额外 +2。' : pet.trait + '。'}</p>
            <dl className="rules-list">{[...Object.entries(state.goals), ['home', state.homeTarget]].map(([type, target]) =>
                <React.Fragment key={type}><dt>{GOAL_NAMES[type]} · {state.progress[type]}/{target}</dt>
                    <dd>{type === 'home' && !state.homeReady ? (state.prepared ? `已熟悉入口，稍后接受 +${state.profile === 'explorer' ? 3 : 2}。` : '先顾好眼下需要，再争取接受安置；可以提前熟悉入口。') :
                        state.progress[type] >= target ? '这一项已经满足。' : type === state.intent ? '它当前正在关注这一项。' : '还可以继续推进这一项。'}</dd></React.Fragment>)}</dl>
        </Modal>}
        {dialog === 'feedback' && <Modal title={state.last.title} onClose={() => setDialog(null)}>
            <div className="gain-line"><GainLine gains={state.last.gains} /></div>
            <p>{state.last.text}</p>{state.last.eventText && <p>{state.last.eventText}</p>}
            {state.last.opened && <p>它愿意靠近安置处了。</p>}
        </Modal>}
        {dialog === 'rules' && <Modal title="这一局的规则" onClose={() => setDialog(null)}>
            <p className="modal-lead">{MODES[state.mode].note}</p>
            <dl className="rules-list">
                <dt>机会棋盘</dt><dd>点选至少两个上下左右相连的同类机会，消除后上方落下、新机会补入。干扰不能单点，会被相邻消除带走，每个额外推进 1 点安心。</dd>
                <dt>当前倾向</dt><dd>图标与它的想法一致时，额外推进 {state.mode === 'read' ? '2' : '1'} 点并获得 1 点默契；每手只结算一次默契，个体特性可改为 2 点。默契满 3 点可主动接出并消除一组。</dd>
                <dt>本局需要</dt><dd>先满足两项个体需要，再争取接受安置。提前消除安置会让它熟悉入口，之后一次性获得 {state.profile === 'explorer' ? 3 : 2} 点接受；反复熟悉不叠加。</dd>
                <dt>留一手</dt><dd>行动牌只改造机会，不直接推进需要。每次行动后换牌，最多保留两张；用掉的保留牌也会消耗。趣味插曲只给回应，各出现一次后退出。</dd>
                <dt>它也有节奏</dt><dd>连续三次没顺着当前倾向消除，它会转而需要空间，局面增加两个空间机会；没有伤害或进度倒扣。</dd>
                <dt>这只的脾气</dt><dd>{pet.trait}。此外偏好“{TOKENS[pet.affinity].name}”；该类一次消除至少四格时，额外推进 1 点。已满足或本局不需要的类别不再加进度，但可以整理棋盘。</dd>
                <dt>不确定性</dt><dd>来自初始局面、补入机会、行动顺序与预告事件，不做隐藏成功判定。相同编号和相同操作可以重现结果；换玩法会重开相同编号。</dd>
            </dl>
            <button className="primary-action full-width" onClick={() => setDialog(null)}><Play size={17} />继续这一局</button>
        </Modal>}
        {dialog === 'forecast' && <Modal title="接下来的现场" onClose={() => setDialog(null)}>
            <p className="modal-lead">每两手发生一次变化；进展不会倒扣。{state.shield ? '已准备应对下一次推车。' : ''}</p>
            <dl className="rules-list">{state.forecast.slice(0, 2).map((event, index) => <React.Fragment key={index}>
                <dt>{index === 0 ? (state.round % 2 ? '两手之后' : '这手之后') : '再过两手'} · {EVENTS[event].name}</dt>
                <dd>{EVENTS[event].detail}</dd></React.Fragment>)}</dl>
        </Modal>}
        {dialog === 'hand' && <Modal title="现场行动" onClose={() => setDialog(null)}>
            <div className="modal-hand duel-hand">{state.hand.map(item => <ActionCard key={item.instanceId} item={item}
                disabled={busy || state.status !== 'playing'} pinDisabled={!item.locked && state.hand.filter(card => card.locked).length >= 2}
                onPlay={() => act({ kind: 'card', instanceId: item.instanceId })}
                onPin={() => setState(before => togglePin(before, item.instanceId))} />)}</div>
            <dl className="rules-list">{state.hand.map(item => <React.Fragment key={item.id}><dt>{CARDS[item.id].title}</dt><dd>{CARDS[item.id].detail}</dd></React.Fragment>)}</dl>
        </Modal>}
        {dialog === 'history' && <Modal title="这一局的来回" onClose={() => setDialog(null)}>
            <p className="history-summary">{state.actions} 次行动 · {state.cleared} 格机会 · 最高 {state.bestChain} 段连锁</p>
            {!state.log.length ? <p>它还在观察你。</p> : <ol className="duel-log">{[...state.log].reverse().map(entry => <li key={entry.round}>
                <span>第 {entry.round} 手</span><strong>{entry.title}</strong><div className="gain-line"><GainLine gains={entry.gains} /></div>
                {entry.chain > 1 && <small>{entry.chain} 段连锁</small>}{entry.event && <small> · {EVENTS[entry.event].name}</small>}
            </li>)}</ol>}
        </Modal>}
        {dialog === 'pause' && <Modal title="先歇一下" onClose={() => setDialog(null)}>
            <p className="modal-lead">它停在原处，局面不会自己往下走。</p>
            <p className="result-seed">HTML 玩法实验 · {MODES[state.mode].name} · {state.actions} 次行动</p>
            <button className="primary-action full-width" onClick={() => setDialog(null)}><Play size={18} />继续</button>
            <div className="pause-actions"><button onClick={() => reset()}><RotateCcw size={17} />重玩本局</button>
                <button onClick={() => reset(freshSeed(state.seed))}><Shuffle size={17} />再遇一只</button></div>
            <form className="seed-form" onSubmit={submitSeed}><label htmlFor="duel-seed">相遇编号</label>
                <div><input id="duel-seed" inputMode="numeric" autoComplete="off" value={seedInput}
                    onChange={event => { setSeedInput(event.target.value); setSeedError(''); }} aria-describedby={seedError ? 'seed-error' : undefined} />
                    <button aria-label="重开指定编号" title="重开指定编号"><ArrowRight size={19} /></button></div>
                {seedError && <p id="seed-error" role="alert">{seedError}</p>}
            </form>
            {state.status === 'playing' && <button className="withdraw-action" onClick={() => setDialog('withdraw')}>这次先不勉强它</button>}
        </Modal>}
        {dialog?.startsWith('mode:') && <Modal title={`试试${MODES[dialog.slice(5)].name}`} onClose={() => setDialog(null)}>
            <p>{MODES[dialog.slice(5)].note}</p><p>用相同编号重新开始，当前这局不会保留。</p>
            <button className="primary-action full-width" onClick={() => reset(state.seed, dialog.slice(5))}><RotateCcw size={18} />换个玩法重开</button>
        </Modal>}
        {dialog === 'withdraw' && <Modal title="这次先到这里？" onClose={() => setDialog(null)}>
            <p>本局会结束，不会记成救助成功。</p><button className="primary-action full-width" onClick={() => { setState(withdraw(state)); setDialog(null); setShowResult(true); }}>结束这次相遇</button>
        </Modal>}
        {showResult && finished && <Modal title={state.status === 'rescued' ? '这次，跟你走了' : '留到下一次相遇'} className="result-modal" onClose={() => setShowResult(false)}>
            <div className="result-animal"><img src={pet.kind === 'cat' ? catImage : dogImage} alt={pet.name} /><Heart size={28} /></div>
            <p className="result-story">{state.status === 'rescued' ? '它自己走进安置处，转了个身，终于放心坐下。刚才那一小步，是它自己决定的。' : '它还需要自己的空间。没有把未完成的相遇写成成功。'}</p>
            <div className="result-metrics"><div><strong>{state.actions}</strong><span>次行动</span></div><div><strong>{state.cleared}</strong><span>格机会</span></div><div><strong>{state.bestChain}</strong><span>最高连锁</span></div></div>
            <p className="result-seed">{pet.name} · {MODES[state.mode].name} · #{state.seed}</p>
            <button className="primary-action full-width" onClick={() => reset(freshSeed(state.seed))}>再遇一只<ArrowRight size={18} /></button>
            <button className="replay-action" onClick={() => reset()}><RotateCcw size={16} />同一局，试另一种走法</button>
        </Modal>}
    </main>;
}
