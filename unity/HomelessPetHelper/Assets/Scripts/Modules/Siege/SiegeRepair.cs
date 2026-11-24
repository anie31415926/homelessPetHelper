using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal sealed class SiegeRepair : SiegeAnimalSkill
    {
        internal override IntentKind Kind => IntentKind.Repair;
        internal override int CooldownMs(SiegeState s, SiegeCatalog c) => c.Rules.RepairCooldownMs;
        internal override void Plan(SiegeState s, SiegeCatalog c) =>
            SiegeEntities.Record(s, c, "event.repairPlan");

        internal override bool CanResolve(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var tower = s.Defenses.FirstOrDefault(x => x.Id == a.Cast.Target.PadId);
            return tower != null && tower.Status == TowerStatus.Active && tower.Hp > 0
                && tower.Hp < c.Defense(tower).Hp && a.RepairsLeft > 0 && a.RepairTowerId == null;
        }

        internal override int Resolve(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            a.RepairsLeft--;
            a.RepairTowerId = a.Cast.Target.PadId;
            a.RepairReadyAtMs = s.ElapsedMs + c.Rules.RepairDurationMs;
            SiegeEntities.Record(s, c, "event.repairStart");
            return 0;
        }

        internal static void AdvanceWork(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            if (a.RepairTowerId == null) return;
            var tower = s.Defenses.FirstOrDefault(x => x.Id == a.RepairTowerId);
            if (tower == null || tower.Status != TowerStatus.Active || tower.Hp <= 0)
            {
                a.RepairTowerId = null;
                SiegeEntities.Record(s, c, "event.repairStopped");
            }
            else if (s.ElapsedMs >= a.RepairReadyAtMs)
            {
                tower.Hp = Math.Min(c.Defense(tower).Hp, tower.Hp + c.Rules.RepairAmount);
                s.Stats.Repairs++;
                a.RepairTowerId = null;
                s.EmitEffect(kind: "repair",
                    from: SiegeGeometry.Position(c, tower),
                    untilMs: s.ElapsedMs + 650);
                SiegeEntities.Record(s, c, "event.repaired");
            }
        }
    }
}
