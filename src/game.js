export const SAVE_KEY = 'fluffy-days-v1';
export const FOOD = [
  { id: 'kibble', name: '幼宠奶糕', note: '奶香酥酥，长身体', amount: 30, cost: 8, color: '#eac785' },
  { id: 'chicken', name: '鸡肉营养餐', note: '一碗满满的元气', amount: 45, cost: 15, color: '#d59c87' },
  { id: 'fish', name: '鲜鱼小罐头', note: '今天也要好好吃饭', amount: 38, cost: 12, color: '#91b9ae' },
];
export const CARE_TASKS = [
  { id: 'feed', label: '吃饱饱' }, { id: 'bath', label: '洗香香' },
  { id: 'groom', label: '变漂亮' }, { id: 'doctor', label: '身体棒' },
];
const clamp = value => Math.max(0, Math.min(100, value));
export function freshGame() {
  return {
    version: 1, day: 1, coins: 180, selected: 'cat', sound: false,
    inventory: { kibble: 5, chicken: 3, fish: 2 },
    pets: {
      cat: { name: '糯米', adopted: true, hunger: 36, clean: 24, health: 62, mood: 66, xp: 12, groomed: false, accessory: 'none', tasks: [], claimed: false },
      dog: { name: '布丁', adopted: false, hunger: 42, clean: 30, health: 65, mood: 58, xp: 0, groomed: false, accessory: 'none', tasks: [], claimed: false },
    },
    journal: [{ day: 1, pet: 'cat', text: '在雨后的路边，遇见了小小的糯米。', type: 'adopt' }],
  };
}
export function loadGame(storage) {
  try {
    const state = JSON.parse(storage.getItem(SAVE_KEY));
    if (!state || state.version !== 1 || !Number.isInteger(state.day) || state.day < 1 || !Number.isFinite(state.coins) || state.coins < 0 || !['cat', 'dog'].includes(state.selected)) return freshGame();
    if (!state.inventory || FOOD.some(food => !Number.isInteger(state.inventory[food.id]) || state.inventory[food.id] < 0)) return freshGame();
    for (const id of ['cat', 'dog']) {
      const pet = state.pets?.[id];
      if (!pet || typeof pet.name !== 'string' || !pet.name.trim() || !Array.isArray(pet.tasks) || !['hunger', 'clean', 'health', 'mood', 'xp'].every(key => Number.isFinite(pet[key]) && pet[key] >= 0) || ['hunger', 'clean', 'health', 'mood'].some(key => pet[key] > 100)) return freshGame();
    }
    if (!state.pets[state.selected].adopted || !Array.isArray(state.journal)) return freshGame();
    return state;
  } catch { return freshGame(); }
}
export function stageOf(xp) {
  if (xp >= 180) return { index: 2, name: '成年期', age: '1 岁', next: 240, start: 180 };
  if (xp >= 75) return { index: 1, name: '成长期', age: '6 个月', next: 180, start: 75 };
  return { index: 0, name: '幼年期', age: '2 个月', next: 75, start: 0 };
}
export function care(state, action, foodId) {
  const id = state.selected;
  const before = state.pets[id];
  if (!before.adopted) return state;
  const pet = { ...before, tasks: [...before.tasks] };
  let inventory = state.inventory;
  const firstToday = !pet.tasks.includes(action);
  if (action === 'feed') {
    const food = FOOD.find(item => item.id === foodId);
    if (!food || !inventory[foodId] || pet.hunger >= 100) return state;
    inventory = { ...inventory, [foodId]: inventory[foodId] - 1 };
    pet.hunger = clamp(pet.hunger + food.amount);
    pet.mood = clamp(pet.mood + 6);
  } else if (action === 'bath') { pet.clean = 100; pet.mood = clamp(pet.mood + 10); }
  else if (action === 'groom') { pet.groomed = true; pet.mood = clamp(pet.mood + 14); }
  else if (action === 'doctor') { pet.health = 100; pet.mood = clamp(pet.mood + 6); }
  else if (action === 'play') { pet.mood = 100; pet.hunger = clamp(pet.hunger - 4); }
  else return state;
  if (firstToday) { pet.tasks.push(action); pet.xp += { feed: 16, bath: 24, groom: 20, doctor: 22, play: 16 }[action]; }
  const text = { feed: '吃饭的时候，连耳朵都开心得抖了抖。', bath: '洗掉了小泥巴，变成香香的小团子。', groom: '乱糟糟的毛毛，终于变得蓬松又整齐。', doctor: '勇敢看完医生，身体恢复了元气。', play: '一起玩了好久，今天又更亲近了一点。' }[action];
  return { ...state, inventory, pets: { ...state.pets, [id]: pet }, journal: firstToday ? [{ day: state.day, pet: id, text, type: action }, ...state.journal].slice(0, 60) : state.journal };
}
export function nightBlocker(pet) {
  if (pet.hunger < 60) return '肚子还有点饿，先吃点东西吧';
  if (pet.clean < 60) return '身上还有小泥巴，先洗个澡吧';
  if (pet.health < 75) return '身体不太舒服，先去看看医生吧';
  return '';
}
export function nextDay(state) {
  if (nightBlocker(state.pets[state.selected])) return state;
  const pets = Object.fromEntries(Object.entries(state.pets).map(([id, pet]) => [id, pet.adopted ? {
    ...pet, hunger: clamp(pet.hunger - 30), clean: clamp(pet.clean - 27), mood: clamp(pet.mood - 18),
    health: clamp(pet.health - 12), xp: pet.xp + (pet.tasks.length >= 3 ? 25 : 8), groomed: false, tasks: [], claimed: false,
  } : pet]));
  return { ...state, day: state.day + 1, coins: state.coins + 60, pets, journal: [{ day: state.day + 1, pet: state.selected, text: '一起醒来的早晨，又长大了一点点。', type: 'grow' }, ...state.journal].slice(0, 60) };
}
