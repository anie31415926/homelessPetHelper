using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Modules.Siege
{
    // Stateless effects only. The casting runner owns gates, clocks and action consumption.
    internal abstract class SiegeAnimalSkill
    {
        internal abstract IntentKind Kind { get; }
        internal virtual bool RequiresDecision => true;
        internal virtual bool StopsGuard => false;
        internal virtual bool CooldownOnMiss => false;
        internal virtual int DelayMs(SiegeState s, SiegeCatalog c) => 0;
        internal virtual int RecoveryMs(SiegeState s, SiegeCatalog c) => 0;
        internal virtual int CooldownMs(SiegeState s, SiegeCatalog c) => 0;
        internal virtual void Plan(SiegeState s, SiegeCatalog c) { }
        internal virtual void Begin(SiegeState s, SiegeCatalog c) { }
        internal virtual bool CanResolve(SiegeState s, SiegeCatalog c) => true;
        // Return actor occupation, not the lifetime of an independent effect such as a tower.
        internal abstract int Resolve(SiegeState s, SiegeCatalog c);
        internal virtual void Miss(SiegeState s, SiegeCatalog c) { }
        internal virtual bool HasEnded(SiegeState s, SiegeCatalog c) => false;
        internal virtual void End(SiegeState s, SiegeCatalog c) { }
    }
}
