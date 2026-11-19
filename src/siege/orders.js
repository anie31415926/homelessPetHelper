import { unitDefinition, laneDefinition } from './catalog.js';
import { spawnUnit, mergeUnits, startReroute } from './entities.js';
import { record } from './supply.js';

const fail = (state, reason) => ({ ok: false, state, reason });
function mergeReason(state, catalog, merge) {
    if (!Array.isArray(merge.materialIds) || merge.materialIds.length !== 3 || new Set(merge.materialIds).size !== 3) return 'reason.merge';
    const materials = merge.materialIds.map(id => state.units.find(unit => unit.id === id));
    if (materials.some(unit => !unit || unit.hp <= 0 || unit.transit)) return 'reason.merge';
    if (!merge.materialIds.includes(merge.anchorId)) return 'reason.anchor';
    const definition = unitDefinition(catalog, materials[0].definitionId);
    if (!definition.upgradeId || materials.some(unit => unit.definitionId !== definition.id)) return 'reason.merge';
    return null;
}
export function evaluateOrder(state, catalog, order) {
    if (state.phase !== 'decision' || state.paused) return fail(state, 'reason.phase');
    if (!order || order.sessionId !== state.sessionId || order.turn !== state.turn) return fail(state, 'reason.stale');
    if (Object.keys(order).some(key => !['sessionId','turn','deploy','merge','reroute','discardId'].includes(key))) return fail(state, 'reason.order');
    if (order.merge && order.reroute) return fail(state, 'reason.order');
    if (order.discardId != null && (order.deploy || order.merge || order.reroute)) return fail(state, 'reason.discard');
    const next = structuredClone(state);
    let cost = 0, spawned = null;
    if (order.discardId != null) {
        const card = next.hand.find(item => item.instanceId === order.discardId);
        if (!card) return fail(state, 'reason.card');
        next.hand = next.hand.filter(item => item !== card);
        record(next, catalog, 'event.discard');
    }
    if (order.deploy) {
        const card = next.hand.find(item => item.instanceId === order.deploy.instanceId);
        if (!card) return fail(state, 'reason.card');
        if (!laneDefinition(catalog, order.deploy.laneId)?.open) return fail(state, 'reason.lane');
        cost += unitDefinition(catalog, card.definitionId).cost;
        next.hand = next.hand.filter(item => item !== card);
        spawned = spawnUnit(next, catalog, card.definitionId, order.deploy.laneId);
        next.stats.deployed++;
        record(next, catalog, 'event.deploy', { unitId: spawned.id, laneId: spawned.laneId });
    }
    if (order.merge) {
        const merge = { anchorId: order.merge.anchorId === 'new' ? spawned?.id : order.merge.anchorId,
            materialIds: Array.isArray(order.merge.materialIds) ? order.merge.materialIds.map(id => id === 'new' ? spawned?.id : id) : null };
        const reason = mergeReason(next, catalog, merge);
        if (reason) return fail(state, reason);
        cost += catalog.rules.mergeCost;
        mergeUnits(next, catalog, merge.materialIds, merge.anchorId);
    }
    if (order.reroute) {
        const unit = next.units.find(item => item.id === order.reroute.unitId);
        if (!unit || unit.reroutes < 1 || unit.transit || order.reroute.laneId === unit.laneId) return fail(state, 'reason.reroute');
        if (!laneDefinition(catalog, order.reroute.laneId)?.open) return fail(state, 'reason.lane');
        startReroute(next, catalog, unit, order.reroute.laneId);
    }
    if (cost > state.energy) return fail(state, 'reason.energy');
    next.energy -= cost;
    return { ok: true, state: next, cost, reason: null };
}
export function commitOrder(state, catalog, order) {
    const result = evaluateOrder(state, catalog, order);
    if (!result.ok) return result;
    const next = result.state;
    next.resolvedTurn = state.turn;
    next.phase = 'running';
    next.playerChargeMs = 0;
    next.accumulatorMs = 0;
    return result;
}
