import test from 'node:test';
import assert from 'node:assert/strict';
import { CONTENT, CONTENT_TEXT, validateContent } from '../tools/siege-content.mjs';

test('two explicit three-family supplies keep upgrades out of the draw pool', () => {
    assert.equal(validateContent(CONTENT, CONTENT_TEXT), CONTENT);
    assert.equal(CONTENT.units.length, 12);
    for (const e of CONTENT.encounters) {
        assert.equal(e.unitIds.length, 3);
        const units = CONTENT.units.filter(x => e.unitIds.includes(x.id));
        assert.ok(units.every(x => x.upgradeId));
        assert.ok(units.every(x => !e.unitIds.includes(x.upgradeId)));
    }
});

for (const [name, mutate] of [
    ['duplicate supply', c => { c.encounters[0].unitIds[1] = c.encounters[0].unitIds[0]; }],
    ['missing upgrade', c => { c.units[0].upgradeId = 'absent'; }],
    ['missing tower', c => { c.encounters[0].buildTowerIds[0] = 'absent'; }],
    ['untelegraphed push', c => { c.towers.find(x => x.attack === 'push').warningMs = 0; }],
    ['non-finite slow', c => { c.towers[1].slowFactor = NaN; }],
    ['infinite burst', c => { c.units.find(x => x.ability === 'burst').burstHits = Infinity; }],
    ['missing text', c => { c.units[0].nameKey = 'absent'; }],
    ['hold without recovery', c => { c.encounters[0].holdRecoveryMs = 0; }],
    ['hold without immunity', c => { c.encounters[0].holdProtectionMs = 0; }],
    ['non-finite hold reach', c => { c.encounters[0].holdRange = Infinity; }],
    ['hold without cooldown', c => { c.encounters[0].holdCooldownMs = 0; }],
    ['carry without recovery', c => { c.encounters[1].carryRecoveryMs = 0; }],
    ['carry without protection', c => { c.encounters[1].carryProtectionMs = 0; }],
    ['non-finite carry range', c => { c.encounters[1].carryRange = NaN; }],
    ['negative carry distance', c => { c.encounters[1].carryDistance = -1; }],
    ['carry into spawn', c => { c.encounters[1].carryMinDistance = 0; }],
    ['fractional carry duration', c => { c.encounters[1].carryDurationMs = 1.5; }],
    ['carry cooldown before landing', c => { c.encounters[1].carryCooldownMs = 2000; }],
]) test(`rejects ${name}`, () => {
    const source = structuredClone(CONTENT);
    mutate(source);
    assert.throws(() => validateContent(source, CONTENT_TEXT), /Siege content/);
});
