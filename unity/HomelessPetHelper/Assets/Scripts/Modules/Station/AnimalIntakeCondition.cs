using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Station
{
    /// <summary>
    /// Immutable observations from upstream rules, not a diagnosis or a second medical system.
    /// Dirtiness uses a provisional 0..100 scale with no implied disease thresholds.
    /// </summary>
    public sealed class AnimalIntakeCondition
    {
        public StationHealthAssessment HealthAssessment { get; }

        public int Dirtiness { get; }

        public IReadOnlyList<string> KnownConditionIds { get; }

        public AnimalIntakeCondition(StationHealthAssessment healthAssessment, int dirtiness,
            IReadOnlyList<string> knownConditionIds)
        {
            if (healthAssessment < StationHealthAssessment.Unchecked ||
                healthAssessment > StationHealthAssessment.UrgentCareRequired)
            {
                throw new ArgumentOutOfRangeException(nameof(healthAssessment));
            }

            if (dirtiness < 0 || dirtiness > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(dirtiness));
            }

            if (knownConditionIds == null)
            {
                throw new ArgumentNullException(nameof(knownConditionIds));
            }

            var ids = new List<string>(knownConditionIds.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in knownConditionIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || !uniqueIds.Add(id))
                {
                    throw new ArgumentException("Condition IDs must be nonempty, trimmed and unique.",
                        nameof(knownConditionIds));
                }

                ids.Add(id);
            }

            HealthAssessment = healthAssessment;
            Dirtiness = dirtiness;
            KnownConditionIds = ids.AsReadOnly();
        }
    }
}
