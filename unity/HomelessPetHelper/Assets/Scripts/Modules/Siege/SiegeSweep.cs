using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    internal sealed class SiegeSweep : SiegeAnimalSkill
    {
        internal override IntentKind Kind => IntentKind.Sweep;
        internal override bool StopsGuard => true;
        internal override int DelayMs(SiegeState s, SiegeCatalog c) =>
            s.EncounterId == null ? 0 : c.Content.Encounter(s.EncounterId).SweepDelayMs;
        internal override int RecoveryMs(SiegeState s, SiegeCatalog c) => c.Rules.SweepRecoveryMs;

        internal override void Plan(SiegeState s, SiegeCatalog c)
        {
            var a = s.AnimalState;
            a.SweepCenter = c.Lane(a.IntentLane).PointAt(c.Lane(a.IntentLane).Length * c.Rules.SweepCenterAt);
            a.SweepRadius = c.Rules.SweepRadius;
            SiegeEntities.Record(s, c, "event.warning");
        }

        internal override void Begin(SiegeState s, SiegeCatalog c) =>
            s.AnimalState.LeapStartMs = s.ElapsedMs;

        internal override int Resolve(SiegeState s, SiegeCatalog c)
        {
            var cast = s.AnimalState.Cast;
            foreach (var unit in s.Troops)
                if (unit.Hp > 0 && SiegeEntities.Position(c, unit).Distance(cast.Point) <= cast.Radius)
                    SiegeEntities.Hit(unit, c.Rules.SweepDamage, c.Rules.SweepPush, s.ElapsedMs);
            s.VisualEffects.Add(new Effect { Kind = "sweep", From = cast.Point, Radius = cast.Radius,
                StartMs = s.ElapsedMs, UntilMs = s.ElapsedMs + 650 });
            SiegeEntities.Record(s, c, "event.sweep");
            return 0;
        }
    }
}
