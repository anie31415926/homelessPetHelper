using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station
{
    public sealed class StationAnimalIntake
    {
        public Guid OutingId { get; }

        public Guid AnimalId { get; }

        public string AnimalDefinitionId { get; }

        public AnimalIntakeCondition Condition { get; }
        public AnimalSnapshot AnimalAtArrival { get; }
        internal AnimalIndividual Individual { get; }

        public StationAnimalIntake(Guid outingId, AnimalIndividual individual, AnimalIntakeCondition condition)
            : this(outingId, (individual ?? throw new ArgumentNullException(nameof(individual))).Current.AnimalId,
                individual.Current.DefinitionId, condition)
        {
            Individual = individual;
            AnimalAtArrival = individual.Current;
        }

        /// <summary>Reconstructs history separately from the current individual; never replays admission.</summary>
        public static StationAnimalIntake Restore(Guid outingId, AnimalSnapshot arrival,
            AnimalIndividual current, AnimalIntakeCondition condition)
        {
            if (arrival == null || current == null || arrival.AnimalId != current.Current.AnimalId ||
                arrival.DefinitionId != current.Current.DefinitionId)
                throw new ArgumentException("Arrival and current individual identities differ.");
            return new StationAnimalIntake(outingId, current, condition, arrival);
        }

        private StationAnimalIntake(Guid outingId, AnimalIndividual current, AnimalIntakeCondition condition,
            AnimalSnapshot arrival) : this(outingId, current.Current.AnimalId, current.Current.DefinitionId, condition)
        {
            Individual = current;
            AnimalAtArrival = arrival;
        }

        /// <summary>
        /// Checks owner identity and frozen arrival against the latest state without exposing mutation.
        /// </summary>
        public bool MatchesCurrentIndividual(AnimalIndividual individual)
        {
            return individual != null && ReferenceEquals(Individual, individual) &&
                ReferenceEquals(AnimalAtArrival, individual.Current);
        }

        public StationAnimalIntake(Guid outingId, Guid animalId, string animalDefinitionId,
            AnimalIntakeCondition condition)
        {
            if (outingId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty outing ID is required.", nameof(outingId));
            }

            if (animalId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty animal ID is required.", nameof(animalId));
            }

            if (string.IsNullOrWhiteSpace(animalDefinitionId) || animalDefinitionId != animalDefinitionId.Trim())
            {
                throw new ArgumentException("A trimmed animal definition ID is required.", nameof(animalDefinitionId));
            }

            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            OutingId = outingId;
            AnimalId = animalId;
            AnimalDefinitionId = animalDefinitionId;
        }
    }
}
