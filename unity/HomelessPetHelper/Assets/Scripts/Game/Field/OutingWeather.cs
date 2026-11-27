using System;

namespace HomelessPetHelper.Game.Field
{
    public enum OutingWeatherKind { Clear, Rain, Snow, Fog }
    public enum OutingWeatherIntensity { Light, Medium, Heavy }

    /// <summary>
    /// One world weather sample. The current value is derived by <see cref="WorldWeatherSchedule"/>
    /// from the action clock; an outing keeps the sample it departed with.
    /// </summary>
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
    }
}
