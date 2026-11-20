using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal static class SiegeAnimal
    {
        internal static void ChooseNext(SiegeState s, SiegeCatalog c, IntentKind previous, string lane)
        {
            var a = s.AnimalState;
            var hold = previous == IntentKind.Guard ? SiegeHold.FindTarget(s, c) : null;
            if (hold != null)
            {
                SiegeAnimalCasting.Plan(s, c, IntentKind.Hold, new SkillTarget(hold.LaneId, unitId: hold.Id));
                return;
            }
            var carry = previous == IntentKind.Guard ? SiegeCarry.FindTarget(s, c) : null;
            if (carry != null)
            {
                SiegeAnimalCasting.Plan(s, c, IntentKind.Carry, new SkillTarget(carry.LaneId, unitId: carry.Id));
                return;
            }
            if (previous == IntentKind.Guard || previous == IntentKind.Hold || previous == IntentKind.Carry)
            {
                SiegeAnimalCasting.Plan(s, c, IntentKind.Sweep, new SkillTarget(lane));
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
            if (a.RepairsLeft > 0 && a.RepairTowerId == null && s.ElapsedMs >= a.SkillReadyAtMs(IntentKind.Repair))
                repair = s
                    .Defenses.Where(x =>
                        x.Status == TowerStatus.Active && x.Hp > 0 && x.Hp <= c.Defense(x).Hp * c.Rules.RepairThreshold
                    )
                    .OrderBy(x => Array.IndexOf(pressure, x.LaneId))
                    .ThenBy(x => x.Hp)
                    .FirstOrDefault();
            if (repair != null)
            {
                SiegeAnimalCasting.Plan(s, c, IntentKind.Repair, new SkillTarget(repair.LaneId, repair.Id));
                return;
            }
            var pad = c
                .Pads.Where(x => c.Lane(x.LaneId).Open && s.Defenses.All(t => t.Id != x.Id))
                .OrderBy(x => Array.IndexOf(pressure, x.LaneId))
                .ThenBy(x => x.At)
                .FirstOrDefault();
            if (a.BuildsLeft > 0 && a.Actions + 1 >= c.Rules.BuildOnAnimalTurn && pad != null)
            {
                SiegeAnimalCasting.Plan(s, c, IntentKind.Build, new SkillTarget(pad.LaneId, pad.Id));
            }
            else
            {
                SiegeAnimalCasting.Plan(s, c, IntentKind.Guard, new SkillTarget(pressure[0]));
            }
        }
    }
}
