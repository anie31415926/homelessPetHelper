using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Framework.Audio
{
    /// <summary>
    /// Bounded, explicitly owned audio playback. No IO, asset loading, static instance, or per-frame work.
    /// Effects are discarded on suspension/mute; music keeps its position and resumes only after all reasons clear.
    /// Muted music requests may start silently so the current track remains correct when unmuted.
    /// </summary>
    public sealed class AudioService : IDisposable
    {
        private sealed class CueState
        {
            public AudioCue Cue;
            public double LastPlayedSeconds = double.NegativeInfinity;
        }

        private readonly Dictionary<string, CueState> _cues = new Dictionary<string, CueState>(StringComparer.Ordinal);
        private readonly IAudioOutput _output;
        private readonly Func<double> _nowSeconds;
        private readonly CueState[] _voices;
        private CueState _music;
        private AudioPauseReason _pauseReasons;
        private bool _disposed;

        public AudioPreferences Preferences { get; private set; }
        public bool IsSuspended => _pauseReasons != AudioPauseReason.None;
        public string CurrentMusicId => _music?.Cue.Id;
        public bool IsDisposed => _disposed;

        public AudioService(IAudioOutput output, IEnumerable<AudioCue> cues,
            AudioPreferences preferences, Func<double> nowSeconds)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _nowSeconds = nowSeconds ?? throw new ArgumentNullException(nameof(nowSeconds));
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            if (output.EffectVoiceCount < 1 || output.EffectVoiceCount > 32)
                throw new ArgumentOutOfRangeException(nameof(output));
            if (cues == null) throw new ArgumentNullException(nameof(cues));
            foreach (var cue in cues)
            {
                if (cue == null) throw new ArgumentException("Audio cues cannot contain null.", nameof(cues));
                _cues.Add(cue.Id, new CueState { Cue = cue });
            }
            _voices = new CueState[output.EffectVoiceCount];
        }

        public AudioPlayResult PlayMusic(string id)
        {
            AudioPlayResult result = CheckCue(id, AudioChannel.Music, out var state);
            if (result != AudioPlayResult.Played) return result;
            if (ReferenceEquals(_music, state) && _output.IsMusicPlaying) return AudioPlayResult.AlreadyPlaying;
            _output.PlayMusic(state.Cue.Clip, state.Cue.Policy.Loop, MusicGain(state.Cue));
            _music = state;
            return AudioPlayResult.Played;
        }

        public AudioPlayResult PlayEffect(string id)
        {
            AudioPlayResult result = CheckCue(id, AudioChannel.Effects, out var state);
            if (result != AudioPlayResult.Played) return result;
            float gain = EffectGain(state.Cue);
            if (gain <= 0) return AudioPlayResult.Muted;
            double now = _nowSeconds();
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0)
                throw new InvalidOperationException("Audio requires a finite nonnegative monotonic clock.");
            if (now - state.LastPlayedSeconds < state.Cue.Policy.MinIntervalSeconds) return AudioPlayResult.Cooldown;
            int voice = FindEffectVoice(state);
            if (voice < 0) return AudioPlayResult.VoiceLimit;
            _output.PlayEffect(voice, state.Cue.Clip, gain);
            _voices[voice] = state;
            state.LastPlayedSeconds = now;
            return AudioPlayResult.Played;
        }

        public void ApplyPreferences(AudioPreferences preferences)
        {
            RequireOpen();
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            if (_music != null) _output.SetMusicGain(MusicGain(_music.Cue));
            if (Preferences.Master.Gain * Preferences.Effects.Gain <= 0) { StopEffects(); return; }
            for (int voice = 0; voice < _voices.Length; voice++)
                if (_voices[voice] != null) _output.SetEffectGain(voice, EffectGain(_voices[voice].Cue));
        }

        public void SetSuspended(AudioPauseReason reason, bool suspended)
        {
            RequireOpen();
            const AudioPauseReason allowed = AudioPauseReason.ApplicationPaused | AudioPauseReason.FocusLost |
                AudioPauseReason.OwnerDisabled | AudioPauseReason.ExternalInterruption |
                AudioPauseReason.GameplayPaused;
            if (reason == AudioPauseReason.None || (reason & ~allowed) != 0)
                throw new ArgumentOutOfRangeException(nameof(reason));
            bool wasSuspended = IsSuspended;
            _pauseReasons = suspended ? _pauseReasons | reason : _pauseReasons & ~reason;
            if (wasSuspended == IsSuspended) return;
            if (IsSuspended) StopEffects();
            _output.SetMusicPaused(IsSuspended);
        }

        public void StopMusic()
        {
            RequireOpen();
            _output.StopMusic();
            _music = null;
        }

        public void StopEffects()
        {
            RequireOpen();
            for (int voice = 0; voice < _voices.Length; voice++)
            {
                _output.StopEffect(voice);
                _voices[voice] = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopMusic();
            StopEffects();
            _disposed = true;
            _cues.Clear();
            _output.Dispose();
        }

        private int FindEffectVoice(CueState state)
        {
            int count = 0;
            int free = -1;
            for (int voice = 0; voice < _voices.Length; voice++)
            {
                if (_voices[voice] != null && _output.IsEffectPlaying(voice))
                {
                    if (ReferenceEquals(_voices[voice], state)) count++;
                }
                else if (free < 0) free = voice;
            }
            return count >= state.Cue.Policy.MaxVoices ? -1 : free;
        }

        private AudioPlayResult CheckCue(string id, AudioChannel channel, out CueState state)
        {
            state = null;
            if (_disposed) return AudioPlayResult.Disposed;
            if (id == null || !_cues.TryGetValue(id, out state)) return AudioPlayResult.UnknownCue;
            if (state.Cue.Channel != channel) return AudioPlayResult.WrongChannel;
            if (state.Cue.Clip == null) return AudioPlayResult.MissingClip;
            if (state.Cue.Clip.loadState != UnityEngine.AudioDataLoadState.Loaded) return AudioPlayResult.ClipNotReady;
            return IsSuspended ? AudioPlayResult.Suspended : AudioPlayResult.Played;
        }

        private float MusicGain(AudioCue cue) => Preferences.Master.Gain * Preferences.Music.Gain * cue.Policy.Gain;
        private float EffectGain(AudioCue cue) => Preferences.Master.Gain * Preferences.Effects.Gain * cue.Policy.Gain;
        private void RequireOpen()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AudioService));
        }
    }
}
