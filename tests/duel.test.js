import test from 'node:test';
import assert from 'node:assert/strict';
import { createDuel, playMove, previewGroup, togglePin, completeRescue, withdraw,
    groups, groupAt, MAX_WAVES, CARDS } from '../src/duel/engine.js';
import { connect, ensureMove, refill } from '../src/duel/board.js';

const patterned = () => Array.from({ length: 20 }, (_, i) => ({ id: i + 1,
    type: ['food', 'space', 'play', 'home'][(i + Math.floor(i / 5)) % 4] }));
const setup = options => { const s = createDuel(options); s.board = patterned(); return s; };
function setCells(state, indices, type) { indices.forEach(i => { state.board[i] = { id: ++state.serial, type }; }); }
const move = (s, kind, extra = {}) => playMove(s, { kind, round: s.round, ...extra });

test('orthogonal groups never wrap a row or include diagonal and noise', () => {
    const s = setup();
    s.board.forEach((item, i) => { item.type = i % 2 ? 'space' : 'home'; });
    setCells(s, [4, 5, 9], 'food');
    assert.deepEqual(groupAt(s.board, 4), [4, 9]);
    assert.deepEqual(groupAt(s.board, 5), [5]);
    setCells(s, [4], 'noise');
    assert.deepEqual(groupAt(s.board, 4), []);
    for (const index of [-1, 20, NaN, 2.1, undefined]) assert.deepEqual(groupAt(s.board, index), []);
});

test('seed and mode reproduce setup, previews and invalid/stale commands consume nothing', () => {
    const s = createDuel({ seed: 0 });
    assert.deepEqual(s, createDuel({ seed: 0 }));
    assert.deepEqual(s.board, createDuel({ seed: 0, mode: 'read' }).board);
    const snapshot = structuredClone(s);
    for (let i = 0; i < 20; i++) previewGroup(s, i);
    assert.deepEqual(s, snapshot);
    for (const command of [null, {}, { kind: 'group', index: 0, round: 0 },
        { kind: 'card', instanceId: -1, round: s.round }, { kind: 'insight', round: s.round },
        { kind: 'group', index: -1, round: s.round }, { kind: 'unknown', round: s.round }]) {
        assert.equal(playMove(s, command), s);
    }
    const command = { kind: 'group', index: groups(s.board)[0].cells[0], round: s.round };
    const n = playMove(s, command);
    assert.deepEqual(n, playMove(snapshot, command));
    assert.equal(playMove(n, command), n);
    assert.deepEqual(s, snapshot);
    for (const seed of [-1, 0x100000000, NaN, 1.2]) assert.throws(() => createDuel({ seed }), RangeError);
});

test('gravity retains surviving IDs and only refills removed cells', () => {
    const s = setup();
    const original = structuredClone(s.board);
    refill(s, [10, 15]);
    assert.equal(s.board[15].id, original[5].id);
    assert.equal(s.board[10].id, original[0].id);
    assert.ok(s.board[0].id > original[19].id);
    for (let i = 0; i < 20; i++) if (i % 5 !== 0) assert.deepEqual(s.board[i], original[i]);
    assert.equal(new Set(s.board.map(item => item.id)).size, 20);
});

test('connect grows an actual connected group even if the category did not exist', () => {
    const s = setup();
    s.board.forEach(item => { item.type = 'food'; });
    assert.equal(connect(s, 'home', 4).length, 4);
    assert.equal(groups(s.board).find(group => group.type === 'home').cells.length, 4);
    assert.equal(connect(s, 'home', 2).length, 2);
    assert.equal(groups(s.board).find(group => group.type === 'home').cells.length, 6);
});

test('a genuine no-move board receives a legal pair without score or unbounded rerolls', () => {
    const s = setup();
    assert.equal(groups(s.board).length, 0);
    const before = structuredClone(s.progress);
    assert.equal(ensureMove(s), true);
    assert.ok(groups(s.board).length);
    assert.deepEqual(s.progress, before);
    assert.equal(ensureMove(s), false);
});

test('pinning is pure, costs no turn, holds at most two, and used pinned cards are consumed', () => {
    let s = createDuel();
    const original = structuredClone(s);
    s = togglePin(s, s.hand[0].instanceId);
    assert.deepEqual(original, createDuel());
    assert.equal(s.round, original.round);
    assert.equal(s.rng, original.rng);
    s = togglePin(s, s.hand[1].instanceId);
    assert.equal(togglePin(s, s.hand[2].instanceId), s);
    const used = s.hand[0].instanceId;
    const retained = s.hand[1].instanceId;
    s = move(s, 'card', { instanceId: used });
    assert.ok(!s.hand.some(item => item.instanceId === used));
    assert.ok(s.hand.some(item => item.instanceId === retained));
    assert.equal(s.hand.length, 3);
});

