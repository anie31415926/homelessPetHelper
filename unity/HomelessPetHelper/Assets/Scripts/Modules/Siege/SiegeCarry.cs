using System;
using System.Linq;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal sealed class SiegeCarry : SiegeAnimalSkill
    {
        internal override IntentKind Kind => IntentKind.Carry;
        internal override bool StopsGuard => true;
        internal override bool CooldownOnMiss => true;
        internal override int DelayMs(SiegeState s, SiegeCatalog c) => Definition(s, c).CarryDelayMs;
        internal override int RecoveryMs(SiegeState s, SiegeCatalog c) => Definition(s, c).CarryRecoveryMs;
        internal override int CooldownMs(SiegeState s, SiegeCatalog c) => Definition(s, c).CarryCooldownMs;

        internal override void Plan(SiegeState s, SiegeCatalog c) => SiegeEntities.Record(s, c, "event.carryPlan");

        internal static Unit FindTarget(SiegeState s, SiegeCatalog c)
        {
            var d = Definition(s, c);
            if (d == null || d.CarryDurationMs == 0 || s.AnimalState.CarriedUnitId != 0
                || s.ElapsedMs < s.AnimalState.SkillReadyAtMs(IntentKind.Carry))
                return null;
            return s.Troops.Where(x => Eligible(s, c, d, x))
                .OrderByDescending(x => x.Distance / c.Lane(x.LaneId).Length).ThenBy(x => x.Id).FirstOrDefault();
        }

        internal override bool CanResolve(SiegeState s, SiegeCatalog c) =>
            Eligible(s, c, Definition(s, c), Target(s));

        internal override void Begin(SiegeState s, SiegeCatalog c)
        {
            var unit = Target(s);
            var d = Definition(s, c);
            s.AnimalState.Cast.Point = unit == null ? new SiegePoint(d.CarryOriginX, d.CarryOriginY)
                : SiegeEntities.Position(c, unit);
        }

        internal override int Resolve(SiegeState s, SiegeCatalog c)
        {
            var d = Definition(s, c);
            var unit = Target(s);
            unit.FreePosition = null;
            unit.IsCarried = true;
            unit.CarryStartedMs = s.ElapsedMs;
            unit.CarriedUntilMs = s.ElapsedMs + d.CarryDurationMs;
            unit.CarryFromDistance = unit.Distance;
            unit.CarryToDistance = Math.Max(d.CarryMinDistance, unit.Distance - d.CarryDistance);
            unit.BurstHitsLeft = 0;
            unit.NextAttackMs = Math.Max(unit.NextAttackMs, unit.BurstCooldownMs);
            s.AnimalState.Cast.Point = SiegeEntities.Position(c, unit);
            s.Stats.Carries++;
            SiegeEntities.Record(s, c, "event.carried");
            return d.CarryDurationMs;
        }

        internal override void AdvanceActive(SiegeState s, SiegeCatalog c)
        {
            var unit = Target(s);
            if (unit == null || !unit.IsCarried) return;
            double t = Math.Min(1, (double)(s.ElapsedMs - unit.CarryStartedMs)
                / (unit.CarriedUntilMs - unit.CarryStartedMs));
            unit.Distance = unit.CarryFromDistance + (unit.CarryToDistance - unit.CarryFromDistance) * t;
        }

        internal override bool HasEnded(SiegeState s, SiegeCatalog c)
        {
            var unit = Target(s);
            return unit == null || unit.Hp <= 0;
        }

        internal override void End(SiegeState s, SiegeCatalog c)
        {
            var unit = Target(s);
            if (unit == null || !unit.IsCarried) return;
            unit.IsCarried = false;
            unit.CarriedUntilMs = s.ElapsedMs;
            if (unit.Hp <= 0) return;
            unit.HoldProtectedUntilMs = Math.Max(unit.HoldProtectedUntilMs,
                s.ElapsedMs + Definition(s, c).CarryProtectionMs);
            s.AnimalState.Cast.Point = SiegeEntities.Position(c, unit);
            SiegeEntities.Record(s, c, "event.carryEnd");
        }

        internal override void Miss(SiegeState s, SiegeCatalog c)
        {
            s.Stats.CarryMisses++;
            SiegeEntities.Record(s, c, "event.carryMiss");
        }

        private static Unit Target(SiegeState s) =>
            s.Troops.FirstOrDefault(x => x.Id == s.AnimalState.Cast.Target.UnitId);

        private static SiegeEncounterDefinition Definition(SiegeState s, SiegeCatalog c) =>
            s.EncounterId == null ? null : c.Content.Encounter(s.EncounterId);

        private static bool Eligible(SiegeState s, SiegeCatalog c, SiegeEncounterDefinition d, Unit unit) =>
            d != null && d.CarryDurationMs > 0 && unit != null && unit.Hp > 0 && !unit.IsCarried
            && unit.TransitRemainingMs == 0 && unit.HeldUntilMs <= s.ElapsedMs
            && unit.HoldProtectedUntilMs <= s.ElapsedMs && unit.Distance > d.CarryMinDistance
            && SiegeEntities.Position(c, unit).Distance(new SiegePoint(d.CarryOriginX, d.CarryOriginY)) <= d.CarryRange;
    }
}
