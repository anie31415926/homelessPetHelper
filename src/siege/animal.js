import { animalDefinition, laneDefinition } from './catalog.js';
import { routeLength, sweepArea, unitInArea } from './geometry.js';
import { hitUnit } from './entities.js';
import { record } from './supply.js';
import { removeLost } from './combat.js';

export function pressureLanes(state, catalog) {
    return catalog.map.lanes.filter(lane => lane.open).map(lane => ({ lane,
        pressure: state.units.filter(unit => unit.laneId === lane.id && unit.hp > 0 && !unit.transit)
            .reduce((sum, unit) => sum + catalog.rules.threatBase + catalog.rules.threatNearWeight * (unit.distance / routeLength(lane)) ** catalog.rules.threatExponent, 0) }))
        .sort((a, b) => b.pressure - a.pressure || catalog.map.lanes.indexOf(a.lane) - catalog.map.lanes.indexOf(b.lane));
}
function repairCandidate(state, catalog, pressure) {
    if (state.animal.repairsLeft <= 0 || state.animal.repair || state.elapsedMs < state.animal.nextRepairMs) return null;
    return state.towers.filter(tower => tower.status === 'active' && tower.hp > 0 && tower.hp <= catalog.tower.hp * catalog.rules.repairThreshold)
        .sort((a, b) => pressure.findIndex(item => item.lane.id === a.laneId) - pressure.findIndex(item => item.lane.id === b.laneId) || a.hp - b.hp)[0];
}
function chooseNext(state, catalog, previous) {
    const pressure = pressureLanes(state, catalog), laneId = pressure[0].lane.id;
    if (previous.type === 'guard') {
        state.animal.intent = { type: 'sweep', laneId: previous.laneId, padId: null, seenTurn: state.turn + 1, area: sweepArea(catalog, previous.laneId) };
        record(state, catalog, 'event.warning', { laneId: previous.laneId });
        return;
    }
    const repair = repairCandidate(state, catalog, pressure);
    if (repair) {
        state.animal.intent = { type: 'repair', laneId: repair.laneId, padId: repair.id, seenTurn: state.turn + 1 };
        record(state, catalog, 'event.repairPlan', { towerId: repair.id });
        return;
    }
    const pad = catalog.map.pads.filter(item => laneDefinition(catalog, item.laneId).open && !state.towers.some(tower => tower.id === item.id))
        .sort((a, b) => pressure.findIndex(item => item.lane.id === a.laneId) - pressure.findIndex(item => item.lane.id === b.laneId) || a.at - b.at)[0];
    if (state.animal.buildsLeft > 0 && state.animal.actions + 1 >= catalog.rules.buildOnAnimalTurn && pad) {
        state.animal.intent = { type: 'build', laneId: pad.laneId, padId: pad.id, seenTurn: state.turn + 1 };
        record(state, catalog, 'event.build', { laneId: pad.laneId });
    } else {
        state.animal.intent = { type: 'guard', laneId, padId: null, seenTurn: state.turn };
    }
}
function sweep(state, catalog, intent) {
    const hitIds = [];
    for (const unit of state.units) {
        if (unit.hp > 0 && unitInArea(catalog, unit, intent.area)) {
            hitIds.push(unit.id);
            hitUnit(state, catalog, unit, catalog.rules.sweepDamage, catalog.rules.sweepPush);
        }
    }
    state.animal.guardUntilMs = state.elapsedMs;
    state.animal.recoveryUntilMs = state.elapsedMs + catalog.rules.sweepRecoveryMs;
    state.effects.push({ kind: 'sweep', area: { ...intent.area }, untilMs: state.elapsedMs + 650 });
    record(state, catalog, 'event.sweep', { laneId: intent.laneId, hitIds });
}
function guard(state, catalog, laneId) {
    state.animal.guardLane = laneId;
    state.animal.guardUntilMs = state.elapsedMs + catalog.rules.guardDurationMs;
    state.animal.nextSlapMs = state.elapsedMs;
    record(state, catalog, 'event.guard', { laneId });
}
function build(state, catalog, padId) {
    const pad = catalog.map.pads.find(item => item.id === padId);
    if (!pad || state.animal.buildsLeft <= 0 || state.towers.some(tower => tower.id === padId)) return;
    const distance = routeLength(laneDefinition(catalog, pad.laneId)) * pad.at;
    state.towers.push({ id: pad.id, laneId: pad.laneId, distance, hp: catalog.tower.hp, status: 'building',
        readyAtMs: state.elapsedMs + catalog.rules.buildDurationMs, nextShotMs: state.elapsedMs + catalog.rules.buildDurationMs });
    state.animal.buildsLeft--;
}
function startRepair(state, catalog, padId) {
    const tower = state.towers.find(item => item.id === padId);
    if (!tower || tower.status !== 'active' || tower.hp <= 0 || tower.hp >= catalog.tower.hp || state.animal.repairsLeft <= 0 || state.animal.repair || state.elapsedMs < state.animal.nextRepairMs) return;
    state.animal.repairsLeft--;
    state.animal.nextRepairMs = state.elapsedMs + catalog.rules.repairCooldownMs;
    state.animal.repair = { towerId: padId, readyAtMs: state.elapsedMs + catalog.rules.repairDurationMs };
    record(state, catalog, 'event.repairStart', { towerId: padId });
}
export function animalCanAct(state) {
    return state.elapsedMs >= state.animal.recoveryUntilMs && (state.animal.intent.type === 'guard' || state.resolvedTurn >= state.animal.intent.seenTurn);
}
export function actAnimal(state, catalog) {
    const intent = state.animal.intent;
    if (intent.type === 'guard') guard(state, catalog, intent.laneId);
    else if (intent.type === 'sweep') sweep(state, catalog, intent);
    else if (intent.type === 'repair') startRepair(state, catalog, intent.padId);
    else build(state, catalog, intent.padId);
    state.animal.actions++;
    state.animal.charge = 0;
    removeLost(state, catalog);
    chooseNext(state, catalog, intent);
}
export function updateAnimalState(state, catalog) {
    if (state.animal.vigilance !== 'high' || state.progress < catalog.rules.target * catalog.rules.relaxAtProgress) return;
    state.animal.vigilance = 'watchful';
    record(state, catalog, 'event.relax');
}
export const animalPeriodMs = (state, catalog) => animalDefinition(catalog, state.animal).periodMs;
