import data from './definitions.json' with { type: 'json' };
import text from './text.zh-CN.json' with { type: 'json' };

const abilities = new Set(['none', 'brace', 'reroute', 'aura']);
const icons = new Set(['truck', 'circle-dot', 'umbrella', 'shield', 'route']);
function requireValue(test, message) {
    if (!test) throw new Error(`Siege configuration: ${message}`);
}
function unique(items, label) {
    requireValue(Array.isArray(items) && items.length > 0, label);
    requireValue(items.every(item => typeof item.id === 'string' && item.id.length > 0), `${label} ids`);
    requireValue(new Set(items.map(item => item.id)).size === items.length, `duplicate ${label}`);
}
function positive(value) { return Number.isFinite(value) && value > 0; }
export function validateCatalog(source, texts) {
    requireValue(source.schemaVersion === 1, 'schemaVersion');
    const r = source.rules;
    requireValue(r && Object.values(r).every(Number.isFinite), 'numeric rules');
    for (const key of ['stepMs', 'durationMs', 'playerPeriodMs', 'handSize', 'bagCopies', 'energyCap', 'turnEnergy', 'turnDraw', 'mergeCost', 'maxLog', 'buildDurationMs', 'buildOnAnimalTurn', 'rerouteDurationMs']) {
        requireValue(Number.isInteger(r[key]) && r[key] > 0, key);
    }
    requireValue(r.stepMs <= r.playerPeriodMs && r.durationMs >= r.playerPeriodMs, 'clock ranges');
    for (const key of ['guardDurationMs', 'slapDamage', 'slapPeriodMs', 'slapReach', 'sweepDamage', 'sweepRadius', 'sweepRecoveryMs', 'repairAmount', 'repairDurationMs', 'repairCooldownMs', 'threatBase', 'threatNearWeight', 'threatExponent']) requireValue(positive(r[key]), key);
    for (const key of ['slapPush', 'sweepPush']) requireValue(Number.isFinite(r[key]) && r[key] >= 0, key);
    requireValue(Number.isInteger(r.openingEnergy) && r.openingEnergy >= 0 && r.openingEnergy <= r.energyCap, 'opening energy');
    requireValue(r.turnDraw <= r.handSize && r.mergeCost <= r.energyCap, 'supply limits');
    requireValue(r.guardStart > 0 && r.guardStart < 1 && r.sweepCenterAt > 0 && r.sweepCenterAt < 1, 'animal reach');
    requireValue(r.repairThreshold > 0 && r.repairThreshold < 1, 'repair threshold');
    requireValue(Number.isInteger(r.animalRepairs) && r.animalRepairs >= 0, 'repair count');
    requireValue(r.relaxAtProgress > 0 && r.relaxAtProgress <= 1 && r.target > 0, 'goal');
    requireValue(r.mergeRadius > 0 && r.naturalMergeMinDistance > r.mergeRadius, 'merge ranges');
    requireValue(Number.isInteger(r.animalBuilds) && r.animalBuilds >= 0, 'build count');
    unique(source.units, 'units'); unique(source.animalStates, 'animal states');
    unique(source.map.lanes, 'lanes'); unique(source.map.pads, 'pads');
    const units = Object.fromEntries(source.units.map(unit => [unit.id, unit]));
    for (const unit of source.units) {
        requireValue(abilities.has(unit.ability) && typeof unit.bag === 'boolean', `ability ${unit.id}`);
        requireValue(icons.has(unit.icon), `icon ${unit.id}`);
        for (const key of ['speed', 'hp', 'damage', 'attackMs', 'reach', 'gain']) requireValue(positive(unit[key]), `${unit.id}.${key}`);
        requireValue(Number.isInteger(unit.cost) && unit.cost >= 0 && unit.cost <= r.energyCap, `cost ${unit.id}`);
        requireValue(texts[unit.nameKey] && texts[unit.detailKey], `unit text ${unit.id}`);
        requireValue(unit.upgradeId === null || units[unit.upgradeId] && !units[unit.upgradeId].bag && unit.upgradeId !== unit.id, `upgrade ${unit.id}`);
        requireValue(!unit.bag || unit.upgradeId !== null, `base upgrade ${unit.id}`);
        requireValue(unit.ability !== 'aura' || positive(unit.abilityRadius) && positive(unit.abilityPeriodMs), `aura ${unit.id}`);
    }
    requireValue(source.units.some(unit => unit.bag && unit.cost > 0), 'draw pool');
    requireValue(new Set(source.animalStates.map(state => `${state.vigilance}:${state.stamina}`)).size === source.animalStates.length, 'duplicate animal conditions');
    for (const state of source.animalStates) {
        requireValue(positive(state.periodMs) && texts[state.nameKey], `animal ${state.id}`);
        requireValue(source.animalStates.some(other => other.vigilance === 'watchful' && other.stamina === state.stamina), `relaxed state ${state.id}`);
    }
    requireValue(source.animalStates.some(state => state.vigilance === 'watchful' && state.stamina === 'normal'), 'relaxed state');
    const lanes = new Set(source.map.lanes.map(lane => lane.id));
    requireValue(source.map.lanes.some(lane => lane.open), 'open route');
    requireValue(positive(source.map.width) && positive(source.map.height), 'map bounds');
    for (const lane of source.map.lanes) {
        requireValue(typeof lane.open === 'boolean' && texts[lane.nameKey] && lane.points.length >= 2, `lane ${lane.id}`);
        lane.points.forEach((point, index) => {
            requireValue(point.length === 2 && point.every(Number.isFinite) && point[0] >= 0 && point[0] <= source.map.width && point[1] >= 0 && point[1] <= source.map.height, `point ${lane.id}`);
            if (index) requireValue(Math.hypot(point[0] - lane.points[index - 1][0], point[1] - lane.points[index - 1][1]) > 0, `zero segment ${lane.id}`);
        });
    }
    for (const pad of source.map.pads) requireValue(lanes.has(pad.laneId) && pad.at > 0 && pad.at < r.guardStart && typeof pad.initial === 'boolean', `pad ${pad.id}`);
    requireValue(Object.values(source.tower).every(positive), 'tower');
    return source;
}
function freeze(value) {
    Object.freeze(value);
    Object.values(value).forEach(item => { if (item && typeof item === 'object' && !Object.isFrozen(item)) freeze(item); });
    return value;
}
export const CATALOG = freeze(validateCatalog(data, text));
export const TEXT = freeze(text);
export const t = key => TEXT[key] ?? key;
export const unitDefinition = (catalog, id) => catalog.units.find(unit => unit.id === id);
export const laneDefinition = (catalog, id) => catalog.map.lanes.find(lane => lane.id === id);
export function animalDefinition(catalog, animal) {
    return catalog.animalStates.find(item => item.vigilance === animal.vigilance && item.stamina === animal.stamina);
}
