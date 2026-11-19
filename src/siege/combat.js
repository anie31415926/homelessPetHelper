import { unitDefinition, laneDefinition } from './catalog.js';
import { routeLength, positionOf, separation } from './geometry.js';
import { hitUnit, naturalMerges } from './entities.js';
import { record } from './supply.js';

function giveCover(state, catalog) {
    for (const unit of state.units) {
        const definition = unitDefinition(catalog, unit.definitionId);
        if (definition.ability !== 'aura' || unit.transit || state.elapsedMs < unit.nextAuraMs) continue;
        for (const ally of state.units) {
            if (ally.id !== unit.id && separation(positionOf(catalog, unit), positionOf(catalog, ally)) <= definition.abilityRadius) ally.shield = 1;
        }
        unit.nextAuraMs = state.elapsedMs + definition.abilityPeriodMs;
    }
}
function repairTower(state, catalog) {
    const repair = state.animal.repair;
    if (!repair) return;
    const tower = state.towers.find(item => item.id === repair.towerId);
    if (!tower || tower.status !== 'active' || tower.hp <= 0) {
        state.animal.repair = null;
        record(state, catalog, 'event.repairStopped', { towerId: repair.towerId });
    } else if (state.elapsedMs >= repair.readyAtMs) {
        tower.hp = Math.min(catalog.tower.hp, tower.hp + catalog.rules.repairAmount);
        state.stats.repairs++;
        state.animal.repair = null;
        state.effects.push({ kind: 'repair', point: positionOf(catalog, tower), untilMs: state.elapsedMs + 650 });
        record(state, catalog, 'event.repaired', { towerId: tower.id });
    }
}
function guardAttacks(state, catalog) {
    const animal = state.animal;
    if (!animal.guardLane || state.elapsedMs >= animal.guardUntilMs || state.elapsedMs < animal.nextSlapMs) return;
    const reach = routeLength(laneDefinition(catalog, animal.guardLane)) * catalog.rules.guardStart - catalog.rules.slapReach;
    const target = state.units.filter(unit => unit.hp > 0 && unit.laneId === animal.guardLane && !unit.transit && unit.distance >= reach)
        .sort((a, b) => b.distance - a.distance || a.id - b.id)[0];
    if (!target) return;
    state.effects.push({ kind: 'slap', point: positionOf(catalog, target), untilMs: state.elapsedMs + 350 });
    hitUnit(state, catalog, target, catalog.rules.slapDamage, catalog.rules.slapPush);
    animal.nextSlapMs = state.elapsedMs + catalog.rules.slapPeriodMs;
}
function moveAndAttack(state, catalog, dt) {
    for (const unit of state.units) {
        if (unit.hp <= 0) continue;
        const definition = unitDefinition(catalog, unit.definitionId);
        if (unit.transit) {
            unit.transit.remainingMs = Math.max(0, unit.transit.remainingMs - dt);
            if (unit.transit.remainingMs === 0) unit.transit = null;
            continue;
        }
        const lane = laneDefinition(catalog, unit.laneId), length = routeLength(lane);
        const target = state.towers.filter(tower => tower.laneId === unit.laneId && tower.status === 'active' && tower.hp > 0 && tower.distance >= unit.distance - definition.reach)
            .sort((a, b) => a.distance - b.distance)[0];
        const blockedByCat = state.animal.guardLane === unit.laneId && state.animal.guardUntilMs > state.elapsedMs;
        const towerLimit = target ? Math.max(0, target.distance - definition.reach * 0.65) : length;
        const limit = Math.min(towerLimit, blockedByCat ? length * catalog.rules.guardStart : length);
        unit.distance = Math.min(Math.max(unit.distance, limit), unit.distance + definition.speed * dt / 1000);
        if (target && target.distance - unit.distance <= definition.reach && state.elapsedMs >= unit.nextAttackMs) {
            target.hp -= definition.damage;
            unit.nextAttackMs = state.elapsedMs + definition.attackMs;
            state.effects.push({ kind: 'unitShot', from: positionOf(catalog, unit), to: positionOf(catalog, { laneId: target.laneId, distance: target.distance }), untilMs: state.elapsedMs + 160 });
            if (target.hp <= 0) {
                target.status = 'destroyed';
                state.stats.towers++;
                record(state, catalog, 'event.destroyed', { towerId: target.id });
            }
        }
        if (unit.distance >= length) {
            state.progress = Math.min(catalog.rules.target, state.progress + definition.gain);
            state.stats.delivered++;
            unit.arrived = true;
            record(state, catalog, 'event.deliver', { unitId: unit.id, gain: definition.gain });
        }
    }
    state.units = state.units.filter(unit => !unit.arrived);
}
function towersAttack(state, catalog) {
    for (const tower of state.towers) {
        if (tower.status === 'building' && state.elapsedMs >= tower.readyAtMs) tower.status = 'active';
        if (tower.status !== 'active' || state.elapsedMs < tower.nextShotMs) continue;
        const point = positionOf(catalog, { laneId: tower.laneId, distance: tower.distance });
        const target = state.units.filter(unit => unit.hp > 0 && separation(positionOf(catalog, unit), point) <= catalog.tower.range)
            .sort((a, b) => b.distance / routeLength(laneDefinition(catalog, b.laneId)) - a.distance / routeLength(laneDefinition(catalog, a.laneId)) || a.id - b.id)[0];
        if (!target) continue;
        hitUnit(state, catalog, target, catalog.tower.damage);
        tower.nextShotMs = state.elapsedMs + catalog.tower.periodMs;
        state.effects.push({ kind: 'towerShot', from: point, to: positionOf(catalog, target), untilMs: state.elapsedMs + 180 });
    }
}
export function removeLost(state, catalog) {
    const lost = state.units.filter(unit => unit.hp <= 0);
    for (const unit of lost) record(state, catalog, 'event.lost', { unitId: unit.id });
    state.stats.lost += lost.length;
    state.units = state.units.filter(unit => unit.hp > 0);
}
export function advanceCombat(state, catalog, dt) {
    state.effects = state.effects.filter(effect => effect.untilMs > state.elapsedMs);
    repairTower(state, catalog);
    giveCover(state, catalog);
    moveAndAttack(state, catalog, dt);
    towersAttack(state, catalog);
    guardAttacks(state, catalog);
    if (state.animal.repair && state.towers.find(item => item.id === state.animal.repair.towerId)?.status === 'destroyed') repairTower(state, catalog);
    removeLost(state, catalog);
    while (naturalMerges(state, catalog)) { /* Each merge removes two entities, so the loop is bounded. */ }
}
