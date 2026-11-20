using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeCombat
    {
        internal static void Advance(SiegeState s, SiegeCatalog c)
        {
            s.VisualEffects.RemoveAll(x => x.UntilMs <= s.ElapsedMs);
            s.Zones.RemoveAll(x => x.UntilMs <= s.ElapsedMs);
            SiegeAnimalCasting.Advance(s, c);
            SiegeRepair.AdvanceWork(s, c);
            Cover(s, c);
            BraceAllies(s, c);
            Move(s, c);
            SiegeDefenses.Advance(s, c);
            SiegeGuard.AdvanceEffect(s, c);
            if (
                s.AnimalState.RepairTowerId != null
                && s.Defenses.Any(x => x.Id == s.AnimalState.RepairTowerId && x.Status == TowerStatus.Destroyed)
            )
                SiegeRepair.AdvanceWork(s, c);
            RemoveLost(s, c);
            SiegeAnimalCasting.Advance(s, c);
            while (SiegeEntities.NaturalMerge(s, c)) { }
        }

        internal static void RemoveLost(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops.Where(x => x.Hp <= 0))
                SiegeEntities.Record(s, c, "event.lost");
            s.Stats.Lost += s.Troops.RemoveAll(x => x.Hp <= 0);
        }

        private static void BraceAllies(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops)
            {
                var d = c.Unit(unit.DefinitionId);
                if (unit.Hp <= 0 || d.Ability != "braceAura" || unit.AbilityCharges <= 0
                    || unit.TransitRemainingMs > 0)
                    continue;
                var targets = s.Troops.Where(x => x.Id != unit.Id && x.Hp > 0 && x.Brace == 0
                    && s.ElapsedMs >= x.BraceReadyMs && x.TransitRemainingMs == 0
                    && SiegeEntities.Position(c, unit).Distance(SiegeEntities.Position(c, x)) <= d.AbilityRadius)
                    .OrderBy(x => SiegeEntities.Position(c, unit).Distance(SiegeEntities.Position(c, x)))
                    .ThenBy(x => x.Id).Take(d.AuraTargets).ToArray();
                if (targets.Length == 0)
                    continue;
                unit.AbilityCharges--;
                foreach (var target in targets)
                {
                    target.Brace = 1;
                    target.BraceRearmMs = d.AuraRearmMs;
                    s.VisualEffects.Add(new Effect { Kind = "brace", From = SiegeEntities.Position(c, unit),
                        To = SiegeEntities.Position(c, target), StartMs = s.ElapsedMs, UntilMs = s.ElapsedMs + 500 });
                }
                SiegeEntities.Record(s, c, "event.braced");
            }
        }

        private static void Cover(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops)
            {
                var d = c.Unit(unit.DefinitionId);
                if (d.Ability != "aura" || unit.TransitRemainingMs > 0 || s.ElapsedMs < unit.NextAuraMs)
                    continue;
                foreach (var ally in s.Troops)
                    if (
                        ally.Id != unit.Id
                        && SiegeEntities.Position(c, unit).Distance(SiegeEntities.Position(c, ally)) <= d.AbilityRadius
                    )
                        ally.Shield = 1;
                unit.NextAuraMs = s.ElapsedMs + d.AbilityPeriodMs;
            }
        }

        private static void Move(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops)
            {
                if (unit.Hp <= 0 || unit.HeldUntilMs > s.ElapsedMs)
                    continue;
                if (unit.TransitRemainingMs > 0)
                {
                    unit.TransitRemainingMs = Math.Max(0, unit.TransitRemainingMs - c.Rules.StepMs);
                    continue;
                }
                var d = c.Unit(unit.DefinitionId);
                var lane = c.Lane(unit.LaneId);
                var a = s.AnimalState;
                var target = s
                    .Defenses.Where(x =>
                        x.LaneId == unit.LaneId
                        && x.Status == TowerStatus.Active
                        && x.Hp > 0
                        && x.Distance >= unit.Distance - d.Reach
                    )
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                bool blocked = a.GuardLane == unit.LaneId && a.GuardUntilMs > s.ElapsedMs;
                double limit = Math.Min(
                    target == null ? lane.Length : Math.Max(0, target.Distance - d.Reach * 0.65),
                    blocked ? lane.Length * c.Rules.GuardStart : lane.Length
                );
                unit.Distance = Math.Min(
                    Math.Max(unit.Distance, limit),
                    unit.Distance + d.Speed * MovementFactor(s, c, unit) * c.Rules.StepMs / 1000
                );
                if (unit.BurstHitsLeft > 0 && (target == null || target.Id != unit.BurstTowerId
                    || target.Distance - unit.Distance > d.Reach))
                {
                    unit.BurstHitsLeft = 0;
                    unit.NextAttackMs = Math.Max(unit.NextAttackMs, unit.BurstCooldownMs);
                }
                if (target != null && target.Distance - unit.Distance <= d.Reach && s.ElapsedMs >= unit.NextAttackMs)
                    AttackTower(s, c, unit, target);
                if (unit.Distance < lane.Length)
                    continue;
                s.Progress = Math.Min(c.Rules.Target, s.Progress + d.Gain);
                s.Stats.Delivered++;
                unit.Arrived = true;
                SiegeEntities.Record(s, c, "event.deliver");
            }
            s.Troops.RemoveAll(x => x.Arrived);
        }

        private static double MovementFactor(SiegeState s, SiegeCatalog c, Unit unit)
        {
            double factor = 1;
            var position = SiegeEntities.Position(c, unit);
            foreach (var zone in s.Zones)
                if (position.Distance(zone.Center) <= zone.Radius)
                    factor = Math.Min(factor, zone.Factor);
            return factor;
        }

        private static void AttackTower(SiegeState s, SiegeCatalog c, Unit unit, Tower tower)
        {
            var d = c.Unit(unit.DefinitionId);
            if (d.Ability == "burst" && !unit.BurstUsed)
            {
                unit.BurstUsed = true;
                unit.BurstTowerId = tower.Id;
                unit.BurstHitsLeft = d.BurstHits;
                unit.BurstCooldownMs = s.ElapsedMs + d.AttackMs;
            }
            tower.Hp -= d.Damage;
            if (unit.BurstHitsLeft > 0)
                unit.BurstHitsLeft--;
            unit.NextAttackMs = s.ElapsedMs + (unit.BurstHitsLeft > 0 ? d.BurstIntervalMs : d.AttackMs);
            s.VisualEffects.Add(
                new Effect
                {
                    Kind = "unitShot",
                    From = SiegeEntities.Position(c, unit),
                    To = c.Lane(tower.LaneId).PointAt(tower.Distance),
                    UntilMs = s.ElapsedMs + 160,
                    StartMs = s.ElapsedMs,
                }
            );
            if (tower.Hp > 0)
                return;
            tower.Status = TowerStatus.Destroyed;
            s.Stats.Towers++;
            SiegeEntities.Record(s, c, "event.destroyed");
        }

    }
}
