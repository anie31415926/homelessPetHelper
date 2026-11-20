using System;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio
{
    /// <summary>Owned by AudioService. Gameplay-thread calls only; clips are borrowed, not unloaded.</summary>
    public interface IAudioOutput : IDisposable
    {
        int EffectVoiceCount { get; }
        bool IsMusicPlaying { get; }
        bool IsEffectPlaying(int voice);
        void PlayMusic(AudioClip clip, bool loop, float gain);
        void SetMusicGain(float gain);
        void SetMusicPaused(bool paused);
        void StopMusic();
        void PlayEffect(int voice, AudioClip clip, float gain);
        void SetEffectGain(int voice, float gain);
        void StopEffect(int voice);
    }
}
