import test from 'node:test';
import assert from 'node:assert/strict';
import { HOME, bridgeAt, canClean, canRescue, cellAt, center, cleanAt, createExperiment, findRoute, guideDestination, guideFood, matrixFor, moveGuide, puzzleFood, rescueCleanPet, slideBox, tick, undoPuzzle } from '../src/playlab/engine.js';

const advance = (game, seconds) => { for (let i = 0; i < Math.ceil(seconds / .02); i++) tick(game, .02); };
const openPuzzle = game => {
  assert.ok(slideBox(game, 'a', 3, 3));
  assert.ok(slideBox(game, 'a', 4, 3));
};

test('puzzle requires an actually traversable route, not a fixed click sequence', () => {
  const game = createExperiment('puzzle');
  assert.ok(puzzleFood(game, HOME.col, HOME.row));
  assert.equal(game.animal.path.length, 0);
  assert.ok(bridgeAt(game, 1, 4));
  assert.equal(game.animal.path.length, 0, 'crate still blocks corridor');
  openPuzzle(game);
  assert.ok(game.animal.path.length > 6);
  advance(game, 12);
  assert.equal(game.done, true);
  assert.equal(game.actions, 4);
});

test('bridge choice changes the spatial solution; right side needs a second crate moved', () => {
  const game = createExperiment('puzzle');
  slideBox(game, 'a', 3, 1);
  slideBox(game, 'a', 2, 1);
  bridgeAt(game, 5, 4);
  puzzleFood(game, HOME.col, HOME.row);
  assert.equal(game.animal.path.length, 0);
  slideBox(game, 'b', 5, 6);
  assert.ok(game.animal.path.length > 0);
  advance(game, 12);
  assert.equal(game.done, true);
});

test('puzzle collision, undo and invalid placements cannot destroy the level', () => {
  const game = createExperiment('puzzle');
  assert.equal(slideBox(game, 'a', 2, 2), false, 'solid wall');
  assert.equal(slideBox(game, 'a', 3, 4), false, 'water');
  assert.equal(slideBox(game, 'a', 4, 3), false, 'diagonal');
  assert.equal(bridgeAt(game, 3, 4), false);
  assert.equal(puzzleFood(game, 0, 0), false);
  assert.equal(puzzleFood(game, -1, 0), false);
  assert.ok(slideBox(game, 'a', 3, 3));
  assert.ok(undoPuzzle(game));
  assert.deepEqual(game.boxes[0], { id: 'a', col: 3, row: 2 });
  assert.equal(game.actions, 0);
  assert.equal(undoPuzzle(game), false);
});

test('cleanup needs the right tool and an accessible frontier, with partial progress', () => {
  const game = createExperiment('cleanup');
  const far = game.items.find(item => item.row === 1 && item.type !== 'mud');
  assert.equal(canClean(game, far), false);
  assert.equal(cleanAt(game, center(far.col, far.row), 1, far.type), false);
  const near = game.items.find(item => canClean(game, item) && item.type === 'weeds');
  const point = center(near.col, near.row);
  assert.equal(cleanAt(game, point, .1, 'mud'), false);
  assert.ok(cleanAt(game, point, .1, 'weeds'));
  assert.ok(near.hp > 0 && near.hp < near.maxHp);
  cleanAt(game, point, 1, 'weeds');
  assert.equal(near.hp, 0);
  assert.equal(game.cleaned, 1);
  cleanAt(game, point, 1, 'weeds');
  assert.equal(game.cleaned, 1, 'no duplicate progress');
});

test('cleanup reveals two pets, routes them home and permits optional dirt to remain', () => {
  const game = createExperiment('cleanup');
  assert.equal(canRescue(game, game.pets[0]), false);
  for (let pass = 0; pass < 20; pass++) for (const item of game.items) {
    if (item.type !== 'mud' && canClean(game, item)) cleanAt(game, center(item.col, item.row), 1, item.type);
  }
  assert.ok(game.items.some(item => item.type === 'mud' && item.hp > 0));
  for (const pet of game.pets) {
    assert.ok(canRescue(game, pet));
    assert.ok(rescueCleanPet(game, pet.id));
    assert.equal(rescueCleanPet(game, pet.id), false);
  }
  assert.equal(game.done, false);
  advance(game, 10);
  assert.ok(game.pets.every(pet => pet.safe));
  assert.equal(game.done, true);
  const elapsed = game.elapsed;
  advance(game, 1);
  assert.equal(game.elapsed, elapsed);
});

test('guide navigation uses obstacle-aware paths and no-food rescue is possible', () => {
  const game = createExperiment('guide');
  assert.equal(guideDestination(game, 3, 3), false, 'bench cannot be selected');
  game.toy = true;
  assert.ok(guideDestination(game, 5, 2));
  for (let i = 0; i < 400; i++) {
    tick(game, .02);
    for (const value of [game.player, game.animal]) {
      const cell = cellAt(value);
      assert.equal(matrixFor(game)[cell.row][cell.col], 0, 'actors never pass through obstacles');
    }
  }
  assert.ok(game.trust >= .55);
  assert.ok(guideDestination(game, HOME.col, HOME.row));
  advance(game, 15);
  assert.equal(game.done, true);
  assert.equal(game.food, 3, 'food is helpful but optional');
});

test('guide attention responds to toy, rushing, food and distractions', () => {
  const game = createExperiment('guide');
  Object.assign(game.player, center(4, 2));
  game.slow = false;
  const before = game.trust;
  moveGuide(game, 0, -1, .02);
  tick(game, .02);
  assert.equal(game.attention, '有点紧张');
  assert.ok(game.trust < before);
  game.slow = true;
  Object.assign(game.player, center(5, 7));
  Object.assign(game.animal, center(5, 4));
  tick(game, .02);
  assert.equal(game.attention, '被球吸引');
  Object.assign(game.player, center(5, 4));
  game.animal.path = [];
  assert.ok(guideFood(game));
  assert.equal(guideFood(game), false);
  advance(game, 2.5);
  assert.equal(game.food, 2);
  assert.ok(game.trust > .55);
});

test('invalid routes and mode-specific actions are rejected', () => {
  const game = createExperiment('guide');
  assert.equal(guideDestination(game, NaN, 2), false);
  assert.equal(guideDestination(game, -1, 2), false);
  assert.equal(guideFood(createExperiment('puzzle')), false);
  assert.deepEqual(findRoute(matrixFor(game), { col: 3, row: 6 }, { col: -1, row: 0 }), []);
});