test('flavor returns text once, never adds goal progress and never crowds out two real cards', () => {
    let s = createDuel({ seed: 12 });
    s.hand[0] = { id: 'photo', instanceId: ++s.serial, locked: true };
    const before = structuredClone(s.progress);
    s = move(s, 'card', { instanceId: s.hand[0].instanceId });
    assert.deepEqual(s.progress, before);
    assert.deepEqual(s.usedFlavor, ['photo']);
    assert.ok(s.last.text.includes('照片'));
    for (let i = 0; i < 80; i++) {
        assert.equal(new Set(s.hand.map(item => item.id)).size, 3);
        assert.ok(s.hand.filter(item => CARDS[item.id].flavor).length <= 1);
        assert.ok(!s.hand.some(item => item.id === 'photo'));
        s = move(s, 'card', { instanceId: s.hand.find(item => !CARDS[item.id].flavor).instanceId });
    }
});

test('intent bonus is visible; focus charges once per move and insight needs three', () => {
    const s = setup({ mode: 'read', profile: 'snacker' });
    setCells(s, [0, 1, 2, 3], 'food');
    s.intent = 'food';
    const p = previewGroup(s, 0);
    assert.equal(p.gain, p.cells.length + 3);
    let n = move(s, 'group', { index: 0 });
    assert.equal(n.last.gains.food, Math.min(p.gain, s.goals.food));
    assert.equal(n.focus, 1);
    assert.equal(move(n, 'insight'), n);
    n.focus = 3;
    n = move(n, 'insight');
    assert.ok(n.last.waves.length >= 1);
    assert.equal(n.focus, 1);
});

test('early home is preparation, not acceptance; goal completion and finish are explicit', () => {
    let s = setup({ mode: 'read', profile: 'snacker' });
    setCells(s, [0, 1], 'home');
    s = move(s, 'group', { index: 0 });
    assert.equal(s.progress.home, 0);
    assert.equal(s.prepared, true);
    assert.equal(s.homeReady, false);
    assert.equal(completeRescue(s, s.round), s);
    s.progress.food = s.goals.food;
    s.progress.space = s.goals.space;
    s.hand[0] = { id: 'food', instanceId: ++s.serial, locked: false };
    s = move(s, 'card', { instanceId: s.hand[0].instanceId });
    assert.equal(s.homeReady, true);
    assert.equal(s.progress.home, 2);
    setCells(s, Array.from({ length: 10 }, (_, i) => i), 'home');
    s = move(s, 'group', { index: 0 });
    assert.equal(s.status, 'ready');
    assert.equal(s.hand.length, 0);
    assert.equal(completeRescue(s, s.round - 1), s);
    const rescued = completeRescue(s, s.round);
    assert.equal(rescued.status, 'rescued');
    assert.equal(completeRescue(rescued, rescued.round), rescued);
    assert.equal(move(rescued, 'group', { index: 0 }), rescued);
});

test('noise is cleared by physical adjacency, not by unrelated or diagonal clears', () => {
    const s = setup({ mode: 'read', profile: 'watcher' });
    setCells(s, [0, 1], 'food');
    setCells(s, [2, 6, 19], 'noise');
    const n = move(s, 'group', { index: 0 });
    assert.equal(n.last.noise, 2);
    assert.ok(n.last.waves[0].cells.includes(2));
    assert.ok(n.last.waves[0].cells.includes(6));
    assert.ok(!n.last.waves[0].cells.includes(19));
    assert.equal(n.last.gains.space, 4);
});

test('chain mode resolves real connected groups with at least five, capped at four waves', () => {
    const s = setup({ mode: 'chain' });
    setCells(s, [0, 1], 'food');
    setCells(s, [15, 16, 17, 18, 19], 'play');
    const n = move(s, 'group', { index: 0 });
    assert.ok(n.last.waves.length >= 2);
    assert.ok(n.last.waves.length <= MAX_WAVES);
    for (const wave of n.last.waves.slice(1)) {
        assert.ok(groups(wave.board, 5).length);
        const actual = groups(wave.board, 5).flatMap(g => g.cells);
        assert.ok(actual.every(i => wave.cells.includes(i)));
    }
    const read = move({ ...s, mode: 'read' }, 'group', { index: 0 });
    assert.equal(read.last.waves.length, 1);
});

test('forecast fires exactly every two actions and quiet can block the next noise event', () => {
    let s = createDuel({ seed: 9, mode: 'change' });
    s.forecast = ['noise', 'breeze', 'opening'];
    s.hand[0] = { id: 'quiet', instanceId: ++s.serial, locked: false };
    s = move(s, 'card', { instanceId: s.hand[0].instanceId });
    assert.equal(s.shield, true);
    assert.equal(s.last.event, undefined);
    s = move(s, 'card', { instanceId: s.hand[0].instanceId });
    assert.equal(s.last.event, 'noise');
    assert.equal(s.shield, false);
    assert.ok(s.last.eventText.includes('挡住'));
    assert.equal(s.forecast[0], 'breeze');
    assert.equal(s.forecast.length, 3);
});

