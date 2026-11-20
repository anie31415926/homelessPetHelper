using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege;

namespace HomelessPetHelper.Game
{
    /// <summary>Bridge for the explicitly healthy parking-cat encounter, not a universal medical clearance.</summary>
    public sealed class SiegeRescueActivity : IRescueActivity
    {
        public Guid SessionId { get; }
        public AnimalIndividual Individual { get; }
        public SiegeSession Battle { get; }
        public bool IsClosed { get; private set; }
        private bool _suspended;
        public bool IsSuspended => _suspended || Battle.Paused;
        public bool IsAssistanceRequested => false;
        public bool IsRescueComplete => !IsClosed && !IsSuspended && Battle.Phase == SiegeState.BattlePhase.Won;

        public SiegeRescueActivity(Guid sessionId, int seed, SiegeCatalog catalog)
        {
            if (sessionId == Guid.Empty) throw new ArgumentException("Missing outing identity.", nameof(sessionId));
            SessionId = sessionId;
            Individual = new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(true, AnimalEmotion.Nervous, AnimalStress.None, AnimalBehavior.Watching),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()),
                new AnimalCarePermissions(true, true, true), new AnimalCoatCondition(65, 50));
            Battle = new SiegeSession(catalog, unchecked((uint)seed), "calm", sessionId.ToString("N"));
        }

        public void SynchronizeOutcome()
        {
            if (IsClosed || Battle.Phase != SiegeState.BattlePhase.Won) return;
            var current = Individual.Current;
            if (current.Disposition.Behavior == AnimalBehavior.PermittingApproach) return;
            if (!Individual.TryApplyDisposition(current, new AnimalDisposition(current.Disposition.IsHungry,
                AnimalEmotion.Relaxed, AnimalStress.None, AnimalBehavior.PermittingApproach)))
                throw new InvalidOperationException("Animal changed while applying rescue outcome.");
        }

        public void SetSuspended(bool suspended)
        {
            if (IsClosed) return;
            _suspended = suspended;
            Battle.SetPaused(suspended);
        }

        public void Dispose()
        {
            if (IsClosed) return;
            Battle.SetPaused(true);
            IsClosed = true;
        }
    }
}
