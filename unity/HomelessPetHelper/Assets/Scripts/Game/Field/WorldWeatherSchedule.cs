using System;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>
    /// Deterministic world-weather timeline for the whole map. The sample is a pure function of the
    /// stable content seed and the persisted elapsed game minutes, so no extra save field is needed:
    /// restoring an outing at the same clock value reproduces the weather it departed with.
    /// Presentation only; battle rules, time gates and rewards never read this value.
    /// </summary>
    public static class WorldWeatherSchedule
    {
        /// <summary>Game minutes each weather block lasts; eight blocks fit one 1440-minute day.</summary>
        public const int BlockMinutes = 180;

        // Trial mix so the map is mostly fair weather and the wet sky is no longer the default.
        // These shares are playtest parameters, not a confirmed gameplay rule.
        private const int ClearShare = 55;
        private const int RainShare = 20;
        private const int SnowShare = 10;
        private const int FogShare = 15;
        private const int ClearLimit = ClearShare;
        private const int RainLimit = ClearLimit + RainShare;
        private const int SnowLimit = RainLimit + SnowShare;
        private const int FogLimit = SnowLimit + FogShare;

        private const int LightShare = 45;
        private const int MediumShare = 40;
        private const int MediumLimit = LightShare + MediumShare;

        private const ulong KindSalt = 0x8A5CD789635D2DFFUL;
        private const ulong IntensitySalt = 0x4F1BBCDCBFA3B9C7UL;

        /// <summary>Samples the block containing the given clock value.</summary>
        public static OutingWeather Sample(int seed, long elapsedMinutes, int minutesPerDay)
        {
            if (elapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinutes));
            if (minutesPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(minutesPerDay));
            return Sample(seed, elapsedMinutes / BlockMinutes);
        }

        /// <summary>Samples one weather block. Blocks are independent so gaps and dry spells alternate.</summary>
        public static OutingWeather Sample(int seed, long blockIndex)
        {
            if (blockIndex < 0) throw new ArgumentOutOfRangeException(nameof(blockIndex));
            int roll = (int)(Roll(seed, blockIndex, KindSalt) % 100u);
            OutingWeatherKind kind;
            if (roll < ClearLimit) kind = OutingWeatherKind.Clear;
            else if (roll < RainLimit) kind = OutingWeatherKind.Rain;
            else if (roll < SnowLimit) kind = OutingWeatherKind.Snow;
            else if (roll < FogLimit) kind = OutingWeatherKind.Fog;
            else throw new InvalidOperationException("Weather share table is out of range.");

            if (kind == OutingWeatherKind.Clear)
                return new OutingWeather(OutingWeatherKind.Clear, OutingWeatherIntensity.Light);
            int strength = (int)(Roll(seed, blockIndex, IntensitySalt) % 100u);
            var intensity = strength < LightShare ? OutingWeatherIntensity.Light :
                strength < MediumLimit ? OutingWeatherIntensity.Medium : OutingWeatherIntensity.Heavy;
            return new OutingWeather(kind, intensity);
        }

        /// <summary>SplitMix64 step; stable across runtimes, unlike seeded System.Random sequences.</summary>
        private static uint Roll(int seed, long blockIndex, ulong salt)
        {
            ulong value = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL;
            value += (ulong)blockIndex * 0xD1B54A32D192ED03UL;
            value ^= salt;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (uint)(value >> 32);
        }
    }
}
