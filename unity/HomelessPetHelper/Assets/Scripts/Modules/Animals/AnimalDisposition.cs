using System;

namespace HomelessPetHelper.Modules.Animals
{
    /// <summary>Immutable individual facts, excluding food on the ground, space and rescue equipment.</summary>
    public sealed class AnimalDisposition
    {
        public bool IsHungry { get; }
        public AnimalEmotion Emotion { get; }
        public AnimalStress Stress { get; }
        public AnimalBehavior Behavior { get; }

        public AnimalDisposition(bool isHungry, AnimalEmotion emotion, AnimalStress stress, AnimalBehavior behavior)
        {
            if (!Enum.IsDefined(typeof(AnimalEmotion), emotion) ||
                !Enum.IsDefined(typeof(AnimalStress), stress) ||
                !Enum.IsDefined(typeof(AnimalBehavior), behavior))
            {
                throw new ArgumentOutOfRangeException(nameof(emotion), "All disposition categories must be defined.");
            }

            IsHungry = isHungry;
            Emotion = emotion;
            Stress = stress;
            Behavior = behavior;
        }
    }
}