test('withdrawal is not a rescue and subsequent commands do nothing', () => {
    const s = withdraw(createDuel());
    assert.equal(s.status, 'withdrawn');
    assert.equal(completeRescue(s, s.round), s);
    assert.equal(move(s, 'insight'), s);
});

test('individual traits change sequence choices, not only names or target numbers', () => {
    let s = setup({ mode: 'read', profile: 'snacker' });
    s.intent = 'food'; setCells(s, [0, 1, 2, 3], 'food');
    s = move(s, 'group', { index: 0 });
    assert.equal(s.followUp, true);
    setCells(s, [0, 1], 'space');
    const bonus = previewGroup(s, 0).gain;
    assert.equal(bonus, previewGroup({ ...s, followUp: false }, 0).gain + 2);
    s = move(s, 'group', { index: 0 });
    assert.equal(s.followUp, false);

    s = setup({ mode: 'read', profile: 'curious' });
    setCells(s, [0, 1], 'space'); s.lastType = 'play';
    assert.equal(previewGroup(s, 0).gain, previewGroup({ ...s, lastType: 'space' }, 0).gain + 2);

    s = setup({ mode: 'read', profile: 'traveler' });
    setCells(s, [0, 1], 'food'); s.intent = 'food';
    assert.equal(previewGroup(s, 0).gain, 6);

    s = setup({ mode: 'read', profile: 'player' });
    s.intent = 'play'; setCells(s, [0, 1, 2, 3], 'play');
    assert.equal(move(s, 'group', { index: 0 }).focus, 2);

    s = createDuel({ profile: 'explorer', mode: 'read' });
    s.prepared = true;
    for (const type of Object.keys(s.goals)) s.progress[type] = s.goals[type];
    assert.equal(move(s, 'card', { instanceId: s.hand[0].instanceId }).progress.home, 3);
});

export function choose(s, policy = 'adaptive') {
    const available = groups(s.board).map(g => ({ ...g, preview: previewGroup(s, g.cells[0]) }));
    if (s.focus >= 3 && policy === 'adaptive') return { kind: 'insight' };
    const score = group => {
        if (policy === 'largest') return group.cells.length;
        const type = group.type;
        const remaining = type === 'home' ? (s.homeReady ? s.homeTarget - s.progress.home : 0) :
            Math.max(0, (s.goals[type] || 0) - s.progress[type]);
        return Math.min(group.preview.gain, remaining) * 3 + (group.preview.matched ? 1 : 0) + group.cells.length * .03;
    };
    available.sort((a, b) => score(b) - score(a));
    const best = available[0];
    if (policy === 'adaptive' && score(best) < 7) {
        const useful = s.hand.find(item => {
            const type = CARDS[item.id].type || (item.id === 'gather' ? s.intent : undefined);
            return type && (type === 'home' ? s.homeReady : s.goals[type] > s.progress[type]);
        });
        if (useful) return { kind: 'card', instanceId: useful.instanceId };
    }
    return { kind: 'group', index: best.cells[0] };
}

test('seed sweep: three modes and two policies remain solvable, distinct and bounded', () => {
    const results = [];
    for (const mode of ['read', 'chain', 'change']) {
        for (const policy of ['adaptive', 'largest']) {
            const runs = [];
            for (let seed = 1; seed <= 36; seed++) {
                let s = createDuel({ seed, mode });
                const opening = JSON.stringify(choose(s, policy));
                while (s.status === 'playing' && s.actions < 160) {
                    const before = s;
                    s = move(s, choose(s, policy).kind, choose(s, policy));
                    assert.notEqual(s, before);
                    assert.equal(s.board.length, 20);
                    assert.equal(new Set(s.board.map(item => item.id)).size, 20);
                    assert.ok(s.board.filter(item => item.type === 'noise').length <= 5);
                    assert.ok(s.focus <= 3 && s.focus >= 0);
                    assert.ok(s.last.waves.length <= MAX_WAVES);
                    for (const [type, amount] of Object.entries(s.progress)) {
                        assert.ok(amount >= before.progress[type]);
                        assert.ok(amount <= (type === 'home' ? s.homeTarget : s.goals[type] || 0));
                    }
                }
                assert.equal(s.status, 'ready', `${mode}/${policy}/${seed} exceeded 160 actions`);
                runs.push({ actions: s.actions, best: s.bestChain, opening });
            }
            assert.ok(new Set(runs.map(item => item.actions)).size >= 6);
            assert.ok(new Set(runs.map(item => item.opening)).size >= 5);
            results.push({ mode, policy, encounters: runs.length,
                min: Math.min(...runs.map(item => item.actions)), max: Math.max(...runs.map(item => item.actions)),
                average: +(runs.reduce((n, item) => n + item.actions, 0) / runs.length).toFixed(1),
                cascades: runs.filter(item => item.best > 1).length });
        }
    }
    console.log('DUEL_POLICY_SAMPLE', JSON.stringify(results));
});
