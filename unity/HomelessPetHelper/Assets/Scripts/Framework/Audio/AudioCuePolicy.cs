using System;

namespace HomelessPetHelper.Framework.Audio
{
    public sealed class AudioCuePolicy
    {
        public float Gain { get; }
        public int MaxVoices { get; }
        public double MinIntervalSeconds { get; }
        public bool Loop { get; }

        public AudioCuePolicy(float gain = 1, int maxVoices = 2, double minIntervalSeconds = 0.05, bool loop = false)
        {
            Gain = new AudioBusSettings(gain).Volume;
            if (maxVoices < 1 || maxVoices > 32) throw new ArgumentOutOfRangeException(nameof(maxVoices));
            if (double.IsNaN(minIntervalSeconds) || double.IsInfinity(minIntervalSeconds) ||
                minIntervalSeconds < 0 || minIntervalSeconds > 60)
                throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds));
            MaxVoices = maxVoices;
            MinIntervalSeconds = minIntervalSeconds;
            Loop = loop;
        }
    }
}
