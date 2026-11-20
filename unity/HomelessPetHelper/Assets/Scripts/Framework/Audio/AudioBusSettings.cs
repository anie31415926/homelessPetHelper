using System;

namespace HomelessPetHelper.Framework.Audio
{
    public readonly struct AudioBusSettings : IEquatable<AudioBusSettings>
    {
        public float Volume { get; }
        public bool IsMuted { get; }
        public float Gain => IsMuted ? 0 : Volume;

        public AudioBusSettings(float volume, bool isMuted = false)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0 || volume > 1)
                throw new ArgumentOutOfRangeException(nameof(volume), "Audio volume must be finite and within [0, 1].");
            Volume = volume;
            IsMuted = isMuted;
        }

        public bool Equals(AudioBusSettings other) => Volume == other.Volume && IsMuted == other.IsMuted;
        public override bool Equals(object value) => value is AudioBusSettings other && Equals(other);
        public override int GetHashCode() => (Volume.GetHashCode() * 397) ^ IsMuted.GetHashCode();
    }
}
