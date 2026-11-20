using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Modules.Station.Cleaning
{
    public sealed class StationCoatCareState
    {
        private readonly DirtField _dirt, _fur;
        public DirtField CopyDirt() => _dirt.Copy();
        public DirtField CopyFur() => _fur.Copy();
        public AnimalCoatCondition InitialCoat { get; }
        public int WashMinutes { get; }
        public int GroomMinutes { get; }
        public long WashSettledMinutes { get; }
        public long GroomSettledMinutes { get; }

        public StationCoatCareState(DirtField dirt, DirtField fur, AnimalCoatCondition initialCoat,
            int washMinutes, int groomMinutes, long washSettledMinutes, long groomSettledMinutes)
        {
            _dirt = (dirt ?? throw new ArgumentNullException(nameof(dirt))).Copy();
            _fur = (fur ?? throw new ArgumentNullException(nameof(fur))).Copy();
            InitialCoat = initialCoat ?? throw new ArgumentNullException(nameof(initialCoat));
            WashMinutes = washMinutes;
            GroomMinutes = groomMinutes;
            WashSettledMinutes = washSettledMinutes;
            GroomSettledMinutes = groomSettledMinutes;
        }
    }
}
