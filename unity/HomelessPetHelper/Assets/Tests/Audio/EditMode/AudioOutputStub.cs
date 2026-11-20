using HomelessPetHelper.Framework.Audio;
using UnityEngine;

namespace HomelessPetHelper.Tests.Audio.EditMode
{
    internal sealed class AudioOutputStub : IAudioOutput
    {
        public readonly bool[] Effects;
        public readonly float[] EffectGains;
        public int MusicStarts, EffectStarts, MusicPauseChanges, DisposalCount;
        public float MusicGain;
        public bool MusicPlaying, Paused;
        private bool _resume;
        public int EffectVoiceCount => Effects.Length;
        public bool IsMusicPlaying => MusicPlaying;

        public AudioOutputStub(int voiceCount = 2)
        {
            Effects = new bool[voiceCount];
            EffectGains = new float[voiceCount];
        }

        public bool IsEffectPlaying(int voice) => Effects[voice];
        public void PlayMusic(AudioClip clip, bool loop, float gain)
        {
            MusicStarts++;
            MusicPlaying = true;
            _resume = false;
            MusicGain = gain;
        }
        public void SetMusicGain(float gain) => MusicGain = gain;
        public void SetMusicPaused(bool paused)
        {
            MusicPauseChanges++;
            Paused = paused;
            if (paused) { _resume = MusicPlaying; MusicPlaying = false; }
            else { MusicPlaying = _resume; _resume = false; }
        }
        public void StopMusic() { MusicPlaying = false; _resume = false; }
        public void PlayEffect(int voice, AudioClip clip, float gain)
        {
            EffectStarts++;
            Effects[voice] = true;
            EffectGains[voice] = gain;
        }
        public void SetEffectGain(int voice, float gain) => EffectGains[voice] = gain;
        public void StopEffect(int voice) => Effects[voice] = false;
        public void Dispose() => DisposalCount++;
    }
}
