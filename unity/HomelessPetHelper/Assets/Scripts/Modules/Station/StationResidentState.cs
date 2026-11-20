using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station
{
    /// <summary>Detached, validated reconstruction input, with frozen history and current care kept separate.</summary>
    public sealed class StationResidentState
    {
        public StationAnimalIntake Arrival { get; }
        public StationCareSnapshot Care { get; }

        public StationResidentState(StationAnimalIntake arrival, StationHealthAssessment assessment,
            AnimalHealthSnapshot healthAtAssessment)
        {
            Arrival = arrival ?? throw new ArgumentNullException(nameof(arrival));
            if (arrival.Individual == null || arrival.AnimalAtArrival == null)
                throw new ArgumentException("Restoration requires explicit individual history.");
            // This first restoration contract supports known healthy Main content only.
            if (arrival.Individual.Current.Health.Problems.Count != 0 ||
                (assessment != StationHealthAssessment.Unchecked &&
                    assessment != StationHealthAssessment.NoUrgentCareRequired) ||
                (assessment == StationHealthAssessment.Unchecked ? healthAtAssessment != null :
                    healthAtAssessment == null || healthAtAssessment.Problems.Count != 0))
                throw new ArgumentException("Unsupported or inconsistent assessment snapshot.");
            Care = new StationCareSnapshot(arrival.AnimalId, arrival.OutingId, assessment,
                Array.Empty<StationCareProblem>(), healthAtAssessment);
        }
    }
}
