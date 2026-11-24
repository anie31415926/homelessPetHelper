using System;
using UnityEngine;

namespace HomelessPetHelper.Modules.Animals.UI
{
    /// <summary>Read-only presentation channels projected from the shared individual snapshot.</summary>
    public readonly struct AnimalExpression
    {
        public float TailRaise { get; }
        public float TailSwish { get; }

        public AnimalExpression(float tailRaise, float tailSwish)
        {
            if (float.IsNaN(tailRaise) || tailRaise < 0 || tailRaise > 1 ||
                float.IsNaN(tailSwish) || tailSwish < 0 || tailSwish > 1)
                throw new ArgumentOutOfRangeException(nameof(tailRaise));
            TailRaise = tailRaise;
            TailSwish = tailSwish;
        }

        public static AnimalExpression From(AnimalSnapshot animal)
        {
            if (animal == null) return default;
            var state = animal.Disposition;
            // Distress takes priority; a saved positive emotion must not hide acute stress.
            if (state.Stress == AnimalStress.Acute || state.Emotion == AnimalEmotion.Irritated)
                return new AnimalExpression(0, 1);
            return new AnimalExpression(state.Emotion == AnimalEmotion.Happy ? 1 : 0, 0);
        }

        public AnimalExpression MoveTowards(AnimalExpression target, float step) => new AnimalExpression(
            Mathf.MoveTowards(TailRaise, target.TailRaise, step),
            Mathf.MoveTowards(TailSwish, target.TailSwish, step));
    }
}
