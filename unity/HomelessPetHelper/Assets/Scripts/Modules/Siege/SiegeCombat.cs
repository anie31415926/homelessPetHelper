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
            Repair(s, c);
            Cover(s, c);
            Move(s, c);
            TowersAttack(s, c);
            GuardAttack(s, c);
            if (
                s.AnimalState.RepairTowerId != null
                && s.Defenses.Any(x => x.Id == s.AnimalState.RepairTowerId && x.Status == TowerStatus.Destroyed)
            )
                Repair(s, c);
            RemoveLost(s, c);
            while (SiegeEntities.NaturalMerge(s, c)) { }
        }

        internal static void RemoveLost(SiegeState s, SiegeCatalog c)
        {
            foreach (var unit in s.Troops.Where(x => x.Hp <= 0))
                SiegeEntities.Record(s, c, "event.lost");
            s.Stats.Lost += s.Troops.RemoveAll(x => x.Hp <= 0);
        }

        private static void Repair(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            if (a.RepairTowerId == null)
                return;
            var tower = s.Defenses.FirstOrDefault(x => x.Id == a.RepairTowerId);
            if (tower == null || tower.Status != TowerStatus.Active || tower.Hp <= 0)
            {
                a.RepairTowerId = null;
                SiegeEntities.Record(s, c, "event.repairStopped");
            }
            else if (s.ElapsedMs >= a.RepairReadyAtMs)
            {
                tower.Hp = Math.Min(c.Tower.Hp, tower.Hp + c.Rules.RepairAmount);
                s.Stats.Repairs++;
                a.RepairTowerId = null;
                s.VisualEffects.Add(
                    new Effect
                    {
                        Kind = "repair",
                        From = c.Lane(tower.LaneId).PointAt(tower.Distance),
                        UntilMs = s.ElapsedMs + 650,
                    }
                );
                SiegeEntities.Record(s, c, "event.repaired");
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
                if (unit.Hp <= 0)
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
                    unit.Distance + d.Speed * c.Rules.StepMs / 1000
                );
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

        private static void AttackTower(SiegeState s, SiegeCatalog c, Unit unit, Tower tower)
        {
            var d = c.Unit(unit.DefinitionId);
            tower.Hp -= d.Damage;
            unit.NextAttackMs = s.ElapsedMs + d.AttackMs;
            s.VisualEffects.Add(
                new Effect
                {
                    Kind = "unitShot",
                    From = SiegeEntities.Position(c, unit),
                    To = c.Lane(tower.LaneId).PointAt(tower.Distance),
                    UntilMs = s.ElapsedMs + 160,
                }
            );
            if (tower.Hp > 0)
                return;
            tower.Status = TowerStatus.Destroyed;
            s.Stats.Towers++;
            SiegeEntities.Record(s, c, "event.destroyed");
        }

        private static void TowersAttack(SiegeState s, SiegeCatalog c)
        {
            foreach (var tower in s.Defenses)
            {
                if (tower.Status == TowerStatus.Building && s.ElapsedMs >= tower.ReadyAtMs)
                    tower.Status = TowerStatus.Active;
                if (tower.Status != TowerStatus.Active || s.ElapsedMs < tower.NextShotMs)
                    continue;
                var point = c.Lane(tower.LaneId).PointAt(tower.Distance);
                var target = s
                    .Troops.Where(x => x.Hp > 0 && point.Distance(SiegeEntities.Position(c, x)) <= c.Tower.Range)
                    .OrderByDescending(x => x.Distance / c.Lane(x.LaneId).Length)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault();
                if (target == null)
                    continue;
                SiegeEntities.Hit(target, c.Tower.Damage);
                tower.NextShotMs = s.ElapsedMs + c.Tower.PeriodMs;
                s.VisualEffects.Add(
                    new Effect
                    {
                        Kind = "towerShot",
                        From = point,
                        To = SiegeEntities.Position(c, target),
                        UntilMs = s.ElapsedMs + 180,
                    }
                );
            }
        }

        private static void GuardAttack(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var r = c.Rules;
            if (a.GuardLane == null || s.ElapsedMs >= a.GuardUntilMs || s.ElapsedMs < a.NextSlapMs)
                return;
            double reach = c.Lane(a.GuardLane).Length * r.GuardStart - r.SlapReach;
            var target = s
                .Troops.Where(x =>
                    x.Hp > 0 && x.LaneId == a.GuardLane && x.TransitRemainingMs == 0 && x.Distance >= reach
                )
                .OrderByDescending(x => x.Distance)
                .ThenBy(x => x.Id)
                .FirstOrDefault();
            if (target == null)
                return;
            s.VisualEffects.Add(
                new Effect
                {
                    Kind = "slap",
                    From = SiegeEntities.Position(c, target),
                    UntilMs = s.ElapsedMs + 350,
                }
            );
            SiegeEntities.Hit(target, r.SlapDamage, r.SlapPush);
            a.NextSlapMs = s.ElapsedMs + r.SlapPeriodMs;
        }
    }
}
