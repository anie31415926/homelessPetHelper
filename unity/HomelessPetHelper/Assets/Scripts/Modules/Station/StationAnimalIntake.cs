using System;

namespace HomelessPetHelper.Modules.Station
{
    public sealed class StationAnimalIntake
    {
        public Guid OutingId { get; }

        public Guid AnimalId { get; }

        public string AnimalDefinitionId { get; }

        public AnimalIntakeCondition Condition { get; }

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
