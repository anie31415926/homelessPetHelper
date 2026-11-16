import test from 'node:test';
import assert from 'node:assert/strict';
import { bridgeAt, canClean, center, cleanAt, slideBox, undoPuzzle } from '../src/playlab/engine.js';
import { PROFILES, actionOptions, approachReady, beginNegotiation, chooseAction, createOuting, cueFor, updateOuting } from '../src/outing/engine.js';

const advance = (game, seconds = 1.3) => { for (let i = 0; i < Math.ceil(seconds / .02); i++) updateOuting(game, .02); };
const act = (game, id) => { assert.ok(chooseAction(game, id), `action ${id} accepted`); advance(game); };
function openApproach(game) {
  const approach = game.approach;
  if (game.route === 'puzzle') {
    slideBox(approach, 'a', 3, 1);
    slideBox(approach, 'a', game.template === 'left' ? 4 : 2, 1);
    bridgeAt(approach, game.template === 'left' ? 5 : 1, 4);
  } else {
    for (let pass = 0; pass < 12; pass++) for (const item of approach.items) {
      if (canClean(approach, item)) cleanAt(approach, center(item.col, item.row), 1, item.type);
    }
  }
}

test('seeded variations are replayable, bounded and both approaches remain solvable', () => {
  const routes = new Set(); const profiles = new Set(); const templates = new Set();
  for (let seed = 0; seed < 160; seed++) {
    const game = createOuting({ seed });
    routes.add(game.route); profiles.add(game.profile); templates.add(game.template);
    assert.deepEqual(createOuting({ seed, route: game.route, profile: game.profile }), game, 'explicit replay preserves random draws');
    assert.equal(beginNegotiation(game), false, 'cannot bypass blocked approach');
    openApproach(game);
    assert.ok(approachReady(game), `seed ${seed}, ${game.route}, ${game.template} has a solution`);
    advance(game, .1);
    assert.equal(game.stage, 'approach', 'opening route does not auto-rescue animal');
    assert.ok(beginNegotiation(game));
    assert.equal(game.stage, 'negotiate');
    assert.equal(game.turn.food, 3, 'approach never consumes negotiation food');
  }
  assert.equal(routes.size, 2); assert.equal(profiles.size, 3); assert.equal(templates.size, 2);
});

test('puzzle readiness updates after undo and cleanup only needs an open route', () => {
  const game = createOuting({ seed: 0, route: 'puzzle' });
  openApproach(game); advance(game, .1);
  assert.equal(game.ready, true);
  undoPuzzle(game.approach); advance(game, .1);
  assert.equal(game.ready, false);
  assert.equal(beginNegotiation(game), false);
  const cleanup = createOuting({ seed: 3, route: 'cleanup' });
  for (let pass = 0; pass < 12; pass++) for (const item of cleanup.approach.items) {
    if (item.type !== 'mud' && canClean(cleanup.approach, item)) cleanAt(cleanup.approach, center(item.col, item.row), 1, item.type);
  }
  assert.ok(approachReady(cleanup));
  assert.ok(cleanup.approach.items.some(item => item.type === 'mud' && item.hp > 0));
});

test('each profile has a distinct short action sequence leading to voluntary rescue', () => {
  const sequences = {
    hungry: ['food', 'wait', 'approach', 'approach', 'invite'],
    shy: ['wait', 'toy', 'wait', 'approach', 'wait', 'approach', 'invite'],
    curious: ['toy', 'approach', 'wait', 'approach', 'invite'],
  };
  for (const [profile, sequence] of Object.entries(sequences)) {
    const game = createOuting({ seed: 15, profile, direct: true });
    assert.match(cueFor(game.turn), profile === 'hungry' ? /食物袋/ : profile === 'shy' ? /缩着/ : /玩具/);
    for (const id of sequence) act(game, id);
    assert.equal(game.stage, 'success', profile);
    assert.equal(game.turn.rescued, true);
    assert.equal(game.turn.log.length, sequence.length);
    assert.ok(game.turn.log.every(item => item.response && Number.isFinite(item.delta)));
    assert.equal(game.turn.alert, 0);
  }
});

test('animal reacts to actions in context, and resolution locks double taps', () => {
  const game = createOuting({ profile: 'hungry', direct: true });
  assert.equal(chooseAction(game, 'invite'), false);
  assert.equal(game.turn.lastAction, null);
  assert.equal(chooseAction(game, 'garbage'), false);
  assert.ok(chooseAction(game, 'food'));
  assert.equal(game.turn.food, 2);
  assert.equal(chooseAction(game, 'food'), false);
  assert.equal(game.turn.food, 2);
  assert.equal(game.turn.phase, 'react');
  assert.equal(game.turn.mood, 'eating');
  assert.match(cueFor(game.turn), /低头吃/);
  advance(game);
  const before = game.turn.alert;
  act(game, 'approach');
  assert.ok(game.turn.alert > before);
  assert.match(game.turn.reaction, /进食时靠近/);
  assert.equal(game.turn.distance, 2);
  act(game, 'wait');
  assert.match(game.turn.reaction, /不再缩/);
});

test('spamming food or waiting cannot substitute for changing distance and earning trust', () => {
  const game = createOuting({ profile: 'hungry', direct: true });
  for (let i = 0; i < 3; i++) act(game, 'food');
  assert.equal(chooseAction(game, 'food'), false);
  assert.equal(actionOptions(game.turn)[0].disabled, true);
  for (let i = 0; i < 20; i++) act(game, 'wait');
  assert.equal(game.stage, 'negotiate');
  assert.equal(game.turn.distance, 2);
  assert.ok(game.turn.alert >= 32);
});

test('even empty-food and maximum-alert states recover without random failure or a reset', () => {
  for (const profile of Object.keys(PROFILES)) {
    const game = createOuting({ profile, direct: true });
    Object.assign(game.turn, { food: 0, alert: 100, mood: 'wary' });
    for (let i = 0; i < 30 && game.stage === 'negotiate'; i++) {
      const t = game.turn;
      const threshold = t.distance === 2 ? 48 : 28;
      const id = t.distance === 0 && t.alert <= 20 ? 'invite' : t.mood === 'wary' || t.alert > 68 ? 'wait' : t.alert <= threshold && t.mood !== 'eating' ? 'approach' : t.mood === 'playing' ? 'wait' : 'toy';
      act(game, id);
    }
    assert.equal(game.stage, 'success', profile);
  }
});

test('invalid modes fall back safely, old stages reject actions, and clocks stop after rescue', () => {
  const game = createOuting({ seed: 2, route: 'invalid', profile: 'invalid' });
  assert.ok(['puzzle', 'cleanup'].includes(game.route));
  assert.ok(Object.hasOwn(PROFILES, game.profile));
  assert.equal(chooseAction(game, 'wait'), false);
  beginNegotiation(game, true);
  assert.equal(actionOptions(game.turn).length, 4);
  const success = createOuting({ profile: 'curious', direct: true });
  for (const id of ['toy', 'approach', 'wait', 'approach', 'invite']) act(success, id);
  const elapsed = success.elapsed;
  advance(success, 1);
  assert.equal(success.elapsed, elapsed);
  assert.equal(chooseAction(success, 'wait'), false);
});
