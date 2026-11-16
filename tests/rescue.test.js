import test from 'node:test';
import assert from 'node:assert/strict';
import { MAZE, NET_LENGTH, castNet, createGame, enterRescue, mazePath, moveMaze, moveRescuer, netPoint, segmentHitsCircle, tapMaze, throwFood, updateGame } from '../src/rescue/game.js';

const advance = (game, seconds) => { for (let i = 0; i < Math.ceil(seconds * 100); i++) updateGame(game, .01); };

test('both maze entrances have a connected route to the opposite exit', () => {
  for (const entrance of ['bottom', 'top']) {
    const game = createGame({ entrance });
    const goal = entrance === 'bottom' ? 0 : 10;
    const path = mazePath(game.player, { col: 5, row: goal });
    assert.ok(path.length > 20);
    for (const [col, row] of path.slice(1)) {
      assert.notEqual(MAZE[row][col], '#');
      const direction = col > game.player.col ? 'right' : col < game.player.col ? 'left' : row > game.player.row ? 'down' : 'up';
      assert.ok(moveMaze(game, direction));
      advance(game, .14);
    }
    advance(game, .5);
    assert.equal(game.phase, 'rescue');
    assert.equal(game.steps, path.length - 1);
  }
});

test('maze walls, bounds, movement cadence and invalid input are enforced', () => {
  const game = createGame();
  assert.equal(moveMaze(game, 'down'), false);
  assert.equal(moveMaze(game, 'left'), false);
  assert.equal(moveMaze(game, 'missing'), false);
  assert.ok(moveMaze(game, 'up'));
  assert.equal(moveMaze(game, 'up'), false);
  assert.deepEqual(mazePath(game.player, { col: 2, row: -1 }), []);
  assert.deepEqual(mazePath(game.player, { col: 2.5, row: 9 }), []);
  assert.deepEqual(tapMaze(createGame(), 9, 3), [], 'tap must not auto-solve around corners');
  assert.deepEqual(tapMaze(createGame(), 5, 9), [[5, 9]]);
});

test('rescue controls are unavailable before arrival and rescuer stays inside the path', () => {
  const game = createGame();
  assert.equal(castNet(game), false);
  assert.equal(throwFood(game), false);
  enterRescue(game);
  moveRescuer(game, 'up', 100);
  moveRescuer(game, 'left', 100);
  assert.equal(game.player.x, 55);
  assert.equal(game.player.y, 372);
  moveRescuer(game, 'down', 100);
  moveRescuer(game, 'right', 100);
  assert.equal(game.player.x, 335);
  assert.equal(game.player.y, 510);
});

test('out-of-range cast misses, retracts and can be repeated without punishment', () => {
  const game = createGame();
  enterRescue(game);
  game.angle = -1;
  assert.ok(castNet(game));
  assert.equal(castNet(game), false);
  const before = { x: game.player.x, y: game.player.y };
  moveRescuer(game, 'up', 1);
  assert.deepEqual({ x: game.player.x, y: game.player.y }, before);
  for (let i = 0; i < 150; i++) {
    updateGame(game, .01);
    if (game.net) assert.ok(game.net.length <= NET_LENGTH);
  }
  assert.equal(game.phase, 'rescue');
  assert.equal(game.animal.caught, false);
  assert.equal(game.net, null);
  assert.equal(game.casts, 1);
  assert.ok(castNet(game));
});

test('food attracts rather than immediately immobilizing; eating expires and inventory is finite', () => {
  const game = createGame();
  enterRescue(game);
  game.player.y = 385;
  game.angle = 0;
  assert.ok(throwFood(game));
  assert.equal(game.foodLeft, 2);
  assert.equal(game.foodUsed, 1);
  assert.equal(game.animal.eating, 0);
  assert.equal(throwFood(game), false);
  advance(game, 2);
  assert.ok(game.animal.eating > 0);
  const eatingPosition = { x: game.animal.x, y: game.animal.y };
  advance(game, 1);
  assert.deepEqual({ x: game.animal.x, y: game.animal.y }, eatingPosition);
  advance(game, 5);
  assert.equal(game.bait, null);
  assert.equal(game.animal.eating, 0);
  for (let i = 0; i < 2; i++) { assert.ok(throwFood(game)); advance(game, 15); }
  assert.equal(game.foodLeft, 0);
  assert.equal(throwFood(game), false);
  assert.ok(castNet(game), 'food is optional, never a soft lock');
});

test('aligned net catches a moving animal and only completes after safe retraction', () => {
  const game = createGame({ kind: 'dog' });
  enterRescue(game);
  game.player.x = 195;
  game.player.y = 390;
  game.animal.x = 195;
  game.animal.y = 232;
  game.bait = { x: 195, y: 232, life: 12, eaten: true };
  game.animal.eating = 4;
  game.angle = 0;
  assert.ok(castNet(game));
  advance(game, .5);
  assert.ok(game.animal.caught);
  assert.equal(game.phase, 'rescue');
  assert.equal(game.net.mode, 'return');
  assert.equal(throwFood(game), false);
  advance(game, 2);
  assert.equal(game.phase, 'success');
  assert.equal(game.net, null);
  const elapsed = game.elapsed;
  advance(game, 1);
  assert.equal(game.elapsed, elapsed);
  assert.equal(castNet(game), false);
});

test('swept collision and net geometry avoid fast-projectile tunneling', () => {
  assert.ok(segmentHitsCircle({ x: 0, y: 0 }, { x: 100, y: 0 }, { x: 50, y: 2 }, 3));
  assert.equal(segmentHitsCircle({ x: 0, y: 0 }, { x: 0, y: 0 }, { x: 5, y: 0 }, 3), false);
  assert.deepEqual(netPoint({ origin: { x: 5, y: 10 }, angle: 0, length: 7 }), { x: 5, y: 3 });
});
