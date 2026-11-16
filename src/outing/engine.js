import { HOME, canRescue, cellAt, center, clamp, createExperiment, findRoute, matrixFor, tick } from '../playlab/engine.js';

export const PROFILES = {
  hungry: { kind: 'cat', name: '有些饿的小猫', alert: 74, mood: 'hungry' },
  shy: { kind: 'dog', name: '谨慎的小狗', alert: 82, mood: 'wary' },
  curious: { kind: 'cat', name: '好奇的小猫', alert: 62, mood: 'curious' },
};
export const DISTANCES = ['身旁', '附近', '远处'];

export function seededRandom(seed) {
  let value = seed >>> 0;
  return () => {
    value += 0x6D2B79F5;
    let result = Math.imul(value ^ value >>> 15, 1 | value);
    result ^= result + Math.imul(result ^ result >>> 7, 61 | result);
    return ((result ^ result >>> 14) >>> 0) / 4294967296;
  };
}

export function createOuting({ seed = Math.floor(Math.random() * 0xFFFFFFFF), route, profile, direct = false } = {}) {
  const random = seededRandom(seed);
  const routeRoll = random();
  const profileRoll = random();
  const templateRoll = random();
  const selectedRoute = ['puzzle', 'cleanup'].includes(route) ? route : routeRoll < .5 ? 'puzzle' : 'cleanup';
  const selectedProfile = Object.hasOwn(PROFILES, profile) ? profile : Object.keys(PROFILES)[Math.floor(profileRoll * 3)];
  const template = templateRoll < .5 ? 'left' : 'right';
  const approach = createExperiment(selectedRoute);
  approach.kind = PROFILES[selectedProfile].kind;
  approach.goalLabel = '走近动物';
  if (selectedRoute === 'puzzle') {
    approach.animal = { ...center(template === 'left' ? 1 : 5, 1), path: [] };
    approach.boxes[1].col = template === 'left' ? 1 : 5;
    approach.message = '先挪开障碍，给它留一条安全的路';
  } else {
    const animalCol = template === 'left' ? 2 : 4;
    approach.items = [];
    for (let row = 3; row <= 6; row++) for (let col = 1; col <= 5; col++) {
      if (random() < .17 && col !== 3) continue;
      const type = ['litter', 'weeds', 'mud'][Math.floor(random() * 3)];
      const hp = type === 'litter' ? .24 : type === 'weeds' ? .55 : .4;
      approach.items.push({ id: `debris-${col}-${row}`, col, row, type, hp, maxHp: hp });
    }
    approach.items.push({ id: 'shelter', col: animalCol, row: 1, type: 'weeds', hp: .8, maxHp: .8 });
    approach.total = approach.items.length;
    approach.pets = [{ ...center(animalCol, 1), path: [], id: 'found', kind: approach.kind, following: false, safe: false }];
    approach.message = '杂草后面的小家伙，正偷偷看着你';
  }
  const game = {
    seed: seed >>> 0, route: selectedRoute, profile: selectedProfile, template,
    stage: 'approach', elapsed: 0, approachTime: 0, turnTime: 0, clock: 0,
    approach, ready: false, turn: null, pats: 0,
  };
  if (direct) beginNegotiation(game, true);
  return game;
}

export function approachReady(game) {
  if (game.route === 'cleanup') return canRescue(game.approach, game.approach.pets[0]);
  return findRoute(matrixFor(game.approach), HOME, cellAt(game.approach.animal)).length > 0;
}

export function beginNegotiation(game, direct = false) {
  if (game.stage !== 'approach' || (!direct && !approachReady(game))) return false;
  game.stage = 'negotiate';
  game.approach.brush = null;
  const profile = PROFILES[game.profile];
  game.turn = {
    alert: profile.alert, distance: 2, mood: profile.mood, food: 3, fed: false,
    round: 1, phase: 'choose', remaining: 0, lastAction: null, streak: 0,
    log: [], reaction: '它注意到了你，还不确定你是否安全。', delta: 0,
    effect: null, rescued: false,
  };
  return true;
}

