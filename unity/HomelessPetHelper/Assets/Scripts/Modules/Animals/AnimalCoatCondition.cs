using System;

namespace HomelessPetHelper.Modules.Animals
{
    /// <summary>Independent surface conditions, not health or disease risk.</summary>
    public sealed class AnimalCoatCondition
    {
        public int Dirtiness { get; }
        public int LooseFur { get; }

        public AnimalCoatCondition(int dirtiness, int looseFur)
        {
            if (dirtiness < 0 || dirtiness > 100 || looseFur < 0 || looseFur > 100)
                throw new ArgumentOutOfRangeException(nameof(dirtiness));
            Dirtiness = dirtiness;
            LooseFur = looseFur;
        }
    }
}
