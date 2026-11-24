using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Siege
{
    public sealed class SiegeCatalog
    {
        public SiegeRulesDefinition Rules { get; }
        public SiegeTowerDefinition Tower { get; }
        public IReadOnlyList<SiegeUnitDefinition> Units { get; }
        public IReadOnlyList<SiegeProfileDefinition> Profiles { get; }
        public IReadOnlyList<SiegeLane> Lanes { get; }
        public IReadOnlyList<SiegePadDefinition> Pads { get; }
        public SiegeContentCatalog Content { get; }
        private readonly SiegeTowerTypeDefinition _legacyTower;
        public double Width { get; }
        public double Height { get; }

        public SiegeCatalog(SiegeCatalogData data)
        {
            Require(
                data != null && data.schemaVersion == 1 && data.rules != null && data.tower != null && data.map != null,
                "header"
            );
            ValidateNumbers(data.rules);
            var r = data.rules;
            Require(
                r.stepMs > 0
                    && r.stepMs <= r.playerPeriodMs
                    && r.durationMs >= r.playerPeriodMs
                    && r.durationMs <= 3600000,
                "clock"
            );
            Require(
                r.handSize > 0 && r.handSize <= 20 && r.bagCopies > 0 && r.bagCopies <= 100 && r.maxLog > 0,
                "supply"
            );
            Require(
                r.energyCap > 0
                    && r.energyCap <= 10000
                    && r.openingEnergy >= 0
                    && r.openingEnergy <= r.energyCap
                    && r.turnEnergy > 0
                    && r.turnEnergy <= r.energyCap,
                "energy"
            );
            Require(
                r.turnDraw > 0 && r.turnDraw <= r.handSize && r.mergeCost > 0 && r.mergeCost <= r.energyCap,
                "cost"
            );
            Require(
                r.guardStart > 0
                    && r.guardStart < 1
                    && r.sweepCenterAt > 0
                    && r.sweepCenterAt < 1
                    && r.repairThreshold > 0
                    && r.repairThreshold < 1,
                "fractions"
            );
            Require(
                r.target > 0
                    && r.relaxAtProgress > 0
                    && r.relaxAtProgress <= 1
                    && r.naturalMergeMinDistance > r.mergeRadius
                    && r.mergeRadius > 0,
                "goal/merge"
            );
            Require(
                r.guardDurationMs > 0
                    && r.slapDamage > 0
                    && r.slapPeriodMs > 0
                    && r.slapReach > 0
                    && r.sweepDamage > 0
                    && r.sweepRadius > 0
                    && r.sweepRecoveryMs > 0,
                "animal"
            );
            Require(
                r.repairDurationMs > 0
                    && r.repairAmount > 0
                    && r.repairCooldownMs > 0
                    && r.animalRepairs >= 0
                    && r.animalBuilds >= 0
                    && r.buildDurationMs > 0
                    && r.buildOnAnimalTurn > 0
                    && r.rerouteDurationMs > 0,
                "actions"
            );
            Require(
                r.threatBase > 0
                    && r.threatNearWeight > 0
                    && r.threatExponent > 0
                    && r.slapPush >= 0
                    && r.sweepPush >= 0,
                "threat"
            );
            Require(data.rules.resourcePeriodMs >= data.rules.stepMs && data.rules.resourcePeriodMs <= 60000 &&
                data.rules.defenderResourcePeriodMs >= data.rules.stepMs &&
                data.rules.defenderResourcePeriodMs <= 60000 && data.rules.defenderEnergyCap > 0 &&
                data.rules.defenderEnergyCap <= 100 &&
                data.rules.defenderOpeningEnergy >= 0
                    && data.rules.defenderOpeningEnergy <= data.rules.defenderEnergyCap &&
                data.rules.defenderBuildCost > 0 && data.rules.defenderBuildCost <= data.rules.defenderEnergyCap &&
                data.rules.fastBuildDurationMs >= data.rules.stepMs && data.rules.fastBuildDurationMs <= 10000 &&
                data.rules.maxTroops >= data.rules.handSize && data.rules.maxTroops <= 128 &&
                Finite(data.rules.deploymentFraction) && data.rules.deploymentFraction > 0 &&
                data.rules.deploymentFraction < .5 && data.rules.defenseGridSize >= 2 &&
                data.rules.defenseGridSize <= 12, "realtime rules");
            Rules = new SiegeRulesDefinition(r);
            ValidateNumbers(data.tower);
            Require(
                data.tower.hp > 0 && data.tower.damage > 0 && data.tower.range > 0 && data.tower.periodMs > 0,
                "tower"
            );
            Tower = new SiegeTowerDefinition(data.tower);
            _legacyTower = new SiegeTowerTypeDefinition(new SiegeCatalogData.TowerTypeDto
            {
                id = "legacy", nameKey = "unity.tower", visual = "legacy", attack = "single",
                hp = data.tower.hp, damage = data.tower.damage, range = data.tower.range,
                periodMs = data.tower.periodMs, slowFactor = 1,
            });
            Unique(data.units?.Select(x => x?.id), "units");
            var allUnits = data.units.Concat(data.contentUnits ?? Array.Empty<SiegeCatalogData.UnitDto>()).ToArray();
            Unique(allUnits.Select(x => x?.id), "all units");
            Units = Array.AsReadOnly(allUnits.Select(x => new SiegeUnitDefinition(x)).ToArray());
            ValidateUnits(data);
            Content = new SiegeContentCatalog(data, this);
            Unique(data.animalStates?.Select(x => x?.id), "profiles");
            Profiles = Array.AsReadOnly(data.animalStates.Select(x => new SiegeProfileDefinition(x)).ToArray());
            Require(
                Profiles.Select(x => x.Vigilance + ":" + x.Stamina).Distinct().Count() == Profiles.Count,
                "duplicate conditions"
            );
            foreach (var p in Profiles)
            {
                Require(
                    p.PeriodMs > 0
                        && !string.IsNullOrWhiteSpace(p.NameKey)
                        && (p.Vigilance == "watchful" || p.Vigilance == "high")
                        && (p.Stamina == "normal" || p.Stamina == "tired")
                        && Profiles.Any(x => x.Vigilance == "watchful" && x.Stamina == p.Stamina),
                    "profile conditions"
                );
            }
            Width = data.map.width;
            Height = data.map.height;
            Require(Finite(Width) && Finite(Height) && Width > 0 && Height > 0, "map bounds");
            Unique(data.map.lanes?.Select(x => x?.id), "lanes");
            Lanes = Array.AsReadOnly(data.map.lanes.Select(x => new SiegeLane(x, Width, Height)).ToArray());
            Require(Lanes.Any(x => x.Open), "open lane");
            Unique(data.map.pads?.Select(x => x?.id), "pads");
            Pads = Array.AsReadOnly(data.map.pads.Select(x => new SiegePadDefinition(x)).ToArray());
            foreach (var pad in Pads)
                Require(
                    Lane(pad.LaneId) != null && Finite(pad.At) && pad.At > 0 && pad.At < r.guardStart,
                    "pad reference"
                );
        }

        public SiegeUnitDefinition Unit(string id)
        {
            for (int index = 0; index < Units.Count; index++) if (Units[index].Id == id) return Units[index];
            return null;
        }

        public int MergeMaterialCount(string unitId) => SiegeEntities.MergeCount(Unit(unitId));

        public SiegeTowerTypeDefinition Defense(SiegeState.Tower tower) =>
            tower.DefinitionId == null ? _legacyTower : Content.Tower(tower.DefinitionId);

        public SiegeLane Lane(string id)
        {
            for (int index = 0; index < Lanes.Count; index++) if (Lanes[index].Id == id) return Lanes[index];
            return null;
        }

        public SiegeProfileDefinition Profile(string id)
        {
            foreach (var profile in Profiles) if (profile.Id == id) return profile;
            return null;
        }

        public SiegeProfileDefinition Conditions(string vigilance, string stamina)
        {
            for (int index = 0; index < Profiles.Count; index++)
                if (Profiles[index].Vigilance == vigilance && Profiles[index].Stamina == stamina)
                    return Profiles[index];
            throw new ArgumentException("Unknown animal conditions.");
        }

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static void Require(bool valid, string name)
        {
            if (!valid)
                throw new ArgumentException("Invalid siege configuration: " + name);
        }

        private void ValidateUnits(SiegeCatalogData data)
        {
            foreach (var u in data.units.Concat(data.contentUnits ?? Array.Empty<SiegeCatalogData.UnitDto>()))
            {
                ValidateNumbers(u);
                Require(
                    u.hp > 0
                        && u.speed > 0
                        && u.damage > 0
                        && u.attackMs > 0
                        && u.reach > 0
                        && u.gain > 0
                        && u.cost >= 0
                        && u.cost <= Rules.EnergyCap,
                    "unit numbers"
                );
                Require(new[] { "none", "aura", "brace", "reroute", "burst", "braceAura",
                    "mend", "interfere", "impact" }.Contains(u.ability), "ability");
                Require(!string.IsNullOrWhiteSpace(u.nameKey) && !string.IsNullOrWhiteSpace(u.detailKey), "unit text");
                Require(
                    string.IsNullOrEmpty(u.upgradeId)
                        || (Unit(u.upgradeId) != null && !Unit(u.upgradeId).Bag && u.upgradeId != u.id),
                    "upgrade"
                );
                Require(!u.bag || !string.IsNullOrEmpty(u.upgradeId), "base upgrade");
                Require(u.mergeCount == 0 || (u.mergeCount >= 2 && u.mergeCount <= 3
                    && !string.IsNullOrEmpty(u.upgradeId)), "merge count");
                Require(SiegeUnitAbilities.Valid(u), "unit ability parameters");
                Require(u.ability != "aura" || (u.abilityRadius > 0 && u.abilityPeriodMs > 0), "aura");
                Require(u.ability != "burst" || (u.burstHits >= 2 && u.burstHits <= 5
                    && u.burstIntervalMs > 0), "burst");
                Require(u.ability != "braceAura" || (u.abilityRadius > 0 && u.auraTargets > 0
                    && u.auraTargets <= 4 && u.auraRearmMs > 0), "brace aura");
            }
            Require(Units.Any(x => x.Bag && x.Cost > 0), "bag empty");
        }

        private static void Unique(IEnumerable<string> source, string name)
        {
            Require(source != null, name);
            string[] ids = source.ToArray();
            Require(
                ids.Length > 0 && ids.All(x => !string.IsNullOrWhiteSpace(x)) && ids.Distinct().Count() == ids.Length,
                name
            );
        }

        private static void ValidateNumbers(object data)
        {
            foreach (var field in data.GetType().GetFields())
            {
                if (field.FieldType == typeof(double))
                    Require(Finite((double)field.GetValue(data)), field.Name);
            }
        }
    }
}
