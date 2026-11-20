using System;

namespace HomelessPetHelper.Framework.Audio
{
    public sealed class AudioPreferences : IEquatable<AudioPreferences>
    {
        public AudioBusSettings Master { get; }
        public AudioBusSettings Music { get; }
        public AudioBusSettings Effects { get; }
        public static AudioPreferences Default => new AudioPreferences(new AudioBusSettings(1),
            new AudioBusSettings(1), new AudioBusSettings(1));

        public AudioPreferences(AudioBusSettings master, AudioBusSettings music, AudioBusSettings effects)
        {
            Master = master;
            Music = music;
            Effects = effects;
        }

        public bool Equals(AudioPreferences other) => other != null && Master.Equals(other.Master) &&
            Music.Equals(other.Music) && Effects.Equals(other.Effects);
        public override bool Equals(object value) => value is AudioPreferences other && Equals(other);
        public override int GetHashCode() => ((Master.GetHashCode() * 397) ^ Music.GetHashCode()) * 397 ^
            Effects.GetHashCode();
    }
}
