using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeAnimal
    {
        internal static void Act(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var previous = a.Intent;
            string lane = a.IntentLane;
            switch (previous)
            {
                case IntentKind.Guard:
                    a.GuardLane = lane;
                    a.GuardUntilMs = s.ElapsedMs + c.Rules.GuardDurationMs;
                    a.NextSlapMs = s.ElapsedMs;
                    SiegeEntities.Record(s, c, "event.guard");
                    break;
                case IntentKind.Sweep:
                    foreach (var unit in s.Troops)
                        if (unit.Hp > 0 && SiegeEntities.Position(c, unit).Distance(a.SweepCenter) <= a.SweepRadius)
                            SiegeEntities.Hit(unit, c.Rules.SweepDamage, c.Rules.SweepPush);
                    a.GuardUntilMs = s.ElapsedMs;
                    a.RecoveryUntilMs = s.ElapsedMs + c.Rules.SweepRecoveryMs;
                    s.VisualEffects.Add(
                        new Effect
                        {
                            Kind = "sweep",
                            From = a.SweepCenter,
                            Radius = a.SweepRadius,
                            UntilMs = s.ElapsedMs + 650,
                        }
                    );
                    SiegeEntities.Record(s, c, "event.sweep");
                    break;
                case IntentKind.Build:
                    Build(s, c);
                    break;
                case IntentKind.Repair:
                    Repair(s, c);
                    break;
                default:
                    throw new InvalidOperationException("Unknown animal intent.");
            }
            a.Actions++;
            a.Charge = 0;
            SiegeCombat.RemoveLost(s, c);
            ChooseNext(s, c, previous, lane);
        }

        private static void Build(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var pad = c.Pads.FirstOrDefault(x => x.Id == a.IntentPad);
            if (pad == null || a.BuildsLeft <= 0 || s.Defenses.Any(x => x.Id == pad.Id))
                return;
            s.Defenses.Add(
                new Tower
                {
                    Id = pad.Id,
                    LaneId = pad.LaneId,
                    Distance = c.Lane(pad.LaneId).Length * pad.At,
                    Hp = c.Tower.Hp,
                    Status = TowerStatus.Building,
                    ReadyAtMs = s.ElapsedMs + c.Rules.BuildDurationMs,
                    NextShotMs = s.ElapsedMs + c.Rules.BuildDurationMs,
                }
            );
            a.BuildsLeft--;
        }

        private static void Repair(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var tower = s.Defenses.FirstOrDefault(x => x.Id == a.IntentPad);
            if (
                tower == null
                || tower.Status != TowerStatus.Active
                || tower.Hp <= 0
                || tower.Hp >= c.Tower.Hp
                || a.RepairsLeft <= 0
                || a.RepairTowerId != null
                || s.ElapsedMs < a.NextRepairMs
            )
                return;
            a.RepairsLeft--;
            a.NextRepairMs = s.ElapsedMs + c.Rules.RepairCooldownMs;
            a.RepairTowerId = tower.Id;
            a.RepairReadyAtMs = s.ElapsedMs + c.Rules.RepairDurationMs;
            SiegeEntities.Record(s, c, "event.repairStart");
        }

        private static void ChooseNext(SiegeState s, SiegeCatalog c, IntentKind previous, string lane)
        {
            var a = s.AnimalState;
            if (previous == IntentKind.Guard)
            {
                a.Intent = IntentKind.Sweep;
                a.IntentLane = lane;
                a.IntentPad = null;
                a.SeenTurn = s.Turn + 1;
                a.SweepCenter = c.Lane(lane).PointAt(c.Lane(lane).Length * c.Rules.SweepCenterAt);
                a.SweepRadius = c.Rules.SweepRadius;
                SiegeEntities.Record(s, c, "event.warning");
                return;
            }
            var pressure = c
                .Lanes.Where(x => x.Open)
                .Select(
                    (x, index) =>
                        new
                        {
                            Lane = x,
                            Index = index,
                            Score = s
                                .Troops.Where(u => u.LaneId == x.Id && u.Hp > 0 && u.TransitRemainingMs == 0)
                                .Sum(u =>
                                    c.Rules.ThreatBase
                                    + c.Rules.ThreatNearWeight * Math.Pow(u.Distance / x.Length, c.Rules.ThreatExponent)
                                ),
                        }
                )
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Index)
                .Select(x => x.Lane.Id)
                .ToArray();
            Tower repair = null;
            if (a.RepairsLeft > 0 && a.RepairTowerId == null && s.ElapsedMs >= a.NextRepairMs)
                repair = s
                    .Defenses.Where(x =>
                        x.Status == TowerStatus.Active && x.Hp > 0 && x.Hp <= c.Tower.Hp * c.Rules.RepairThreshold
                    )
                    .OrderBy(x => Array.IndexOf(pressure, x.LaneId))
                    .ThenBy(x => x.Hp)
                    .FirstOrDefault();
            if (repair != null)
            {
                a.Intent = IntentKind.Repair;
                a.IntentLane = repair.LaneId;
                a.IntentPad = repair.Id;
                a.SeenTurn = s.Turn + 1;
                SiegeEntities.Record(s, c, "event.repairPlan");
                return;
            }
            var pad = c
                .Pads.Where(x => c.Lane(x.LaneId).Open && s.Defenses.All(t => t.Id != x.Id))
                .OrderBy(x => Array.IndexOf(pressure, x.LaneId))
                .ThenBy(x => x.At)
                .FirstOrDefault();
            if (a.BuildsLeft > 0 && a.Actions + 1 >= c.Rules.BuildOnAnimalTurn && pad != null)
            {
                a.Intent = IntentKind.Build;
                a.IntentLane = pad.LaneId;
                a.IntentPad = pad.Id;
                a.SeenTurn = s.Turn + 1;
                SiegeEntities.Record(s, c, "event.build");
            }
            else
            {
                a.Intent = IntentKind.Guard;
                a.IntentLane = pressure[0];
                a.IntentPad = null;
                a.SeenTurn = s.Turn;
            }
        }
    }
}
