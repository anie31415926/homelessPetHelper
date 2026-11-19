using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Editor.RescueCase
{
    /// <summary>Explicit initial facts for this healthy sample, not defaults for all rescued animals.</summary>
    public static class RescueCaseAnimalFixture
    {
        public static AnimalIndividual Create(CaseStart start)
        {
            if (start != CaseStart.Normal && start != CaseStart.StressRecovery)
                throw new ArgumentOutOfRangeException(nameof(start));
            bool stress = start == CaseStart.StressRecovery;
            return new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(!stress, stress ? AnimalEmotion.Startled : AnimalEmotion.Nervous,
                    stress ? AnimalStress.Acute : AnimalStress.None,
                    stress ? AnimalBehavior.Retreating : AnimalBehavior.Watching),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()),
                new AnimalCarePermissions(true, true, true), new AnimalCoatCondition(65, 50));
        }
    }
}
