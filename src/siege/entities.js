import { unitDefinition, laneDefinition } from './catalog.js';
import { pointOnRoute, routeLength, positionOf, separation, projectOntoRoute } from './geometry.js';
import { record } from './supply.js';

export function spawnUnit(state, catalog, definitionId, laneId) {
    const definition = unitDefinition(catalog, definitionId);
    const unit = { id: ++state.serial, definitionId, laneId, distance: 0, hp: definition.hp,
        nextAttackMs: state.elapsedMs, nextAuraMs: state.elapsedMs, shield: 0,
        brace: definition.ability === 'brace' ? 1 : 0, reroutes: definition.ability === 'reroute' ? 1 : 0, transit: null };
    state.units.push(unit);
    return unit;
}
export function mergeUnits(state, catalog, ids, anchorId, natural = false) {
    const materials = ids.map(id => state.units.find(unit => unit.id === id));
    const anchor = materials.find(unit => unit.id === anchorId);
    const definition = unitDefinition(catalog, anchor.definitionId);
    const fraction = materials.reduce((sum, unit) => sum + unit.hp, 0) / (definition.hp * materials.length);
    const upgrade = unitDefinition(catalog, definition.upgradeId);
    const distance = anchor.distance, laneId = anchor.laneId;
    state.units = state.units.filter(unit => !ids.includes(unit.id));
    const result = spawnUnit(state, catalog, upgrade.id, laneId);
    result.distance = distance;
    result.hp = Math.max(0.01, upgrade.hp * fraction);
    state.effects.push({ kind: 'merge', point: positionOf(catalog, result), untilMs: state.elapsedMs + 650 });
    state.stats.merges++;
    record(state, catalog, natural ? 'event.natural' : 'event.merge', { unitId: result.id, materialIds: ids });
    return result;
}
export function naturalMerges(state, catalog) {
    for (const anchor of [...state.units]) {
        const definition = unitDefinition(catalog, anchor.definitionId);
        if (!definition.upgradeId || anchor.transit || anchor.distance < catalog.rules.naturalMergeMinDistance) continue;
        const candidates = state.units.filter(unit => unit.definitionId === anchor.definitionId && unit.laneId === anchor.laneId && !unit.transit &&
            unit.distance >= catalog.rules.naturalMergeMinDistance && separation(positionOf(catalog, unit), positionOf(catalog, anchor)) <= catalog.rules.mergeRadius);
        const pair = candidates.find((unit, i) => candidates.slice(i + 1).some(other => unit.id !== anchor.id && other.id !== anchor.id && separation(positionOf(catalog, unit), positionOf(catalog, other)) <= catalog.rules.mergeRadius));
        if (!pair) continue;
        const other = candidates.find(unit => unit.id !== anchor.id && unit.id !== pair.id && separation(positionOf(catalog, unit), positionOf(catalog, pair)) <= catalog.rules.mergeRadius);
        if (!other) continue;
        mergeUnits(state, catalog, [anchor.id, pair.id, other.id], anchor.id, true);
        return true;
    }
    return false;
}
export function hitUnit(state, catalog, unit, damage, push = 0) {
    if (unit.shield > 0) unit.shield--;
    else unit.hp -= damage;
    if (push && unit.brace > 0) unit.brace--;
    else if (push && !unit.transit) unit.distance = Math.max(0, unit.distance - push);
}
export function startReroute(state, catalog, unit, laneId) {
    const from = positionOf(catalog, unit), lane = laneDefinition(catalog, laneId);
    unit.distance = Math.min(routeLength(lane) * 0.97, projectOntoRoute(lane, from));
    unit.laneId = laneId;
    unit.transit = { from, to: pointOnRoute(lane, unit.distance), remainingMs: catalog.rules.rerouteDurationMs };
    unit.reroutes--;
    record(state, catalog, 'event.reroute', { unitId: unit.id });
}
