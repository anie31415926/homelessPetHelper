using System;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio
{
    public sealed class AudioCue
    {
        public string Id { get; }
        public AudioChannel Channel { get; }
        public AudioClip Clip { get; }
        public AudioCuePolicy Policy { get; }

        public AudioCue(string id, AudioChannel channel, AudioClip clip, AudioCuePolicy policy = null)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new ArgumentException("A stable cue ID of at most 128 characters is required.", nameof(id));
            if (channel != AudioChannel.Music && channel != AudioChannel.Effects)
                throw new ArgumentOutOfRangeException(nameof(channel));
            Policy = policy ?? new AudioCuePolicy();
            if (channel == AudioChannel.Effects && Policy.Loop)
                throw new ArgumentException("Transient effect cues cannot loop.", nameof(policy));
            Id = id;
            Channel = channel;
            Clip = clip;
        }
    }
}
