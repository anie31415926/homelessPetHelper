import PF from 'pathfinding';

export const MODES = ['puzzle', 'cleanup', 'guide'];
export const INFO = {
  puzzle: { name: '场景解谜', place: '雨后的小仓库', animal: '木箱后的小猫', kind: 'cat' },
  cleanup: { name: '清理开路', place: '长满荒草的小院', animal: '院子里的两个小家伙', kind: 'cat' },
  guide: { name: '引导回家', place: '河畔的弯弯小路', animal: '容易分心的小狗', kind: 'dog' },
};
export const COLS = 7;
export const ROWS = 8;
export const CELL = 44;
export const BOARD_X = 41;
export const BOARD_Y = 100;
export const HOME = { col: 3, row: 7 };
export const center = (col, row) => ({ x: BOARD_X + (col + .5) * CELL, y: BOARD_Y + (row + .5) * CELL });
export const cellAt = point => ({ col: Math.floor((point.x - BOARD_X) / CELL), row: Math.floor((point.y - BOARD_Y) / CELL) });
export const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
export const clamp = (n, low, high) => Math.min(high, Math.max(low, n));
export const inBoard = (col, row) => Number.isInteger(col) && Number.isInteger(row) && col >= 0 && col < COLS && row >= 0 && row < ROWS;
const finder = new PF.AStarFinder({ allowDiagonal: false });
const PUZZLE_MAP = ['#######', '#.....#', '###.###', '#.....#', '~~~~~~~', '#..#..#', '#.....#', '###.###'];
export const GUIDE_WALLS = [[2, 3], [3, 3], [4, 3], [1, 5]];
export const BRIDGES = [1, 5];
const base = mode => ({ mode, elapsed: 0, clock: 0, actions: 0, done: false, notified: false, effects: [], message: '', selected: null });
const actor = (col, row, extra = {}) => ({ ...center(col, row), path: [], ...extra });

export function createExperiment(mode) {
  if (mode === 'puzzle') return {
    ...base(mode), tool: 'move', history: [],
    boxes: [{ id: 'a', col: 3, row: 2 }, { id: 'b', col: 5, row: 5 }],
    bridge: null, bait: null, animal: actor(5, 1), message: '小猫被困在仓库后面了',
  };
  if (mode === 'cleanup') {
    const items = [];
    for (let row = 1; row <= 6; row++) for (let col = 1; col <= 5; col++) {
      const type = ['litter', 'weeds', 'mud'][(row * 2 + col) % 3];
      const hp = type === 'weeds' ? .65 : type === 'mud' ? .5 : .25;
      items.push({ id: `junk-${col}-${row}`, col, row, type, hp, maxHp: hp });
    }
    for (const col of [2, 5]) items.push({ id: `nest-${col}`, col, row: 0, type: 'weeds', hp: 1.2, maxHp: 1.2 });
    return {
      ...base(mode), tool: 'litter', items, total: items.length, cleaned: 0,
      pets: [actor(2, 0, { id: 'cat', kind: 'cat', following: false, safe: false }), actor(5, 0, { id: 'dog', kind: 'dog', following: false, safe: false })],
      message: '杂草后面，好像有两双小眼睛', brush: null,
    };
  }
  return {
    ...base('guide'), player: actor(3, 6), animal: actor(4, 1),
    slow: true, toy: false, food: 3, bait: null, trust: .3, attention: '观察',
    moving: false, distractionTime: 0, retreats: 0, message: '小狗在悄悄观察你',
  };
}

export function matrixFor(game, ignoreBox = null) {
  const matrix = Array.from({ length: ROWS }, (_, row) => Array.from({ length: COLS }, (_, col) => {
    if (game.mode === 'puzzle') {
      const tile = PUZZLE_MAP[row][col];
      if (tile === '#' || (tile === '~' && game.bridge !== col)) return 1;
      return game.boxes.some(box => box.id !== ignoreBox && box.col === col && box.row === row) ? 1 : 0;
    }
    if (col === 0 || col === 6) return 1;
    if (game.mode === 'guide') return GUIDE_WALLS.some(([x, y]) => col === x && row === y) ? 1 : 0;
    return game.items.some(item => item.hp > 0 && item.type !== 'mud' && item.col === col && item.row === row) ? 1 : 0;
  }));
  return matrix;
}

