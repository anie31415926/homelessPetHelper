using HomelessPetHelper.Modules.Station.Cleaning;
using UnityEngine;

namespace HomelessPetHelper.Game.Station
{
    internal static class StationCoatFields
    {
        internal static DirtField Create(byte[] mask, int seed, bool fur, bool cleared)
        {
            var samples = new byte[mask.Length];
            if (!cleared)
            {
                for (int y = 0; y < 128; y++)
                {
                    for (int x = 0; x < 128; x++)
                    {
                        if (fur)
                        {
                            float noise = Mathf.PerlinNoise(x * 0.07f + seed * 13, y * 0.07f + 43);
                            samples[y * 128 + x] = (x + y * 2) % 11 < 2 && y % 9 < 6 ?
                                (byte)(Mathf.Clamp01((noise - 0.38f) * 4) * 200) : (byte)0;
                        }
                        else
                        {
                            float noise = Mathf.PerlinNoise(x * 0.10f + seed * 19, y * 0.10f + 7);
                            samples[y * 128 + x] = (byte)(Mathf.Clamp01((noise - 0.43f) * 5) * 220);
                        }
                    }
                }
            }
            return new DirtField(128, 128, mask, samples);
        }
    }
}
