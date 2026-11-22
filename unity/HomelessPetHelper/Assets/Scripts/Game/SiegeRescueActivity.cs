using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege;

namespace HomelessPetHelper.Game
{
    /// <summary>Bridge for explicitly authored healthy encounters, not a universal medical clearance.</summary>
    public sealed class SiegeRescueActivity : IRescueActivity
    {
        public Guid SessionId { get; }
        public AnimalIndividual Individual { get; }
        public SiegeSession Battle { get; }
        public bool IsClosed { get; private set; }
        private bool _suspended, _outcomeApplied;
        private readonly AnimalDisposition _rescuedDisposition;
        public bool IsSuspended => _suspended || Battle.Paused;
        public bool IsAssistanceRequested => false;
        public bool IsRescueComplete => !IsClosed && !IsSuspended && Battle.Phase == SiegeState.BattlePhase.Won;

        public SiegeRescueActivity(Guid sessionId, int seed, SiegeCatalog catalog)
            : this(sessionId, seed, catalog, null) { }

        public SiegeRescueActivity(Guid sessionId, int seed, SiegeCatalog catalog,
            CityMap.RescueEncounterDefinition encounter, AnimalIndividual individual = null)
        {
            if (encounter != null && !encounter.KnownHealthy)
                throw new ArgumentException("An explicit health source is required.");
            if (sessionId == Guid.Empty) throw new ArgumentException("Missing outing identity.", nameof(sessionId));
            SessionId = sessionId;
            if (individual != null && (encounter == null ||
                individual.Current.DefinitionId != encounter.AnimalDefinitionId))
                throw new ArgumentException("Animal does not belong to this encounter.");
            Individual = individual ?? encounter?.CreateIndividual() ?? new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(true, AnimalEmotion.Nervous, AnimalStress.None,
                    AnimalBehavior.Watching),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()),
                new AnimalCarePermissions(true, true, true), new AnimalCoatCondition(65, 50));
            _rescuedDisposition = encounter?.RescuedDisposition ?? new AnimalDisposition(true,
                AnimalEmotion.Relaxed, AnimalStress.None, AnimalBehavior.PermittingApproach);
            Battle = new SiegeSession(catalog, unchecked((uint)seed), encounter?.RescueProfile ?? "calm",
                sessionId.ToString("N"), encounter?.EncounterId);
        }

        public void SynchronizeOutcome()
        {
            if (IsClosed || _outcomeApplied || Battle.Phase != SiegeState.BattlePhase.Won) return;
            var current = Individual.Current;
            if (!Individual.TryApplyDisposition(current, new AnimalDisposition(current.Disposition.IsHungry,
                _rescuedDisposition.Emotion, _rescuedDisposition.Stress, _rescuedDisposition.Behavior)))
                throw new InvalidOperationException("Animal changed while applying rescue outcome.");
            _outcomeApplied = true;
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
