import { CARDS, COLS, ROWS, EVENTS, MODES, PROFILES, RESPONSES } from './content.js';
import { connect, ensureMove, groupAt, groups, refill, roll, shuffle, tile } from './board.js';

export { groupAt, groups } from './board.js';
export { CARDS, EVENTS, INTENTS, MODES, PROFILES, TOKENS, GOAL_NAMES } from './content.js';

const copy = value => structuredClone(value);
const baseCards = Object.keys(CARDS);
export const MAX_WAVES = 4;

export function createDuel({ seed = 20261119, mode = 'chain', profile } = {}) {
    if (!Number.isInteger(seed) || seed < 0 || seed > 0xFFFFFFFF || !MODES[mode]) {
        throw new RangeError('Invalid encounter seed or mode.');
    }
    const state = { seed, rng: seed, mode, serial: 0, round: 1, status: 'playing',
        board: [], hand: [], deck: [], goals: {}, progress: { food: 0, space: 0, play: 0, home: 0 },
        homeReady: false, prepared: false, focus: 0, strain: 0, shield: false, usedFlavor: [],
        intent: 'space', forecast: [], log: [], actions: 0, cleared: 0, bestChain: 0, lastType: null, followUp: false,
        last: { title: '先看看它在打什么主意', text: '', waves: [], changed: [], gains: {} } };
    const selected = profile ? PROFILES.find(item => item.id === profile) :
        PROFILES[Math.floor(roll(state) * PROFILES.length)];
    if (!selected) throw new RangeError('Unknown animal profile.');
    state.profile = selected.id;
    state.goals = Object.fromEntries(Object.entries(selected.goals).map(([type, amount]) =>
        [type, amount + Math.floor(roll(state) * 3)]));
    state.homeTarget = selected.home;
    state.board = Array.from({ length: COLS * ROWS }, () => tile(state));
    // These barriers are puzzle symbols, not a literal second road-cleaning stage.
    shuffle(state, Array.from({ length: COLS * ROWS }, (_, i) => i)).slice(0, 2)
        .forEach(i => { state.board[i] = tile(state, 'noise'); });
    state.forecast = Array.from({ length: 3 }, () => randomEvent(state));
    state.intent = selectIntent(state);
    state.last.text = selected.start;
    ensureMove(state);
    supply(state);
    return state;
}

export function animal(state) {
    return PROFILES.find(item => item.id === state.profile);
}

export function goalsDone(state) {
    return Object.entries(state.goals).every(([type, target]) => state.progress[type] >= target);
}

function randomEvent(state) {
    return Object.keys(EVENTS)[Math.floor(roll(state) * Object.keys(EVENTS).length)];
}

function selectIntent(state) {
    const remaining = Object.keys(state.goals).filter(type => state.progress[type] < state.goals[type]);
    const pool = goalsDone(state) ? ['home', 'home', 'space'] : [...remaining, animal(state).affinity, 'space'];
    return pool[Math.floor(roll(state) * pool.length)];
}

export function previewGroup(state, index) {
    const cells = groupAt(state.board, index);
    if (cells.length < 2) return { valid: false, cells, text: '需要至少两个相邻的同类机会。' };
    const type = state.board[index].type;
    const matched = type === state.intent;
    const gain = groupGain(state, type, cells.length);
    const prepared = type === 'home' && !goalsDone(state);
    const target = type === 'home' ? state.homeTarget : state.goals[type] || 0;
    const actualGain = prepared ? 0 : Math.max(0, Math.min(gain, target - state.progress[type]));
    return { valid: true, cells, type, gain, actualGain, matched, prepared,
        text: prepared ? (state.prepared ? '已经熟悉了；这手只改变机会布局。' :
            `提前熟悉安置处，之后接受安置 +${state.profile === 'explorer' ? 3 : 2}。`) :
            matched ? '顺着它的倾向，能多推进一点。' : '可以推进这项，下一步再看它的反应。' };
}

function groupGain(state, type, size) {
    const matched = type === state.intent;
    const trait = state.profile === 'snacker' && type === 'space' && state.followUp ||
        state.profile === 'curious' && state.lastType && state.lastType !== type ||
        state.profile === 'traveler' && matched && size <= 3;
    return size + (matched ? (state.mode === 'read' ? 2 : 1) : 0) +
        (type === animal(state).affinity && size >= 4 ? 1 : 0) + (trait ? 2 : 0);
}

export function togglePin(state, instanceId) {
    if (state.status !== 'playing') return state;
    const card = state.hand.find(item => item.instanceId === instanceId);
    if (!card || (!card.locked && state.hand.filter(item => item.locked).length >= 2)) return state;
    const next = copy(state);
    const target = next.hand.find(item => item.instanceId === instanceId);
    target.locked = !target.locked;
    return next;
}

