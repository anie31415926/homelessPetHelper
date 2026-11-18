using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Editor.RescueCase
{
    public sealed class CaseAnimalSnapshot
    {
        public Guid AnimalId { get; }
        public bool IsHungry { get; }
        public bool HasSpace { get; }
        public bool HasFoodOnGround { get; }
        public bool IsWithinReach { get; }
        public CasePlacement Placement { get; }
        public CaseEmotion Emotion { get; }
        public CaseBehavior Behavior { get; }
        public CaseStress Stress { get; }
        public AnimalSnapshot Individual { get; }

        internal CaseAnimalSnapshot(CaseAnimal animal)
        {
            AnimalId = animal.AnimalId;
            IsHungry = animal.IsHungry;
            HasSpace = animal.HasSpace;
            HasFoodOnGround = animal.HasFoodOnGround;
            IsWithinReach = animal.IsWithinReach;
            Placement = animal.Placement;
            Emotion = animal.Emotion;
            Behavior = animal.Behavior;
            Stress = animal.Stress;
            Individual = animal.Individual.Current;
        }
    }
}
