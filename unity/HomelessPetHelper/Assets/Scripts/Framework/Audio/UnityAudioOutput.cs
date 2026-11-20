using System;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio
{
    /// <summary>Owns fixed 2D sources. Does not touch global listener volume/pause or unload clips.</summary>
    public sealed class UnityAudioOutput : IAudioOutput
    {
        private readonly GameObject _root;
        private readonly AudioSource _music;
        private readonly AudioSource[] _effects;
        private bool _resumeMusic;
        private int _pausedSample;
        private bool _musicStarted;
        private double _startedAtSeconds;
        private bool _disposed;
        public int EffectVoiceCount => _effects.Length;
        public bool IsMusicPlaying => _music != null && _music.isPlaying;

        public UnityAudioOutput(Transform parent, int effectVoiceCount = 8)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (effectVoiceCount < 1 || effectVoiceCount > 32)
                throw new ArgumentOutOfRangeException(nameof(effectVoiceCount));
            _root = new GameObject("AudioOutput");
            _root.transform.SetParent(parent, false);
            _music = CreateSource(64);
            _effects = new AudioSource[effectVoiceCount];
            for (int voice = 0; voice < _effects.Length; voice++) _effects[voice] = CreateSource(128);
        }

        public bool IsEffectPlaying(int voice) => _effects[voice] != null && _effects[voice].isPlaying;

        public void PlayMusic(AudioClip clip, bool loop, float gain)
        {
            _music.Stop();
            _resumeMusic = false;
            _music.clip = clip;
            _music.loop = loop;
            _music.volume = gain;
            _music.Play();
            _musicStarted = true;
            _startedAtSeconds = Time.realtimeSinceStartupAsDouble;
        }

        public void SetMusicGain(float gain) => _music.volume = gain;

        public void SetMusicPaused(bool paused)
        {
            if (paused)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - _startedAtSeconds;
                _resumeMusic = _musicStarted && _music.clip != null &&
                    (_music.loop || elapsed < _music.clip.length);
                if (_resumeMusic)
                {
                    _pausedSample = _music.isPlaying ? _music.timeSamples :
                        (int)((elapsed * _music.clip.frequency) % _music.clip.samples);
                    _music.Pause();
                }
            }
            else if (_resumeMusic)
            {
                _music.UnPause();
                // Deactivating the owner also disables AudioSources, which may stop rather than pause them.
                if (!_music.isPlaying && _music.clip != null)
                {
                    _music.Play();
                    _music.timeSamples = Mathf.Clamp(_pausedSample, 0, _music.clip.samples - 1);
                }
                _startedAtSeconds = Time.realtimeSinceStartupAsDouble -
                    (double)_pausedSample / _music.clip.frequency;
                _resumeMusic = false;
            }
        }

        public void StopMusic()
        {
            _resumeMusic = false;
            _musicStarted = false;
            if (_music == null) return;
            _music.Stop();
            _music.clip = null;
        }

        public void PlayEffect(int voice, AudioClip clip, float gain)
        {
            var source = _effects[voice];
            source.Stop();
            source.clip = clip;
            source.volume = gain;
            source.Play();
        }

        public void SetEffectGain(int voice, float gain) => _effects[voice].volume = gain;

        public void StopEffect(int voice)
        {
            if (_effects[voice] == null) return;
            _effects[voice].Stop();
            _effects[voice].clip = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_music != null) StopMusic();
            for (int voice = 0; voice < _effects.Length; voice++)
                if (_effects[voice] != null) StopEffect(voice);
            if (_root != null)
            {
                _root.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
                else UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private AudioSource CreateSource(int priority)
        {
            var source = _root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.pitch = 1;
            source.priority = priority;
            return source;
        }
    }
}