function supply(state) {
    const retained = state.hand.filter(item => item.locked);
    const eligible = id => !retained.some(item => item.id === id) &&
        !state.usedFlavor.includes(id) &&
        !(CARDS[id].flavor && retained.some(item => CARDS[item.id].flavor));
    const drawn = [];
    let safety = 0;
    while (retained.length + drawn.length < 3 && safety++ < 50) {
        if (!state.deck.length) state.deck = shuffle(state, baseCards.filter(id => !state.usedFlavor.includes(id)));
        const id = state.deck.shift();
        if (!eligible(id) || drawn.includes(id) || (CARDS[id].flavor && drawn.some(key => CARDS[key].flavor))) continue;
        drawn.push(id);
    }
    if (retained.length + drawn.length !== 3) throw new Error('Insufficient distinct cards.');
    state.hand = [...retained, ...drawn.map(id => ({ id, instanceId: ++state.serial, locked: false }))];
}

function addProgress(state, type, amount, gains) {
    const before = state.progress[type];
    const cap = type === 'home' ? state.homeTarget : state.goals[type] || 0;
    state.progress[type] = Math.min(cap, before + amount);
    gains[type] = (gains[type] || 0) + state.progress[type] - before;
}

function clearWave(state, clusters, result, depth) {
    const cells = [...new Set(clusters.flatMap(group => group.cells))];
    const wave = { board: copy(state.board), cells, depth, types: clusters.map(group => group.type) };
    for (const group of clusters) {
        const type = group.type;
        const match = type === state.intent;
        const amount = groupGain(state, type, group.cells.length);
        if (type === 'home' && !goalsDone(state)) {
            state.prepared = true;
            result.prepared = true;
        } else {
            addProgress(state, type, amount, result.gains);
        }
        if (match) state.strain = Math.max(0, state.strain - 1);
        if (match && !result.matched) {
            state.focus = Math.min(3, state.focus + (state.profile === 'player' && type === 'play' && group.cells.length >= 4 ? 2 : 1));
            result.matched = true;
        }
        if (type === 'space') state.followUp = false;
        if (state.profile === 'snacker' && type === 'food' && group.cells.length >= 4) state.followUp = true;
        state.lastType = type;
    }
    // Clear adjacent interference in the same physical connected move, not a cosmetic combo number.
    const noise = state.board.flatMap((item, index) => item.type === 'noise' && cells.some(i =>
        Math.abs(i % COLS - index % COLS) + Math.abs(Math.floor(i / COLS) - Math.floor(index / COLS)) === 1) ?
        [index] : []);
    wave.cells = [...new Set([...cells, ...noise])];
    if (noise.length) addProgress(state, 'space', noise.length * (state.profile === 'watcher' ? 2 : 1), result.gains);
    result.noise += noise.length;
    result.waves.push(wave);
    state.cleared += wave.cells.length;
    refill(state, wave.cells);
}

function resolveClear(state, clusters, result) {
    clearWave(state, clusters, result, 1);
    if (state.mode !== 'chain') return;
    for (let depth = 2; depth <= MAX_WAVES; depth++) {
        const next = groups(state.board, 5);
        if (!next.length) break;
        clearWave(state, next, result, depth);
    }
}

function applyCard(state, card, result) {
    const definition = CARDS[card.id];
    if (definition.flavor) {
        state.usedFlavor.push(card.id);
        result.text = card.id === 'reply' ? '消息回完了。它没有在等你的输入状态。' :
            '照片拍好了。它还是按自己的节奏来。';
        return;
    }
    if (card.id === 'shuffle') {
        state.board = shuffle(state, state.board);
        result.changed = state.board.map((_, i) => i);
    } else if (card.id === 'quiet') {
        result.changed = state.board.flatMap((item, i) => item.type === 'noise' ? [i] : []);
        if (!result.changed.length) result.changed = connect(state, 'space', 2);
        else result.changed.forEach(i => { state.board[i] = tile(state, 'space'); });
        state.shield = true;
    } else {
        result.changed = connect(state, definition.type || state.intent, definition.count);
    }
    result.text = card.id === 'quiet' ? '干扰变成了空间，下次小推车也有了应对。' :
        '机会接在一起了。下一手可以连成更大的一片。';
}

