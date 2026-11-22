using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.Animals.UI;

namespace HomelessPetHelper.Game.Station
{
    internal static class StationCoatFields
    {
        internal static DirtField Create(byte[] mask, int seed, bool fur, int amount)
        {
            int size = AnimalCoatPattern.Resolution;
            var samples = new byte[mask.Length];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    samples[y * size + x] = AnimalCoatPattern.Sample(x, y, seed, fur, amount);
            return new DirtField(size, size, mask, samples);
        }

        internal static DirtField Create(byte[] mask, int seed, bool fur, bool cleared) =>
            Create(mask, seed, fur, cleared ? 0 : 100);
    }
}
