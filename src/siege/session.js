import { CATALOG, animalDefinition, laneDefinition } from './catalog.js';
import { routeLength } from './geometry.js';
import { fillHand, record } from './supply.js';
import { advanceCombat } from './combat.js';
import { actAnimal, animalCanAct, animalPeriodMs, updateAnimalState } from './animal.js';
export { evaluateOrder, commitOrder } from './orders.js';

export function createSession({ seed = 20261119, profile = 'calm', sessionId = 'trial' } = {}, catalog = CATALOG) {
    if (!Number.isInteger(seed) || seed < 0 || seed > 0xFFFFFFFF || typeof sessionId !== 'string' || !sessionId) throw new RangeError('Invalid siege identity.');
    const preset = catalog.animalStates.find(item => item.id === profile);
    if (!preset) throw new RangeError('Invalid animal state.');
    const state = { sessionId, seed, rng: seed, serial: 0, profile, phase: 'decision', paused: false,
        elapsedMs: 0, accumulatorMs: 0, playerChargeMs: 0, turn: 1, resolvedTurn: 0,
        energy: catalog.rules.openingEnergy, hand: [], bag: [], units: [], towers: [], effects: [],
        progress: 0, stats: { deployed: 0, merges: 0, towers: 0, delivered: 0, lost: 0, repairs: 0 }, log: [],
        animal: { vigilance: preset.vigilance, stamina: preset.stamina, charge: 0, actions: 0,
            buildsLeft: catalog.rules.animalBuilds, repairsLeft: catalog.rules.animalRepairs, repair: null, nextRepairMs: 0,
            guardLane: null, guardUntilMs: 0, nextSlapMs: 0, recoveryUntilMs: 0,
            intent: { type: 'guard', laneId: catalog.map.lanes.find(lane => lane.id === 'mid' && lane.open)?.id || catalog.map.lanes.find(lane => lane.open).id, padId: null, seenTurn: 1 } } };
    for (const pad of catalog.map.pads.filter(item => item.initial && laneDefinition(catalog, item.laneId).open)) {
        state.towers.push({ id: pad.id, laneId: pad.laneId, distance: routeLength(laneDefinition(catalog, pad.laneId)) * pad.at,
            hp: catalog.tower.hp, status: 'active', readyAtMs: 0, nextShotMs: 0 });
    }
    fillHand(state, catalog, catalog.rules.handSize);
    record(state, catalog, 'event.start');
    return state;
}
function openDecision(state, catalog) {
    state.turn++;
    state.phase = 'decision';
    state.energy = Math.min(catalog.rules.energyCap, state.energy + catalog.rules.turnEnergy);
    fillHand(state, catalog, catalog.rules.turnDraw);
    record(state, catalog, 'event.turn');
}
function tick(state, catalog) {
    const dt = catalog.rules.stepMs;
    state.elapsedMs += dt;
    state.playerChargeMs = Math.min(catalog.rules.playerPeriodMs, state.playerChargeMs + dt);
    state.animal.charge = Math.min(1, state.animal.charge + dt / animalPeriodMs(state, catalog));
    advanceCombat(state, catalog, dt);
    updateAnimalState(state, catalog);
    if (state.progress >= catalog.rules.target) {
        state.phase = 'won'; record(state, catalog, 'event.win'); return;
    }
    if (state.elapsedMs >= catalog.rules.durationMs) {
        state.phase = 'timeout'; record(state, catalog, 'event.timeout'); return;
    }
    // The decision window wins a same-tick tie; a charged animal keeps its action pending.
    if (state.playerChargeMs >= catalog.rules.playerPeriodMs) { openDecision(state, catalog); return; }
    if (state.animal.charge >= 1 && animalCanAct(state)) actAnimal(state, catalog);
}
export function advanceSession(state, elapsedMs, catalog = CATALOG) {
    if (!Number.isFinite(elapsedMs) || elapsedMs < 0 || elapsedMs > 1000) throw new RangeError('advance accepts 0..1000 milliseconds.');
    if (state.phase !== 'running' || state.paused || elapsedMs === 0) return state;
    const next = structuredClone(state);
    next.accumulatorMs += elapsedMs;
    while (next.accumulatorMs >= catalog.rules.stepMs && next.phase === 'running') {
        next.accumulatorMs -= catalog.rules.stepMs;
        tick(next, catalog);
    }
    if (next.phase !== 'running') next.accumulatorMs = 0;
    return next;
}
export function setPaused(state, paused) {
    if (typeof paused !== 'boolean' || !['decision','running'].includes(state.phase) || state.paused === paused) return state;
    return { ...state, paused, accumulatorMs: 0 };
}
export function summary(state, catalog = CATALOG) {
    return { phase: state.phase, progress: state.progress, target: catalog.rules.target, elapsedMs: state.elapsedMs,
        turns: state.turn, animal: animalDefinition(catalog, state.animal).id, ...state.stats };
}