export function findRoute(matrix, from, to) {
  if (!inBoard(from.col, from.row) || !inBoard(to.col, to.row) || matrix[to.row][to.col]) return [];
  const copy = matrix.map(row => [...row]);
  copy[from.row][from.col] = 0;
  return finder.findPath(from.col, from.row, to.col, to.row, new PF.Grid(copy));
}

function routeActor(game, value, point) {
  const cell = cellAt(value);
  const next = value.path[0] || center(cell.col, cell.row);
  const route = findRoute(matrixFor(game), cellAt(next), cellAt(point));
  if (!route.length) return [];
  const prefix = distance(value, next) > 1 ? [{ x: next.x, y: next.y }] : [];
  return [...prefix, ...route.slice(1).map(([col, row]) => center(col, row))];
}

function advanceActor(value, speed, dt) {
  let budget = speed * dt;
  while (value.path.length && budget > 0) {
    const next = value.path[0];
    const gap = distance(value, next);
    if (gap <= budget) {
      value.x = next.x; value.y = next.y;
      budget -= gap; value.path.shift();
    } else {
      value.x += (next.x - value.x) / gap * budget;
      value.y += (next.y - value.y) / gap * budget;
      budget = 0;
    }
  }
}

function remember(game) {
  const { history, ...snapshot } = game;
  history.push(structuredClone(snapshot));
  if (history.length > 40) history.shift();
}

export function undoPuzzle(game) {
  if (game.mode !== 'puzzle' || !game.history.length || game.done) return false;
  const history = game.history;
  Object.assign(game, history.pop(), { history });
  game.message = '回到上一步了';
  return true;
}

function updatePuzzlePath(game) {
  game.animal.path = [];
  if (!game.bait) return;
  const route = findRoute(matrixFor(game), cellAt(game.animal), game.bait);
  if (route.length) {
    game.animal.path = route.slice(1).map(([col, row]) => center(col, row));
    game.message = '小猫探出头，跟着香味走来了';
  } else game.message = '闻到香味了，可是路还不通';
}

export function slideBox(game, id, col, row) {
  if (game.mode !== 'puzzle' || game.done || !inBoard(col, row) || game.animal.path.length) return false;
  const box = game.boxes.find(value => value.id === id);
  if (!box || (box.col === col && box.row === row) || (box.col !== col && box.row !== row)) return false;
  const matrix = matrixFor(game, id);
  const dx = Math.sign(col - box.col);
  const dy = Math.sign(row - box.row);
  const steps = Math.max(Math.abs(col - box.col), Math.abs(row - box.row));
  const animalCell = cellAt(game.animal);
  for (let i = 1; i <= steps; i++) {
    const x = box.col + dx * i;
    const y = box.row + dy * i;
    if (matrix[y]?.[x] !== 0 || (animalCell.col === x && animalCell.row === y) || (x === HOME.col && y === HOME.row)) {
      game.message = '箱子在这里被挡住了';
      return false;
    }
  }
  remember(game);
  box.col = col; box.row = row;
  game.actions++;
  game.selected = id;
  game.message = '挪开一点，留出一条路';
  updatePuzzlePath(game);
  return true;
}

export function bridgeAt(game, col, row) {
  if (game.mode !== 'puzzle' || game.done || row !== 4 || !BRIDGES.includes(col) || game.bridge === col || game.animal.path.length) return false;
  remember(game);
  game.bridge = col;
  game.actions++;
  game.message = '木板稳稳搭好了';
  updatePuzzlePath(game);
  return true;
}

