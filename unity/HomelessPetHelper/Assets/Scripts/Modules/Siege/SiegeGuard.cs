using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal sealed class SiegeGuard : SiegeAnimalSkill
    {
        internal override IntentKind Kind => IntentKind.Guard;
        internal override bool RequiresDecision => false;

        internal override int Resolve(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            a.GuardLane = a.Cast.Target.LaneId;
            a.GuardUntilMs = s.ElapsedMs + c.Rules.GuardDurationMs;
            a.NextSlapMs = s.ElapsedMs;
            SiegeEntities.Record(s, c, "event.guard");
            return 0;
        }

        internal static void AdvanceEffect(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            var r = c.Rules;
            if (a.GuardLane == null || a.ImpactAtMs > 0 || a.HeldUnitId != 0
                || s.ElapsedMs >= a.GuardUntilMs || s.ElapsedMs < a.NextSlapMs)
                return;
            double reach = c.Lane(a.GuardLane).Length * r.GuardStart - r.SlapReach;
            var target = s.Troops.Where(x => x.Hp > 0 && x.LaneId == a.GuardLane
                    && x.TransitRemainingMs == 0 && x.Distance >= reach)
                .OrderByDescending(x => x.Distance).ThenBy(x => x.Id).FirstOrDefault();
            if (target == null) return;
            s.VisualEffects.Add(new Effect
            {
                Kind = "slap", From = SiegeEntities.Position(c, target), UntilMs = s.ElapsedMs + 350,
            });
            SiegeEntities.Hit(target, r.SlapDamage, r.SlapPush, s.ElapsedMs);
            a.NextSlapMs = s.ElapsedMs + r.SlapPeriodMs;
        }
    }
}
