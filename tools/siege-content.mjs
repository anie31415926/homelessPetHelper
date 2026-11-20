import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

export const contentPath = new URL('../src/siege/content.json', import.meta.url);
export const copyPath = new URL('../src/siege/content-text.zh-CN.json', import.meta.url);
export const CONTENT = JSON.parse(fs.readFileSync(contentPath, 'utf8'));
export const CONTENT_TEXT = JSON.parse(fs.readFileSync(copyPath, 'utf8'));
export const UNIT_DEFAULTS = { burstHits: 0, burstIntervalMs: 0, auraTargets: 0, auraRearmMs: 0 };

export function validateContent(source, text) {
    const require = (test, field) => { if (!test) throw new Error(`Siege content: ${field}`); };
    const unique = (items, label) => {
        require(Array.isArray(items) && items.length > 0, label);
        require(items.every(x => x && typeof x.id === 'string' && x.id.length > 0), `${label} ids`);
        require(new Set(items.map(x => x.id)).size === items.length, `${label} duplicate`);
    };
    require(source.schemaVersion === 1, 'schema');
    for (const key of ['units', 'towers', 'encounters']) unique(source[key], key);
    const units = new Map(source.units.map(x => [x.id, x]));
    const towers = new Map(source.towers.map(x => [x.id, x]));
    const abilities = new Set(['none', 'aura', 'brace', 'reroute', 'burst', 'braceAura']);
    for (const u of source.units) {
        require(abilities.has(u.ability) && u.bag === false, `unit ${u.id}`);
        require(text[u.nameKey] && text[u.detailKey], `unit text ${u.id}`);
        for (const key of ['hp', 'speed', 'damage', 'attackMs', 'reach', 'gain'])
            require(Number.isFinite(u[key]) && u[key] > 0, `unit ${u.id}.${key}`);
        require(Number.isInteger(u.cost) && u.cost >= 0, `cost ${u.id}`);
        require(u.upgradeId === null || units.has(u.upgradeId) && u.upgradeId !== u.id
            && units.get(u.upgradeId).upgradeId === null, `upgrade ${u.id}`);
        if (u.ability === 'aura') require(u.abilityRadius > 0 && u.abilityPeriodMs > 0, `aura ${u.id}`);
        if (u.ability === 'burst') require(Number.isInteger(u.burstHits) && u.burstHits >= 2
            && u.burstHits <= 5 && Number.isInteger(u.burstIntervalMs) && u.burstIntervalMs > 0, `burst ${u.id}`);
        if (u.ability === 'braceAura') require(u.abilityRadius > 0 && u.auraTargets > 0
            && u.auraTargets <= 4 && Number.isInteger(u.auraTargets)
            && Number.isInteger(u.auraRearmMs) && u.auraRearmMs > 0, `braceAura ${u.id}`);
    }
    for (const t of source.towers) {
        require(text[t.nameKey] && ['single', 'slow', 'push'].includes(t.attack), `tower ${t.id}`);
        for (const key of ['hp', 'range', 'periodMs']) require(Number.isFinite(t[key]) && t[key] > 0, `tower ${key}`);
        for (const key of ['damage', 'warningMs', 'radius', 'push', 'zoneDurationMs'])
            require(Number.isFinite(t[key]) && t[key] >= 0, `tower ${key}`);
        require(Number.isFinite(t.slowFactor) && t.slowFactor > 0 && t.slowFactor <= 1, `slow ${t.id}`);
        if (t.attack === 'push') require(t.warningMs > 0 && t.radius > 0 && t.push > 0, `push ${t.id}`);
        if (t.attack === 'slow') require(t.radius > 0 && t.zoneDurationMs > 0 && t.slowFactor < 1, `zone ${t.id}`);
    }
    for (const e of source.encounters) {
        require(text[e.nameKey] && typeof e.animalVisual === 'string' && e.animalVisual.length > 0
            && typeof e.guardNameKey === 'string' && typeof e.sweepNameKey === 'string', `encounter ${e.id}`);
        require(Array.isArray(e.unitIds) && e.unitIds.length === 3 && new Set(e.unitIds).size === 3
            && e.unitIds.every(id => units.get(id)?.upgradeId && units.get(id).cost > 0), `supply ${e.id}`);
        require(e.initialTowerIds.length > 0 && e.buildTowerIds.length > 0
            && [...e.initialTowerIds, ...e.buildTowerIds].every(id => towers.has(id)), `towers ${e.id}`);
        require(Number.isInteger(e.sweepDelayMs) && e.sweepDelayMs >= 0 && e.sweepDelayMs <= 2000, `impact ${e.id}`);
        for (const key of ['holdDurationMs', 'holdRecoveryMs', 'holdCooldownMs', 'holdProtectionMs'])
            require(Number.isInteger(e[key]) && e[key] >= 0 && e[key] <= 60000, `hold ${e.id}.${key}`);
        require(Number.isFinite(e.holdOriginX) && Number.isFinite(e.holdOriginY)
            && Number.isFinite(e.holdRange) && e.holdRange >= 0, `hold geometry ${e.id}`);
        if (e.holdDurationMs > 0) require(text[e.holdNameKey] && e.holdRecoveryMs > 0
            && e.holdCooldownMs >= e.holdDurationMs + e.holdRecoveryMs
            && e.holdProtectionMs > 0 && e.holdRange > 0, `hold enabled ${e.id}`);
    }
    return source;
}

validateContent(CONTENT, CONTENT_TEXT);
if (process.argv[1] === fileURLToPath(import.meta.url)) console.log('Siege content valid: 12 units, 6 towers, 2 encounters.');
