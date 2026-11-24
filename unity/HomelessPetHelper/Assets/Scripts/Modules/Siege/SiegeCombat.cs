using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeCombat
    {
        internal static void Advance(SiegeState s, SiegeCatalog c, bool continuous = false)
        {
            s.ExpireEffects();
            s.Zones.RemoveAll(x => x.UntilMs <= s.ElapsedMs);
            SiegeAnimalCasting.Advance(s, c, continuous);
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
            SiegeAnimalCasting.Advance(s, c, continuous);
            while (SiegeEntities.NaturalMerge(s, c)) { }
        }

        internal static void RemoveLost(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops)
            {
                if (unit.Hp > 0) continue;
                if (s.Realtime)
                    s.EmitEffect(EffectKind.UnitLost, SiegeEntities.Position(c, unit), s.ElapsedMs + 420,
                        startMs: s.ElapsedMs, visual: c.Unit(unit.DefinitionId).Icon, unitId: unit.Id);
                SiegeEntities.Record(s, c, "event.lost");
            }
            s.Stats.Lost += s.Troops.RemoveAll(x => x.Hp <= 0);
        }

        private static void BraceAllies(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops)
            {
                var d = c.Unit(unit.DefinitionId);
                if (unit.Hp <= 0 || unit.IsCarried || d.Ability != "braceAura" || unit.AbilityCharges <= 0
                    || unit.TransitRemainingMs > 0)
                    continue;
                var targets = s.Troops.Where(x => x.Id != unit.Id && x.Hp > 0 && !x.IsCarried && x.Brace == 0
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
                    s.EmitEffect(kind: "brace",
                        from: SiegeEntities.Position(c, unit),
                        to: SiegeEntities.Position(c, target),
                        startMs: s.ElapsedMs,
                        untilMs: s.ElapsedMs + 500);
                }
                SiegeEntities.Record(s, c, "event.braced");
            }
        }

        private static void Cover(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops)
            {
                var d = c.Unit(unit.DefinitionId);
                if (d.Ability != "aura" || unit.IsCarried || unit.TransitRemainingMs > 0
                    || s.ElapsedMs < unit.NextAuraMs)
                    continue;
                foreach (var ally in s.Troops)
                    if (
                        ally.Id != unit.Id
                        && !ally.IsCarried
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
                if (unit.Hp <= 0 || unit.IsCarried || unit.CarriedUntilMs == s.ElapsedMs
                    || unit.HeldUntilMs > s.ElapsedMs)
                    continue;
                if (unit.TransitRemainingMs > 0)
                {
                    unit.TransitRemainingMs = Math.Max(0, unit.TransitRemainingMs - c.Rules.StepMs);
                    continue;
                }
                var d = c.Unit(unit.DefinitionId);
                var lane = c.Lane(unit.LaneId);
                var a = s.AnimalState;
                Tower target = null;
                double nearest = double.PositiveInfinity;
                var position = SiegeEntities.Position(c, unit);
                foreach (var candidate in s.Defenses)
                {
                    if (candidate.Hp <= 0 || candidate.Status != TowerStatus.Active) continue;
                    double separation;
                    if (s.Realtime)
                    {
                        separation = position.Distance(SiegeGeometry.Position(c, candidate));
                        if (separation > d.Reach) continue;
                    }
                    else
                    {
                        if (candidate.LaneId != unit.LaneId || candidate.Distance < unit.Distance - d.Reach) continue;
                        separation = candidate.Distance;
                    }
                    if (separation >= nearest) continue;
                    nearest = separation; target = candidate;
                }
                if (s.Realtime)
                {
                    MoveRealtime(s, c, unit, target, position);
                    continue;
                }
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

        private static void MoveRealtime(SiegeState s, SiegeCatalog c, Unit unit, Tower target,
            SiegePoint position)
        {
            var definition = c.Unit(unit.DefinitionId);
            if (target != null)
            {
                if (s.ElapsedMs >= unit.NextAttackMs) AttackTower(s, c, unit, target);
                return;
            }
            var lane = c.Lane(unit.LaneId);
            double travel = definition.Speed * MovementFactor(s, c, unit) * c.Rules.StepMs / 1000;
            var pathPoint = lane.PointAt(unit.Distance);
            double joining = position.Distance(pathPoint);
            if (unit.FreePosition.HasValue && joining > travel)
            {
                unit.FreePosition = position.Lerp(pathPoint, travel / joining);
                return;
            }
            unit.FreePosition = null;
            bool blocked = s.AnimalState.GuardLane == unit.LaneId && s.AnimalState.GuardUntilMs > s.ElapsedMs;
            double limit = blocked ? lane.Length * c.Rules.GuardStart : lane.Length;
            unit.Distance = Math.Min(Math.Max(unit.Distance, limit), unit.Distance + Math.Max(0, travel - joining));
            if (unit.Distance < lane.Length) return;
            s.Progress = Math.Min(c.Rules.Target, s.Progress + definition.Gain);
            s.Stats.Delivered++; unit.Arrived = true;
            SiegeEntities.Record(s, c, "event.deliver");
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
            s.EmitEffect(kind: EffectKind.UnitShot,
                from: SiegeEntities.Position(c, unit),
                to: SiegeGeometry.Position(c, tower),
                untilMs: s.ElapsedMs + 160,
                startMs: s.ElapsedMs, visual: d.Icon, unitId: unit.Id);
            if (tower.Hp > 0)
                return;
            tower.Status = TowerStatus.Destroyed;
            if (s.Realtime)
                s.EmitEffect(EffectKind.TowerLost, SiegeGeometry.Position(c, tower), s.ElapsedMs + 380,
                    startMs: s.ElapsedMs, visual: c.Defense(tower).Visual);
            s.Stats.Towers++;
            SiegeEntities.Record(s, c, "event.destroyed");
        }

    }
}
