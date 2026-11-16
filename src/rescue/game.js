import PF from 'pathfinding';

export const MAZE = [
  '#####.#####',
  '#...#.....#',
  '#.#.#####.#',
  '#.#.......#',
  '#.#######.#',
  '#.....#...#',
  '#####.#.###',
  '#...#.#...#',
  '#.#.#.###.#',
  '#.#.......#',
  '#####.#####',
];
export const TILE = 30;
export const MAP_X = 30;
export const MAP_Y = 178;
export const NET_LENGTH = 210;
export const DIRECTIONS = { up: [0, -1], down: [0, 1], left: [-1, 0], right: [1, 0] };
const finder = new PF.AStarFinder({ allowDiagonal: false });
const matrix = MAZE.map(row => [...row].map(cell => cell === '#' ? 1 : 0));
export const cellPoint = (col, row) => ({ x: MAP_X + col * TILE + TILE / 2, y: MAP_Y + row * TILE + TILE / 2 });
export const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
export const distance = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

export function mazePath(from, to) {
  if (!Number.isInteger(to.col) || !Number.isInteger(to.row) || !matrix[to.row] || matrix[to.row][to.col] !== 0) return [];
  return finder.findPath(from.col, from.row, to.col, to.row, new PF.Grid(matrix));
}

export function createGame({ entrance = 'bottom', kind = 'cat' } = {}) {
  const row = entrance === 'bottom' ? 10 : 0;
  const point = cellPoint(5, row);
  return {
    phase: 'maze', entrance, kind, clock: 0, elapsed: 0, steps: 0, casts: 0, foodUsed: 0,
    foodLeft: 3, status: '花园里传来了小小的叫声', statusUntil: 5,
    player: { ...point, col: 5, row, targetX: point.x, targetY: point.y },
    animal: { x: 194, y: 238, caught: false, eating: 0 },
    net: null, bait: null, effects: [], moveCooldown: 0, angle: 0, petCount: 0,
  };
}

export function tell(game, message, duration = 3) {
  game.status = message;
  game.statusUntil = game.clock + duration;
}

export function enterRescue(game) {
  game.phase = 'rescue';
  Object.assign(game.player, { x: 195, y: 495, targetX: 195, targetY: 495 });
  tell(game, game.kind === 'cat' ? '找到你了，小猫' : '找到你了，小狗', 4);
}

export function moveMaze(game, direction) {
  if (game.phase !== 'maze' || game.moveCooldown > 0 || !DIRECTIONS[direction]) return false;
  const [dx, dy] = DIRECTIONS[direction];
  const col = game.player.col + dx;
  const row = game.player.row + dy;
  if (matrix[row]?.[col] !== 0) return false;
  const point = cellPoint(col, row);
  Object.assign(game.player, { col, row, targetX: point.x, targetY: point.y });
  game.steps++;
  game.moveCooldown = .135;
  return true;
}

export function tapMaze(game, col, row) {
  const path = mazePath(game.player, { col, row });
  // A tap follows only a straight corridor, never solves the maze for the player.
  if (path.length < 2 || (col !== game.player.col && row !== game.player.row)) return [];
  if (!path.every(([x, y]) => col === game.player.col ? x === col : y === row)) return [];
  return path.slice(1);
}

export function moveRescuer(game, direction, dt) {
  if (game.phase !== 'rescue' || game.net || !DIRECTIONS[direction]) return;
  const [dx, dy] = DIRECTIONS[direction];
  game.player.x = clamp(game.player.x + dx * dt * 128, 55, 335);
  game.player.y = clamp(game.player.y + dy * dt * 128, 372, 510);
}

export function castNet(game) {
  if (game.phase !== 'rescue' || game.net) return false;
  game.casts++;
  game.net = {
    origin: { x: game.player.x, y: game.player.y - 20 },
    angle: game.angle, length: 0, mode: 'out', caught: false,
  };
  tell(game, '网兜出发了', 1);
  return true;
}