export function cueFor(turn) {
  if (turn.rescued) return '它把下巴轻轻搭在箱沿，终于放松下来了。';
  if (turn.distance === 0 && turn.alert <= 20 && turn.mood !== 'eating') return '它主动蹭近，身体放松，愿意跟你走了。';
  if (turn.mood === 'hungry') return '它盯着你的食物袋，舔了舔嘴，却不敢过来。';
  if (turn.mood === 'eating') return '它正低头吃东西，但耳朵仍留意着你的动作。';
  if (turn.mood === 'wary') return '它缩着身体，耳朵向后，正紧盯着你的手。';
  if (turn.mood === 'scent') return '它闻着食物，却还不敢低头。也许需要一点空间。';
  if (turn.mood === 'curious') return '它的耳朵转向你，视线追着你身边的小玩具。';
  if (turn.mood === 'playing') return '它伸爪碰了碰玩具，身体不再那么紧绷。';
  if (turn.alert > 45) return '它开始观察你，但身体仍缩在原来的位置。';
  if (turn.distance === 2) return '它身体放松了，抬头看看你，又往前探了探。';
  return '它停在你附近，闻闻你的方向，没有再后退。';
}

export function actionOptions(turn) {
  const ready = turn.distance === 0 && turn.alert <= 20 && turn.mood !== 'eating';
  return [
    { id: 'food', label: '放下食物', detail: turn.food > 0 ? `还剩 ${turn.food} 份` : '食物用完了', disabled: turn.food <= 0 },
    { id: 'wait', label: turn.alert >= 65 ? '退开等待' : '蹲下等待', detail: '留一点空间' },
    { id: 'toy', label: '轻晃玩具', detail: '看看它的兴趣' },
    { id: ready ? 'invite' : 'approach', label: ready ? '邀请进箱' : '慢慢靠近', detail: ready ? '一起安全回站' : DISTANCES[Math.max(0, turn.distance - 1)] },
  ];
}

