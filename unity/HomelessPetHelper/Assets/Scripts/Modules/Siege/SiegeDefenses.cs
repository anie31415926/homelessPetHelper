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
                    if (s.ElapsedMs >= tower.WarningReadyMs && (s.Realtime || s.ResolvedTurn >= tower.SeenTurn))
                        ReleasePush(s, c, tower, d);
                    continue;
                }
                if (s.ElapsedMs < tower.NextShotMs)
                    continue;
                var point = SiegeGeometry.Position(c, tower);
                Unit target = null;
                double best = double.NegativeInfinity;
                foreach (var candidate in s.Troops)
                {
                    if (candidate.Hp <= 0 || candidate.IsCarried ||
                        point.Distance(SiegeEntities.Position(c, candidate)) > d.Range) continue;
                    double score = candidate.Distance / c.Lane(candidate.LaneId).Length;
                    if (score < best || score == best && target != null && candidate.Id >= target.Id) continue;
                    target = candidate; best = score;
                }
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
            s.EmitEffect(kind: EffectKind.TowerShot,
                from: point,
                to: destination,
                startMs: s.ElapsedMs,
                untilMs: s.ElapsedMs + (s.Realtime ? 260 : 180),
                visual: d.Visual, unitId: d.Attack == "slow" ? 0 : target.Id);
        }

        private static void ReleasePush(SiegeState s, SiegeCatalog c, Tower tower,
            SiegeTowerTypeDefinition d)
        {
            tower.WindingUp = false;
            tower.NextShotMs = s.ElapsedMs + d.PeriodMs;
            foreach (var unit in s.Troops)
                if (unit.Hp > 0 && !unit.IsCarried
                    && SiegeEntities.Position(c, unit).Distance(tower.LockedPoint) <= d.Radius)
                    SiegeEntities.Hit(unit, d.Damage, d.Push, s.ElapsedMs);
            s.EmitEffect(kind: "push",
                from: tower.LockedPoint,
                radius: d.Radius,
                startMs: s.ElapsedMs,
                untilMs: s.ElapsedMs + 400,
                visual: d.Visual);
        }
    }
}