export function throwFood(game) {
  if (game.phase !== 'rescue' || game.net || game.bait || game.foodLeft <= 0) return false;
  const x = clamp(game.player.x + Math.sin(game.angle) * 175, 65, 325);
  const y = clamp(game.player.y - 20 - Math.cos(game.angle) * 175, 185, 292);
  game.bait = { x, y, life: 12, eaten: false };
  game.foodLeft--;
  game.foodUsed++;
  tell(game, '它闻到了食物的香味', 3);
  return true;
}

export function netPoint(net, length = net.length) {
  return { x: net.origin.x + Math.sin(net.angle) * length, y: net.origin.y - Math.cos(net.angle) * length };
}

export function segmentHitsCircle(a, b, center, radius) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  const lengthSquared = dx * dx + dy * dy;
  const t = lengthSquared ? clamp(((center.x - a.x) * dx + (center.y - a.y) * dy) / lengthSquared, 0, 1) : 0;
  return Math.hypot(center.x - a.x - t * dx, center.y - a.y - t * dy) <= radius;
}

export function updateGame(game, dt) {
  dt = clamp(dt, 0, .05);
  game.clock += dt;
  game.moveCooldown = Math.max(0, game.moveCooldown - dt);
  game.effects = game.effects.filter(effect => game.clock - effect.at < 1.2);
  if (game.phase === 'maze' || game.phase === 'rescue') game.elapsed += dt;
  if (game.phase === 'maze') {
    const p = game.player;
    const factor = Math.min(1, dt * 24);
    p.x += (p.targetX - p.x) * factor;
    p.y += (p.targetY - p.y) * factor;
    const goal = game.entrance === 'bottom' ? 0 : 10;
    if (p.row === goal && distance(p, { x: p.targetX, y: p.targetY }) < 2) enterRescue(game);
    return;
  }
  if (game.phase !== 'rescue') return;
  if (!game.net) game.angle = Math.sin(game.clock * 1.15) * 1.02;
  const animal = game.animal;
  if (!animal.caught) {
    if (game.bait) {
      const bait = game.bait;
      bait.life -= dt;
      const gap = distance(animal, bait);
      if (gap > 3 && !bait.eaten) {
        const step = Math.min(gap, dt * 85);
        animal.x += (bait.x - animal.x) / gap * step;
        animal.y += (bait.y - animal.y) / gap * step;
      } else if (!bait.eaten) {
        bait.eaten = true;
        animal.eating = 4.5;
        tell(game, '它正安心地吃东西', 4.5);
      }
      if (bait.eaten) {
        animal.eating = Math.max(0, animal.eating - dt);
        if (animal.eating === 0) game.bait = null;
      } else if (bait.life <= 0) game.bait = null;
    } else {
      const target = { x: 195 + Math.sin(game.clock * .76) * 104, y: 237 + Math.sin(game.clock * .48 + 1) * 40 };
      const gap = distance(animal, target);
      if (gap > 0) {
        const step = Math.min(gap, dt * (game.kind === 'cat' ? 48 : 58));
        animal.x += (target.x - animal.x) / gap * step;
        animal.y += (target.y - animal.y) / gap * step;
      }
    }
  }
  const net = game.net;
  if (!net) return;
  const previous = netPoint(net);
  if (net.mode === 'out') {
    net.length = Math.min(NET_LENGTH, net.length + dt * 420);
    const tip = netPoint(net);
    if (!animal.caught && segmentHitsCircle(previous, tip, animal, 27)) {
      animal.caught = true;
      net.caught = true;
      net.mode = 'return';
      game.bait = null;
      tell(game, '稳稳接住了，别怕', 4);
    } else if (net.length >= NET_LENGTH) {
      net.mode = 'return';
      tell(game, distance(net.origin, animal) > NET_LENGTH + 27 ? '还差一点距离' : '网兜落空了', 3);
    }
  } else {
    net.length = Math.max(0, net.length - dt * (net.caught ? 165 : 390));
    if (net.length === 0) {
      game.net = null;
      if (net.caught) {
        game.phase = 'success';
        tell(game, '小家伙已经安全了', 30);
      }
    }
  }
  if (game.net?.caught) Object.assign(animal, netPoint(game.net));
}