export function chooseAction(game, action) {
  const turn = game.turn;
  if (game.stage !== 'negotiate' || !turn || turn.phase !== 'choose' || !['food', 'wait', 'toy', 'approach', 'invite'].includes(action)) return false;
  if (action === 'food' && turn.food <= 0) return false;
  if (action === 'invite' && (turn.distance !== 0 || turn.alert > 20 || turn.mood === 'eating')) return false;
  const before = turn.alert;
  const oldMood = turn.mood;
  const repeat = turn.lastAction === action;
  turn.streak = repeat ? turn.streak + 1 : 1;
  const reduce = amount => { turn.alert = Math.max(0, turn.alert - amount); };
  const worry = amount => { turn.alert = Math.min(100, turn.alert + amount); };
  let response;
  let outcome = 'calm';
  if (action === 'food') {
    turn.food--;
    if (oldMood === 'eating') {
      worry(5); response = '你又伸手放食物，它停下进食，警觉地抬头。'; outcome = 'startled';
    } else if (oldMood === 'wary' && turn.alert > 60) {
      reduce(8); turn.mood = 'scent'; response = '香味让它探了探鼻子，但它还不敢低头。';
    } else if (!turn.fed) {
      reduce(oldMood === 'hungry' ? 24 : 16); turn.mood = 'eating'; turn.fed = true;
      response = '你放下食物后退开，它试探着吃了第一口。'; outcome = 'eating';
    } else {
      turn.alert = Math.min(turn.alert, Math.max(28, turn.alert - 3)); turn.mood = 'assess';
      response = '它已经不饿了，闻了闻食物，又把目光转向你。';
    }
  } else if (action === 'wait') {
    if (oldMood === 'eating') {
      reduce(19); turn.mood = 'assess'; response = '你没有打扰，它安心吃完，耳朵也慢慢放松了。';
    } else if (oldMood === 'scent') {
      reduce(20); turn.mood = 'eating'; turn.fed = true;
      response = '你退开后，它终于放心低头，轻轻吃了起来。'; outcome = 'eating';
    } else if (oldMood === 'wary') {
      reduce(18); turn.mood = 'assess'; response = '你收回手，蹲低身体。它不再缩得那么紧了。';
    } else if (oldMood === 'playing') {
      reduce(12); turn.mood = 'assess'; response = '你停下来等它，它玩够了，抬头认真看着你。';
    } else {
      const floor = oldMood === 'hungry' ? 54 : turn.distance < 2 ? 20 : 32;
      turn.alert = Math.min(turn.alert, Math.max(floor, turn.alert - (repeat ? 3 : 10)));
      response = turn.alert === before ? '它已经习惯你安静待着，正等着你的下一个动作。' : '你安静地陪着，它的呼吸慢慢平稳下来。';
    }
  } else if (action === 'toy') {
    if (oldMood === 'eating' || oldMood === 'hungry' || oldMood === 'wary' || turn.alert > 68) {
      worry(7); response = oldMood === 'hungry' ? '它对玩具没有兴趣，仍盯着你的食物袋。' : '突然晃动的玩具让它往后缩了一下。'; outcome = 'startled';
      if (oldMood !== 'hungry' && oldMood !== 'eating') turn.mood = 'wary';
    } else {
      const amount = oldMood === 'curious' ? 24 : repeat ? 4 : 14;
      turn.alert = Math.min(turn.alert, Math.max(14, turn.alert - amount));
      turn.mood = 'playing'; response = repeat ? '它又碰了碰玩具，但已经没有刚才那么感兴趣了。' : '它试探着碰了碰玩具，眼神柔和了许多。'; outcome = 'play';
    }
  } else if (action === 'approach') {
    const threshold = turn.distance === 2 ? 48 : 28;
    if (turn.distance === 0) {
      response = '已经在它身旁了，先让它再安心一点吧。';
    } else if (turn.alert > threshold || oldMood === 'eating' || oldMood === 'scent') {
      worry(12); turn.distance = Math.min(2, turn.distance + 1); turn.mood = 'wary';
      response = oldMood === 'eating' ? '你在它进食时靠近，它立刻退开，护住了食物。' : '你靠近得有些早，它后退了一小步。'; outcome = 'startled';
    } else {
      turn.distance--; reduce(8); turn.mood = 'assess';
      response = turn.distance === 0 ? '你慢慢伸出手，它闻了闻，轻轻靠在你身旁。' : '你只往前一小步。它看着你，没有再退开。'; outcome = 'closer';
    }
  } else {
    turn.rescued = true; turn.mood = 'safe'; turn.alert = 0;
    response = '你把救援箱放低，它自己走进去，回头轻轻看了你一眼。'; outcome = 'safe';
  }
  turn.alert = clamp(Math.round(turn.alert), 0, 100);
  turn.delta = turn.alert - before;
  turn.reaction = response;
  turn.effect = outcome;
  turn.lastAction = action;
  turn.phase = 'react';
  turn.remaining = 1.15;
  turn.log.push({ round: turn.round, action, response, delta: turn.delta, outcome });
  return true;
}

export function updateOuting(game, dt) {
  dt = clamp(dt, 0, .05);
  game.clock += dt;
  if (!['success', 'station'].includes(game.stage)) game.elapsed += dt;
  if (game.stage === 'approach') {
    game.approachTime += dt;
    tick(game.approach, dt);
    game.ready = approachReady(game);
  } else if (game.stage === 'negotiate') {
    game.turnTime += dt;
    const turn = game.turn;
    if (turn.phase === 'react') {
      turn.remaining = Math.max(0, turn.remaining - dt);
      if (turn.remaining === 0) {
        if (turn.rescued) game.stage = 'success';
        else { turn.phase = 'choose'; turn.round++; }
      }
    }
  }
}
