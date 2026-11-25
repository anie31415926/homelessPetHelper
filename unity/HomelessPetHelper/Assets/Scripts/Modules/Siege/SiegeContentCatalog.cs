using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege
{
    public sealed class SiegeContentCatalog
    {
        public IReadOnlyList<SiegeTowerTypeDefinition> Towers { get; }
        public IReadOnlyList<SiegeEncounterDefinition> Encounters { get; }

        internal SiegeContentCatalog(SiegeCatalogData data, SiegeCatalog catalog)
        {
            var towers = data.towerTypes ?? Array.Empty<SiegeCatalogData.TowerTypeDto>();
            var encounters = data.encounters ?? Array.Empty<SiegeCatalogData.EncounterDto>();
            SiegeCatalog.Require(towers.All(x => x != null && !string.IsNullOrWhiteSpace(x.id))
                && towers.Select(x => x.id).Distinct().Count() == towers.Length, "tower type ids");
            foreach (var t in towers)
            {
                SiegeCatalog.Require(!string.IsNullOrWhiteSpace(t.nameKey)
                    && new[] { "single", "slow", "push" }.Contains(t.attack), "tower type");
                SiegeCatalog.Require(Positive(t.hp) && Positive(t.range) && t.periodMs > 0
                    && NonNegative(t.damage) && NonNegative(t.push) && NonNegative(t.radius)
                    && t.warningMs >= 0 && t.zoneDurationMs >= 0
                    && Positive(t.slowFactor) && t.slowFactor <= 1, "tower type numbers");
                SiegeCatalog.Require(t.attack != "slow" || (t.slowFactor < 1
                    && t.zoneDurationMs > 0 && t.radius > 0), "slow zone");
                SiegeCatalog.Require(t.attack != "push" || (t.warningMs > 0
                    && t.push > 0 && t.radius > 0), "push warning");
            }
            Towers = Array.AsReadOnly(towers.Select(x => new SiegeTowerTypeDefinition(x)).ToArray());
            SiegeCatalog.Require(encounters.All(x => x != null && !string.IsNullOrWhiteSpace(x.id))
                && encounters.Select(x => x.id).Distinct().Count() == encounters.Length, "encounter ids");
            foreach (var e in encounters)
            {
                SiegeCatalog.Require(!string.IsNullOrWhiteSpace(e.nameKey)
                    && !string.IsNullOrWhiteSpace(e.animalVisual)
                    && !string.IsNullOrWhiteSpace(e.guardNameKey)
                    && !string.IsNullOrWhiteSpace(e.sweepNameKey), "encounter visual");
                SiegeCatalog.Require(e.unitIds != null && e.unitIds.Length == (e.rotateSupply ? 5 : 3)
                    && e.unitIds.Distinct().Count() == e.unitIds.Length
                    && e.unitIds.All(id => catalog.Unit(id) != null && catalog.Unit(id).Cost > 0
                        && catalog.Unit(id).Cost <= catalog.Rules.EnergyCap), "encounter supply");
                SiegeCatalog.Require(e.initialTowerIds != null && e.initialTowerIds.Length > 0
                    && e.buildTowerIds != null && e.buildTowerIds.Length > 0
                    && e.initialTowerIds.Concat(e.buildTowerIds).All(id => Tower(id) != null), "encounter towers");
                SiegeCatalog.Require(e.sweepDelayMs >= 0 && e.sweepDelayMs <= 2000, "sweep delay");
                ValidateHold(e);
                ValidateCarry(e);
            }
            Encounters = Array.AsReadOnly(encounters.Select(x => new SiegeEncounterDefinition(x)).ToArray());
        }

        private static void ValidateHold(SiegeCatalogData.EncounterDto e)
        {
            SiegeCatalog.Require(e.holdDurationMs >= 0 && e.holdDurationMs <= 60000
                && e.holdRecoveryMs >= 0 && e.holdRecoveryMs <= 60000
                && e.holdCooldownMs >= 0 && e.holdCooldownMs <= 60000
                && e.holdProtectionMs >= 0 && e.holdProtectionMs <= 60000
                && NonNegative(e.holdRange) && SiegeCatalog.Finite(e.holdOriginX)
                && SiegeCatalog.Finite(e.holdOriginY), "hold numbers");
            SiegeCatalog.Require(e.holdDurationMs == 0 || (!string.IsNullOrWhiteSpace(e.holdNameKey)
                && e.holdRecoveryMs > 0 && e.holdProtectionMs > 0 && e.holdRange > 0
                && e.holdCooldownMs >= e.holdDurationMs + e.holdRecoveryMs), "hold enabled");
        }

        public SiegeTowerTypeDefinition Tower(string id)
        {
            for (int index = 0; index < Towers.Count; index++) if (Towers[index].Id == id) return Towers[index];
            return null;
        }

        private static void ValidateCarry(SiegeCatalogData.EncounterDto e)
        {
            SiegeCatalog.Require(e.carryDelayMs >= 0 && e.carryDelayMs <= 60000
                && e.carryDurationMs >= 0 && e.carryDurationMs <= 60000
                && e.carryRecoveryMs >= 0 && e.carryRecoveryMs <= 60000
                && e.carryCooldownMs >= 0 && e.carryCooldownMs <= 60000
                && e.carryProtectionMs >= 0 && e.carryProtectionMs <= 60000
                && NonNegative(e.carryRange) && NonNegative(e.carryDistance)
                && NonNegative(e.carryMinDistance) && SiegeCatalog.Finite(e.carryOriginX)
                && SiegeCatalog.Finite(e.carryOriginY), "carry numbers");
            SiegeCatalog.Require(e.carryDurationMs == 0 || (!string.IsNullOrWhiteSpace(e.carryNameKey)
                && e.carryRecoveryMs > 0 && e.carryProtectionMs > 0 && e.carryRange > 0
                && e.carryDistance > 0 && e.carryMinDistance > 0
                && e.carryCooldownMs >= e.carryDelayMs + e.carryDurationMs + e.carryRecoveryMs), "carry enabled");
        }

        public SiegeEncounterDefinition Encounter(string id)
        {
            for (int index = 0; index < Encounters.Count;
                index++) if (Encounters[index].Id == id) return Encounters[index];
            return null;
        }

        private static bool Positive(double value) => SiegeCatalog.Finite(value) && value > 0;

        private static bool NonNegative(double value) => SiegeCatalog.Finite(value) && value >= 0;
    }
}