export function puzzleFood(game, col, row) {
  if (game.mode !== 'puzzle' || game.done || !inBoard(col, row) || matrixFor(game)[row][col] || game.animal.path.length) return false;
  remember(game);
  game.bait = { col, row };
  game.actions++;
  updatePuzzlePath(game);
  return true;
}

export function reachableCleanup(game) {
  const matrix = matrixFor(game);
  return (col, row) => Boolean(findRoute(matrix, HOME, { col, row }).length);
}

export function canClean(game, item) {
  if (game.mode !== 'cleanup' || item.hp <= 0 || game.done) return false;
  const reachable = reachableCleanup(game);
  return [[0, 0], [0, 1], [0, -1], [1, 0], [-1, 0]].some(([dx, dy]) => reachable(item.col + dx, item.row + dy));
}

export function cleanAt(game, point, dt, tool = game.tool) {
  if (game.mode !== 'cleanup' || game.done) return false;
  let changed = false;
  for (const item of game.items) {
    if (item.hp <= 0 || distance(center(item.col, item.row), point) > 31 || item.type !== tool || !canClean(game, item)) continue;
    item.hp = Math.max(0, item.hp - clamp(dt, 0, 1));
    changed = true;
    game.message = tool === 'weeds' ? '杂草一点点倒下了' : tool === 'mud' ? '泥斑慢慢洗干净了' : '把散落的杂物收起来';
    if (item.hp === 0) {
      game.cleaned++;
      game.actions++;
      game.effects.push({ ...center(item.col, item.row), at: game.clock, kind: tool });
      game.message = game.cleaned % 4 === 0 ? '通道越来越宽敞了' : '干净一小块，又靠近一点';
      const revealed = game.pets.some(pet => !pet.following && canRescue(game, pet));
      if (revealed) game.message = '小家伙露出来了，它在等你';
    }
  }
  return changed;
}

export function canRescue(game, pet) {
  if (game.mode !== 'cleanup' || pet.safe || pet.following || game.done) return false;
  const cell = cellAt(pet);
  return !game.items.some(item => item.hp > 0 && item.type !== 'mud' && item.col === cell.col && item.row === cell.row) && reachableCleanup(game)(cell.col, cell.row);
}

export function rescueCleanPet(game, id) {
  const pet = game.pets?.find(value => value.id === id);
  if (!pet || !canRescue(game, pet)) return false;
  pet.path = routeActor(game, pet, center(HOME.col, HOME.row));
  pet.following = true;
  game.actions++;
  game.message = pet.kind === 'cat' ? '小猫愿意跟你走了' : '小狗摇着尾巴走来了';
  return true;
}

export function guideDestination(game, col, row) {
  if (game.mode !== 'guide' || game.done || !inBoard(col, row)) return false;
  const path = findRoute(matrixFor(game), cellAt(game.player), { col, row });
  if (!path.length) { game.message = '这里是长椅，绕一绕吧'; return false; }
  game.player.path = routeActor(game, game.player, center(col, row));
  game.actions++;
  return true;
}

export function moveGuide(game, dx, dy, dt) {
  if (game.mode !== 'guide' || game.done) return false;
  const length = Math.hypot(dx, dy);
  if (!length) return false;
  game.player.path = [];
  const speed = game.slow ? 51 : 115;
  const point = { x: game.player.x + dx / length * speed * dt, y: game.player.y + dy / length * speed * dt };
  const cell = cellAt(point);
  if (!inBoard(cell.col, cell.row) || matrixFor(game)[cell.row][cell.col]) return false;
  Object.assign(game.player, point);
  game.moving = true;
  return true;
}

export function guideFood(game) {
  if (game.mode !== 'guide' || game.done || game.bait || game.food <= 0) return false;
  game.food--;
  game.actions++;
  game.bait = { ...game.player, path: undefined, eaten: 0, life: 18 };
  game.message = '小狗抬起鼻子，闻到了食物';
  return true;
}

