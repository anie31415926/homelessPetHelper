using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Animals
{
    /// <summary>
    /// Explicit health facts supplied at first appearance. An empty collection must be intentional.
    /// No diagnosis, random generation, recovery or reassessment algorithm is implemented.
    /// </summary>
    public sealed class AnimalHealthSnapshot
    {
        public IReadOnlyList<AnimalHealthProblem> Problems { get; }

        public AnimalHealthSnapshot(IReadOnlyList<AnimalHealthProblem> problems)
        {
            if (problems == null)
            {
                throw new ArgumentNullException(nameof(problems));
            }

            var copy = new List<AnimalHealthProblem>(problems.Count);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (AnimalHealthProblem problem in problems)
            {
                if (problem == null || !ids.Add(problem.InstanceId))
                {
                    throw new ArgumentException("Health problems must be nonnull with unique instance IDs.",
                        nameof(problems));
                }

                copy.Add(problem);
            }

            Problems = copy.AsReadOnly();
        }
    }
}
