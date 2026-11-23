using System;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Modules.Rescue;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    /// <summary>Main owns selection and the single gameplay-pause reason, not the shared player or clips.</summary>
    public sealed class MainMusicSession : IDisposable
    {
        private readonly AudioService _audio;
        private readonly AudioCue[] _cues;
        private string _preparedCueId;
        private bool _gameplayPaused;
        public bool IsDisposed { get; private set; }
        public string DesiredCueId { get; private set; }
        public AudioPlayResult LastPlayResult { get; private set; } = AudioPlayResult.NotBound;

        public MainMusicSession(AudioService audio, AudioCatalog catalog)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _cues = catalog != null ? catalog.CreateCues() : Array.Empty<AudioCue>();
        }

        public void Refresh(RescueStage stage, bool gameplayPaused)
        {
            if (IsDisposed || _audio.IsDisposed) return;
            string desired = SelectCue(stage);
            if (DesiredCueId != desired) { DesiredCueId = desired; _preparedCueId = null; }
            if (_gameplayPaused != gameplayPaused)
            {
                _gameplayPaused = gameplayPaused;
                _audio.SetSuspended(AudioPauseReason.GameplayPaused, gameplayPaused);
            }
            if (_audio.CurrentMusicId == DesiredCueId) return;
            if (_audio.IsSuspended) { LastPlayResult = AudioPlayResult.Suspended; return; }
            PrepareSelectedClip();
            LastPlayResult = _audio.PlayMusic(DesiredCueId);
        }

        private void PrepareSelectedClip()
        {
            if (_preparedCueId == DesiredCueId) return;
            _preparedCueId = DesiredCueId;
            // Unity can unload a stopped stream despite preload=true. Prime only once per selection.
            foreach (var cue in _cues)
                if (cue.Id == DesiredCueId && cue.Clip != null && cue.Clip.loadState == AudioDataLoadState.Unloaded)
                    cue.Clip.LoadAudioData();
        }

        private static string SelectCue(RescueStage stage)
        {
            switch (stage)
            {
                case RescueStage.Map: return GameAudioCues.ExploreMusic;
                case RescueStage.Approach:
                case RescueStage.Negotiation:
                case RescueStage.Rescued: return GameAudioCues.RescueMusic;
                default: return GameAudioCues.StationMusic;
            }
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            if (_audio.IsDisposed) return;
            _audio.StopMusic();
            if (_gameplayPaused) _audio.SetSuspended(AudioPauseReason.GameplayPaused, false);
        }
    }
}
