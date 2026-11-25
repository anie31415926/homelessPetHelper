using System;

namespace HomelessPetHelper.Game.Field
{
    public enum OutingWeatherKind { Clear, Rain, Snow, Fog }
    public enum OutingWeatherIntensity { Light, Medium, Heavy }

    /// <summary>Initial field conditions; independent of the effect clock and battle rules.</summary>
    public readonly struct OutingWeather
    {
        public OutingWeatherKind Kind { get; }
        public OutingWeatherIntensity Intensity { get; }

        public OutingWeather(OutingWeatherKind kind, OutingWeatherIntensity intensity)
        {
            if ((uint)kind > (uint)OutingWeatherKind.Fog)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if ((uint)intensity > (uint)OutingWeatherIntensity.Heavy)
                throw new ArgumentOutOfRangeException(nameof(intensity));
            Kind = kind;
            Intensity = kind == OutingWeatherKind.Clear ? OutingWeatherIntensity.Light : intensity;
        }

        // One fixed world sample until weather scheduling is designed.
        internal static OutingWeather InitialWorld =>
            new OutingWeather(OutingWeatherKind.Rain, OutingWeatherIntensity.Light);
    }
}
