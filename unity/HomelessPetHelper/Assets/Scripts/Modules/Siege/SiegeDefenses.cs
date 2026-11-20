using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeDefenses
    {
        internal static void Advance(SiegeState s, SiegeCatalog c)
        {
            foreach (var tower in s.Defenses)
            {
                if (tower.Status == TowerStatus.Building && s.ElapsedMs >= tower.ReadyAtMs)
                    tower.Status = TowerStatus.Active;
                if (tower.Status != TowerStatus.Active || tower.Hp <= 0)
                    continue;
                var d = c.Defense(tower);
                if (tower.WindingUp)
                {
                    if (s.ElapsedMs >= tower.WarningReadyMs && s.ResolvedTurn >= tower.SeenTurn)
                        ReleasePush(s, c, tower, d);
                    continue;
                }
                if (s.ElapsedMs < tower.NextShotMs)
                    continue;
                var point = c.Lane(tower.LaneId).PointAt(tower.Distance);
                var target = s.Troops.Where(x => x.Hp > 0
                    && point.Distance(SiegeEntities.Position(c, x)) <= d.Range)
                    .OrderByDescending(x => x.Distance / c.Lane(x.LaneId).Length)
                    .ThenBy(x => x.Id).FirstOrDefault();
                if (target == null)
                    continue;
                Fire(s, c, tower, d, point, target);
            }
        }

        private static void Fire(SiegeState s, SiegeCatalog c, Tower tower,
            SiegeTowerTypeDefinition d, SiegePoint point, Unit target)
        {
            var destination = SiegeEntities.Position(c, target);
            if (d.Attack == "push")
            {
                tower.WindingUp = true;
                tower.LockedPoint = destination;
                tower.WarningReadyMs = s.ElapsedMs + d.WarningMs;
                tower.SeenTurn = s.Turn + 1;
                return;
            }
            tower.NextShotMs = s.ElapsedMs + d.PeriodMs;
            if (d.Attack == "slow")
            {
                s.Zones.Add(new SlowZone { Center = destination, Radius = d.Radius,
                    Factor = d.SlowFactor, UntilMs = s.ElapsedMs + d.ZoneDurationMs, Visual = d.Visual });
                SiegeEntities.Record(s, c, "event.slow");
            }
            else
                SiegeEntities.Hit(target, d.Damage);
            s.VisualEffects.Add(new Effect { Kind = "towerShot", From = point, To = destination,
                StartMs = s.ElapsedMs, UntilMs = s.ElapsedMs + 180, Visual = d.Visual });
        }

        private static void ReleasePush(SiegeState s, SiegeCatalog c, Tower tower,
            SiegeTowerTypeDefinition d)
        {
            tower.WindingUp = false;
            tower.NextShotMs = s.ElapsedMs + d.PeriodMs;
            foreach (var unit in s.Troops)
                if (unit.Hp > 0 && SiegeEntities.Position(c, unit).Distance(tower.LockedPoint) <= d.Radius)
                    SiegeEntities.Hit(unit, d.Damage, d.Push, s.ElapsedMs);
            s.VisualEffects.Add(new Effect { Kind = "push", From = tower.LockedPoint,
                Radius = d.Radius, StartMs = s.ElapsedMs, UntilMs = s.ElapsedMs + 400, Visual = d.Visual });
        }
    }
}
