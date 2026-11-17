using System;

namespace HomelessPetHelper.Editor.RescueCase
{
    public sealed class CaseAnimalSnapshot
    {
        public Guid AnimalId { get; }
        public bool IsHungry { get; }
        public bool HasSpace { get; }
        public bool HasFoodOnGround { get; }
        public CaseEmotion Emotion { get; }
        public CaseBehavior Behavior { get; }
        public CaseStress Stress { get; }

        internal CaseAnimalSnapshot(CaseAnimal animal)
        {
            AnimalId = animal.AnimalId;
            IsHungry = animal.IsHungry;
            HasSpace = animal.HasSpace;
            HasFoodOnGround = animal.HasFoodOnGround;
            Emotion = animal.Emotion;
            Behavior = animal.Behavior;
            Stress = animal.Stress;
        }
    }
}
