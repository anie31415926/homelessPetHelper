using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station
{
    internal sealed class StationCareRecord
    {
        internal StationCareSnapshot Current { get; private set; }

        internal StationCareRecord(StationAnimalIntake arrival)
        {
            Current = new StationCareSnapshot(arrival.AnimalId, arrival.OutingId,
                StationHealthAssessment.Unchecked, Array.Empty<StationCareProblem>());
        }

        internal bool TryCompleteBasicAssessment(IReadOnlyList<string> necessaryProblemIds,
            AnimalHealthSnapshot healthAtAssessment, out StationFailure failure)
        {
            failure = StationFailure.None;
            if (Current.BasicAssessment != StationHealthAssessment.Unchecked)
            {
                failure = StationFailure.BasicAssessmentAlreadyCompleted;
                return false;
            }

            if (necessaryProblemIds == null)
            {
                failure = StationFailure.InvalidCareAssessment;
                return false;
            }

            var problems = new List<StationCareProblem>(necessaryProblemIds.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in necessaryProblemIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim() || !uniqueIds.Add(id))
                {
                    failure = StationFailure.InvalidCareAssessment;
                    return false;
                }

                problems.Add(new StationCareProblem(id, ProfessionalCareStatus.AwaitingConfirmation));
            }

            var assessment = problems.Count == 0 ? StationHealthAssessment.NoUrgentCareRequired :
                StationHealthAssessment.UrgentCareRequired;
            Current = new StationCareSnapshot(Current.AnimalId, Current.OutingId, assessment, problems,
                healthAtAssessment);
            return true;
        }

        internal bool TryAdvanceProblem(string problemId, ProfessionalCareStatus expected,
            ProfessionalCareStatus next, out StationFailure failure)
        {
            failure = StationFailure.None;
            if (Current.BasicAssessment == StationHealthAssessment.Unchecked)
            {
                failure = StationFailure.BasicAssessmentRequired;
                return false;
            }

            var problems = new List<StationCareProblem>(Current.NecessaryProblems);
            int index = problems.FindIndex(problem => problem.Id == problemId);
            if (index < 0)
            {
                failure = StationFailure.CareProblemNotFound;
                return false;
            }

            if (problems[index].Status != expected)
            {
                failure = StationFailure.UnexpectedCareStatus;
                return false;
            }

            problems[index] = new StationCareProblem(problemId, next);
            Current = new StationCareSnapshot(Current.AnimalId, Current.OutingId, Current.BasicAssessment, problems,
                Current.HealthAtAssessment);
            return true;
        }
    }
}
