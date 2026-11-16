import test from 'node:test';
import assert from 'node:assert/strict';
import { freshGame, care, nextDay, stageOf, nightBlocker, loadGame } from '../src/game.js';

test('a rescued pet starts small and needs care', () => {
  const state = freshGame();
  assert.equal(state.pets.cat.adopted, true);
  assert.equal(state.pets.dog.adopted, false);
  assert.equal(stageOf(state.pets.cat.xp).name, '幼年期');
  assert.notEqual(nightBlocker(state.pets.cat), '');
});
test('feeding consumes exactly one portion, clamps stats and grants daily XP once', () => {
  let state = freshGame();
  state = care(state, 'feed', 'chicken');
  assert.equal(state.pets.cat.hunger, 81);
  assert.equal(state.inventory.chicken, 2);
  assert.equal(state.pets.cat.xp, 28);
  state = care(state, 'feed', 'chicken');
  assert.equal(state.pets.cat.hunger, 100);
  assert.equal(state.pets.cat.xp, 28);
  assert.equal(care(state, 'feed', 'chicken'), state);
});
test('empty inventories cannot feed or change progression', () => {
  const state = freshGame(); state.inventory.kibble = 0;
  assert.equal(care(state, 'feed', 'kibble'), state);
  assert.equal(care(state, 'feed', 'bad-id'), state);
});
test('all care tasks lead to clean, neat and healthy pets', () => {
  let state = freshGame();
  for (const task of ['feed', 'bath', 'groom', 'doctor', 'play']) state = care(state, task, 'chicken');
  assert.equal(state.pets.cat.clean, 100);
  assert.equal(state.pets.cat.health, 100);
  assert.equal(state.pets.cat.groomed, true);
  assert.equal(state.pets.cat.mood, 100);
  assert.equal(state.pets.cat.tasks.length, 5);
  assert.equal(nightBlocker(state.pets.cat), '');
  assert.equal(stageOf(state.pets.cat.xp).name, '成长期');
});
test('night requires care, then advances growth and restores daily economy', () => {
  let state = freshGame();
  assert.equal(nextDay(state), state);
  for (const task of ['feed', 'bath', 'doctor']) state = care(state, task, 'chicken');
  const before = structuredClone(state);
  state = nextDay(state);
  assert.equal(state.day, 2);
  assert.equal(state.coins, before.coins + 60);
  assert.equal(state.pets.cat.xp, before.pets.cat.xp + 25);
  assert.equal(state.pets.cat.hunger, 51);
  assert.equal(state.pets.cat.tasks.length, 0);
  assert.deepEqual(state.pets.dog, before.pets.dog);
});
test('growth stages unlock at their exact thresholds', () => {
  assert.equal(stageOf(74).index, 0); assert.equal(stageOf(75).index, 1);
  assert.equal(stageOf(179).index, 1); assert.equal(stageOf(180).index, 2);
});
test('pets retain independent food, care, growth and journal state', () => {
  let state = freshGame(); state.pets.dog.adopted = true; state.selected = 'dog';
  const beforeCat = structuredClone(state.pets.cat);
  state = care(state, 'bath');
  assert.equal(state.pets.dog.clean, 100);
  assert.deepEqual(state.pets.cat, beforeCat);
  assert.equal(state.journal[0].pet, 'dog');
});
test('save loading tolerates blocked storage and rejects invalid states', () => {
  assert.deepEqual(loadGame({ getItem: () => { throw new Error('blocked'); } }), freshGame());
  assert.deepEqual(loadGame({ getItem: () => 'broken' }), freshGame());
  const state = freshGame();
  assert.deepEqual(loadGame({ getItem: () => JSON.stringify(state) }), state);
  state.pets.cat.clean = 999;
  assert.deepEqual(loadGame({ getItem: () => JSON.stringify(state) }), freshGame());
});
