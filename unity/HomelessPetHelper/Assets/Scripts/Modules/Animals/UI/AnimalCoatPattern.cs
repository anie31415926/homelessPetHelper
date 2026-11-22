using System;
using UnityEngine;

namespace HomelessPetHelper.Modules.Animals.UI
{
    /// <summary>Stable normalized coat marks shared by encounter views and the initial care fields.</summary>
    public static class AnimalCoatPattern
    {
        public const int Resolution = 128;
        public static int Seed(Guid animalId)
        {
            uint hash = 2166136261u;
            foreach (byte value in animalId.ToByteArray()) hash = unchecked((hash ^ value) * 16777619u);
            return (int)(hash & 1023u);
        }

        public static byte Sample(int x, int y, int seed, bool fur, int amount)
        {
            if (amount < 0 || amount > 100) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return 0;
            float value;
            if (fur)
            {
                float noise = Mathf.PerlinNoise(x * .07f + seed * 13, y * .07f + 43);
                value = (x + y * 2) % 11 < 2 && y % 9 < 6 ? Mathf.Clamp01((noise - .38f) * 4) * 200 : 0;
            }
            else value = Mathf.Clamp01((Mathf.PerlinNoise(x * .10f + seed * 19, y * .10f + 7) - .43f) * 5) * 220;
            return (byte)(value * amount / 100);
        }
    }
}
