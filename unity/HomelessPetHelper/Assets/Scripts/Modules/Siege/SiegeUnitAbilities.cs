using System;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    /// <summary>Ability rules depend on the configured behavior and its parameters, never the card ID.</summary>
    internal static class SiegeUnitAbilities
    {
        internal static bool Valid(SiegeCatalogData.UnitDto unit)
        {
            if (unit.ability == "interfere")
                return unit.abilityCharges == 1 && unit.abilityPeriodMs > 0
                    && unit.abilityCooldownMs > unit.abilityPeriodMs;
            if (unit.ability == "impact")
                return unit.abilityCharges == 1 && SiegeCatalog.Finite(unit.abilityPower) && unit.abilityPower > 0;
            if (unit.ability == "mend")
                return unit.abilityCharges > 0 && unit.abilityPeriodMs > 0 && unit.abilityRadius > 0
                    && SiegeCatalog.Finite(unit.abilityPower) && unit.abilityPower > 0;
            return unit.abilityCharges == 0;
        }

        internal static double OnTowerHit(SiegeState state, SiegeCatalog catalog, Unit unit, Tower tower,
            SiegeUnitDefinition definition)
        {
            if (unit.AbilityCharges <= 0) return 0;
            if (definition.Ability == "impact")
            {
                unit.AbilityCharges--;
                Signal(state, catalog, unit, SiegeGeometry.Position(catalog, tower), definition.Ability);
                return definition.AbilityPower;
            }
            if (definition.Ability != "interfere" || tower.WindingUp ||
                tower.InterferenceReadyMs > state.ElapsedMs || tower.Status != TowerStatus.Active) return 0;

            unit.AbilityCharges--;
            tower.NextShotMs = Math.Max(tower.NextShotMs, state.ElapsedMs) + definition.AbilityPeriodMs;
            tower.InterferenceReadyMs = state.ElapsedMs + definition.AbilityCooldownMs;
            Signal(state, catalog, unit, SiegeGeometry.Position(catalog, tower), definition.Ability);
            return 0;
        }

        internal static void MendAllies(SiegeState state, SiegeCatalog catalog)
        {
            foreach (var unit in state.Troops)
            {
                if (unit.AbilityCharges <= 0 || !CanParticipate(unit) || unit.NextAuraMs > state.ElapsedMs)
                    continue;
                var definition = catalog.Unit(unit.DefinitionId);
                if (definition.Ability != "mend") continue;

                var origin = SiegeEntities.Position(catalog, unit);
                Unit target = null;
                double nearest = definition.AbilityRadius;
                foreach (var ally in state.Troops)
                {
                    if (ally == unit || !CanParticipate(ally) || catalog.Unit(ally.DefinitionId).Hp - ally.Hp < 1)
                        continue;
                    var distance = origin.Distance(SiegeEntities.Position(catalog, ally));
                    if (distance > nearest || (distance == nearest && target != null && ally.Id >= target.Id))
                        continue;
                    nearest = distance;
                    target = ally;
                }
                if (target == null)
                {
                    // No wounded ally nearby: defer the search instead of scanning the roster every tick.
                    unit.NextAuraMs = state.ElapsedMs + 250;
                    continue;
                }

                target.Hp = Math.Min(catalog.Unit(target.DefinitionId).Hp, target.Hp + definition.AbilityPower);
                unit.AbilityCharges--;
                unit.NextAuraMs = state.ElapsedMs + definition.AbilityPeriodMs;
                Signal(state, catalog, unit, SiegeEntities.Position(catalog, target), definition.Ability);
            }
        }

        private static bool CanParticipate(Unit unit) =>
            unit.Hp > 0 && !unit.IsCarried && unit.TransitRemainingMs == 0;

        private static void Signal(SiegeState state, SiegeCatalog catalog, Unit unit, SiegePoint target, string kind) =>
            state.EmitEffect(kind: kind, from: SiegeEntities.Position(catalog, unit), to: target,
                startMs: state.ElapsedMs, untilMs: state.ElapsedMs + 500);
    }
}
