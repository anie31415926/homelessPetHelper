using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station
{
    /// <summary>Immutable initial-care progress. Admission history and animal health remain separate.</summary>
    public sealed class StationCareSnapshot
    {
        public Guid AnimalId { get; }
        public Guid OutingId { get; }
        public StationHealthAssessment BasicAssessment { get; }
        public IReadOnlyList<StationCareProblem> NecessaryProblems { get; }
        public AnimalHealthSnapshot HealthAtAssessment { get; }

        public bool NecessaryCareIsArranged
        {
            get
            {
                if (BasicAssessment == StationHealthAssessment.Unchecked)
                {
                    return false;
                }

                foreach (StationCareProblem problem in NecessaryProblems)
                {
                    if (problem.Status != ProfessionalCareStatus.SafelyHandedOver)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        internal StationCareSnapshot(Guid animalId, Guid outingId, StationHealthAssessment assessment,
            IEnumerable<StationCareProblem> necessaryProblems, AnimalHealthSnapshot healthAtAssessment = null)
        {
            AnimalId = animalId;
            OutingId = outingId;
            BasicAssessment = assessment;
            NecessaryProblems = new List<StationCareProblem>(necessaryProblems).AsReadOnly();
            HealthAtAssessment = healthAtAssessment;
        }
    }
}
