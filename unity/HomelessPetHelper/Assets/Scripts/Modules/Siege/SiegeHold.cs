using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal sealed class SiegeHold : SiegeAnimalSkill
    {
        internal override IntentKind Kind => IntentKind.Hold;
        internal override bool StopsGuard => true;
        internal override bool CooldownOnMiss => true;
        internal override int RecoveryMs(SiegeState s, SiegeCatalog c) => Definition(s, c).HoldRecoveryMs;
        internal override int CooldownMs(SiegeState s, SiegeCatalog c) => Definition(s, c).HoldCooldownMs;
        internal override void Plan(SiegeState s, SiegeCatalog c) =>
            SiegeEntities.Record(s, c, "event.holdPlan");

        internal static Unit FindTarget(SiegeState s, SiegeCatalog c)
        {
            var d = Definition(s, c);
            var a = s.AnimalState;
            if (d == null || d.HoldDurationMs == 0 || a.HeldUnitId != 0
                || s.ElapsedMs < a.SkillReadyAtMs(IntentKind.Hold))
                return null;
            return s.Troops.Where(x => Eligible(s, c, d, x))
                .OrderByDescending(x => x.Distance / c.Lane(x.LaneId).Length).ThenBy(x => x.Id).FirstOrDefault();
        }

        internal override bool CanResolve(SiegeState s, SiegeCatalog c) =>
            Eligible(s, c, Definition(s, c), s.Troops.FirstOrDefault(x => x.Id == s.AnimalState.Cast.Target.UnitId));

        internal override void Miss(SiegeState s, SiegeCatalog c)
        {
            s.Stats.HoldMisses++;
            SiegeEntities.Record(s, c, "event.holdMiss");
        }

        internal override int Resolve(SiegeState s, SiegeCatalog c)
        {
            var d = Definition(s, c);
            var a = s.AnimalState;
            if (d == null || d.HoldDurationMs == 0)
                throw new InvalidOperationException("Hold requires an enabled encounter definition.");
            var target = s.Troops.First(x => x.Id == a.Cast.Target.UnitId);
            a.HeldUnitId = target.Id;
            a.HoldStartedMs = s.ElapsedMs;
            a.HoldUntilMs = s.ElapsedMs + d.HoldDurationMs;
            target.HeldUntilMs = a.HoldUntilMs;
            target.HoldProtectedUntilMs = a.HoldUntilMs + d.HoldProtectionMs;
            target.BurstHitsLeft = 0;
            target.NextAttackMs = Math.Max(target.NextAttackMs, target.BurstCooldownMs);
            s.Stats.Holds++;
            SiegeEntities.Record(s, c, "event.held");
            return d.HoldDurationMs;
        }

        internal override bool HasEnded(SiegeState s, SiegeCatalog c) =>
            s.Troops.All(x => x.Id != s.AnimalState.HeldUnitId || x.Hp <= 0);

        internal override void End(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            if (a.HeldUnitId == 0)
                return;
            a.HeldUnitId = 0;
            a.HoldUntilMs = s.ElapsedMs;
            SiegeEntities.Record(s, c, "event.holdEnd");
        }

        internal static void TransferMerge(SiegeState s, Unit[] materials, Unit merged)
        {
            merged.HeldUntilMs = materials.Max(x => x.HeldUntilMs);
            merged.HoldProtectedUntilMs = materials.Max(x => x.HoldProtectedUntilMs);
            var a = s.AnimalState;
            if (materials.Any(x => x.Id == a.HeldUnitId))
            {
                a.HeldUnitId = merged.Id;
                a.HoldUntilMs = merged.HeldUntilMs;
            }
        }

        private static SiegeEncounterDefinition Definition(SiegeState s, SiegeCatalog c) =>
            s.EncounterId == null ? null : c.Content.Encounter(s.EncounterId);

        private static bool Eligible(SiegeState s, SiegeCatalog c, SiegeEncounterDefinition d, Unit unit) =>
            d != null && d.HoldDurationMs > 0 && unit != null && unit.Hp > 0
            && unit.TransitRemainingMs == 0 && unit.HeldUntilMs <= s.ElapsedMs
            && unit.HoldProtectedUntilMs <= s.ElapsedMs
            && SiegeEntities.Position(c, unit).Distance(new SiegePoint(d.HoldOriginX, d.HoldOriginY)) <= d.HoldRange;
    }
}