function applyEvent(state, result) {
    if (state.mode !== 'change' || state.round % 2 !== 0) return;
    const event = state.forecast.shift();
    state.forecast.push(randomEvent(state));
    result.event = event;
    if (event === 'noise' && state.shield) {
        state.shield = false;
        result.eventText = '小推车经过，提前留出的空间挡住了干扰。';
        return;
    }
    if (event === 'breeze') {
        const row = state.board.slice(5, 10);
        state.board.splice(5, 5, row[4], ...row.slice(0, 4));
    } else if (event === 'noise') {
        const choices = shuffle(state, state.board.flatMap((item, i) => item.type !== 'noise' ? [i] : []));
        const count = Math.min(2, Math.max(0, 5 - state.board.filter(item => item.type === 'noise').length));
        choices.slice(0, count).forEach(i => { state.board[i] = tile(state, 'noise'); });
    } else if (event === 'quiet') {
        const noise = state.board.flatMap((item, i) => item.type === 'noise' ? [i] : []).slice(0, 2);
        noise.forEach(i => { state.board[i] = tile(state, 'space'); });
        if (!noise.length) connect(state, 'space', 2);
    } else {
        connect(state, event === 'interest' ? 'play' : 'home', 3);
    }
    result.eventText = EVENTS[event].name + '，局面变了。';
}

function finishTurn(state, result) {
    if (goalsDone(state) && !state.homeReady) {
        state.homeReady = true;
        result.opened = true;
        if (state.prepared) addProgress(state, 'home', state.profile === 'explorer' ? 3 : 2, result.gains);
        result.changed.push(...connect(state, 'home', 2));
    }
    if (state.progress.home >= state.homeTarget) state.status = 'ready';
    if (!result.matched && result.waves.length) state.strain = Math.min(3, state.strain + 1);
    if (state.strain >= 3) {
        // A readable change of focus creates space; no damage, lost progress or forced failure.
        state.strain = 0;
        state.intent = 'space';
        result.breather = true;
        result.changed.push(...connect(state, 'space', 2));
    } else state.intent = selectIntent(state);
    applyEvent(state, result);
    result.repaired = ensureMove(state);
    if (result.breather) result.text += ' 它想先缓一缓，空间机会增加了。';
    if (result.repaired) result.text += ' 没有可连机会，已补上一组。';
    state.actions++;
    state.bestChain = Math.max(state.bestChain, result.waves.length);
    state.round++;
    if (state.status === 'playing') supply(state);
    else state.hand = [];
    state.last = result;
    state.log = [...state.log, { round: state.round - 1, title: result.title, gains: result.gains,
        chain: result.waves.length, event: result.event, text: result.text }].slice(-40);
}

// Commands require current round/instance identity. Invalid input returns the exact same object.
export function playMove(state, command) {
    if (state.status !== 'playing' || !command || command.round !== state.round) return state;
    const preview = command.kind === 'group' ? previewGroup(state, command.index) : null;
    const card = command.kind === 'card' ? state.hand.find(item => item.instanceId === command.instanceId) : null;
    if ((command.kind === 'group' && !preview?.valid) || (command.kind === 'card' && !card) ||
        !['group', 'card', 'insight'].includes(command.kind) || (command.kind === 'insight' && state.focus < 3)) {
        return state;
    }
    const next = copy(state);
    const result = { title: '', text: '', waves: [], gains: {}, changed: [], matched: false, noise: 0 };
    if (command.kind === 'group') {
        result.title = preview.cells.length >= 6 ? '好大一片，接住了' :
            preview.matched ? '这一手，正合它意' : '换条路，也有进展';
        result.text = preview.prepared ? (state.prepared ? '它已经检查过了。先看看眼下还缺什么。' :
            '它检查了入口，记住了位置。还要先顾好眼下的需要。') :
            RESPONSES[preview.type][state.round % 2];
        resolveClear(next, [{ type: preview.type, cells: preview.cells }], result);
    } else if (command.kind === 'card') {
        result.title = CARDS[card.id].title;
        applyCard(next, card, result);
        next.hand = next.hand.filter(item => item.instanceId !== card.instanceId);
    } else {
        next.focus = 0;
        const type = goalsDone(next) ? 'home' : next.intent;
        result.title = '突然对上频道了';
        result.changed = connect(next, type, 4);
        const cluster = groups(next.board).filter(group => group.type === type)
            .sort((a, b) => b.cells.length - a.cells.length)[0];
        if (cluster) resolveClear(next, [cluster], result);
        result.text = '它主动回应了你，几件事一下顺了起来。';
    }
    finishTurn(next, result);
    return next;
}

export function completeRescue(state, round) {
    if (state.status !== 'ready' || round !== state.round) return state;
    return { ...copy(state), status: 'rescued', last: { ...state.last,
        title: '这次，跟你走了', text: '它自己走进安置处，终于把身体放松下来。' } };
}

export function withdraw(state) {
    if (state.status !== 'playing') return state;
    return { ...copy(state), status: 'withdrawn' };
}