function tickGuide(game, dt) {
  const before = { x: game.player.x, y: game.player.y };
  advanceActor(game.player, game.slow ? 51 : 115, dt);
  const moving = game.moving || distance(before, game.player) > .01;
  const gap = distance(game.player, game.animal);
  const home = center(HOME.col, HOME.row);
  const ball = center(5, 4);
  let destination = null;
  let speed = 67;
  if (distance(game.animal, home) < 88 && distance(game.player, home) < 57 && game.trust >= .55) {
    destination = home;
    game.attention = '安心进箱';
    game.message = '它正往救援箱里探头';
    speed = 60;
  } else if (game.bait) {
    const bait = game.bait;
    bait.life -= dt;
    if (distance(game.animal, bait) > 9) {
      destination = bait;
      game.attention = '闻到香味';
      game.message = '小狗抬起鼻子，向食物走来了';
    } else {
      bait.eaten += dt;
      game.trust = clamp(game.trust + dt * .19, 0, 1);
      game.attention = '吃点东西';
      game.message = '它安心地吃着，身体慢慢放松了';
      if (bait.eaten > 2) game.bait = null;
    }
    if (bait.life <= 0) game.bait = null;
  } else if (moving && !game.slow && gap < 65) {
    game.trust = clamp(game.trust - dt * .23, .12, 1);
    destination = center(4, 1);
    game.attention = '有点紧张';
    game.message = '脚步太急，小狗往后缩了缩';
  } else if (game.toy && gap < 154 && game.trust >= .34) {
    game.attention = '跟着你';
    game.trust = clamp(game.trust + dt * .065, 0, 1);
    if (gap > 49) destination = game.player;
    else if (gap < 35) game.attention = '靠近你';
    game.message = gap > 49 ? '小狗迈着小步，跟在你身边' : '它在你的身旁，安心地停下来';
  } else if (distance(game.animal, ball) < 120 && (!game.toy || gap > 154)) {
    destination = ball;
    game.attention = '被球吸引';
    game.distractionTime += dt;
    game.message = '路边的球把它的注意力吸引走了';
  } else {
    game.attention = '观察';
    if (gap < 150 && (game.slow || !moving)) game.trust = clamp(game.trust + dt * .13, 0, 1);
    game.message = gap > 190 && game.toy ? '你走远了，小狗停下来找你' : '小狗在悄悄观察你';
  }
  if (destination) {
    // Replan on the grid so neither the dog nor the player cuts through a bench.
    const currentGoal = game.animal.path.at(-1);
    if (!currentGoal || distance(currentGoal, destination) > 18) game.animal.path = routeActor(game, game.animal, destination);
    advanceActor(game.animal, speed, dt);
  } else game.animal.path = [];
  if (game.attention === '安心进箱' && distance(game.animal, home) < 10) {
    game.done = true;
    game.message = '它终于放心地跟你回家了';
  }
  game.moving = false;
}

export function tick(game, dt) {
  dt = clamp(dt, 0, .05);
  game.clock += dt;
  if (!game.done) game.elapsed += dt;
  game.effects = game.effects.filter(effect => game.clock - effect.at < .8);
  if (game.done) return;
  if (game.mode === 'puzzle') {
    advanceActor(game.animal, 69, dt);
    if (!game.animal.path.length && distance(game.animal, center(HOME.col, HOME.row)) < 5) {
      game.done = true;
      game.message = '小猫自己走进了救援箱';
    }
  } else if (game.mode === 'cleanup') {
    for (const pet of game.pets) if (pet.following && !pet.safe) {
      advanceActor(pet, 93, dt);
      if (!pet.path.length) { pet.safe = true; game.message = '又一个小家伙平安到达'; }
    }
    if (game.pets.every(pet => pet.safe)) { game.done = true; game.message = '两个小家伙，都安全啦'; }
  } else tickGuide(game, dt);
}
