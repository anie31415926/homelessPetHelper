using System;
using System.Collections.Generic;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioHost : MonoBehaviour
    {
        private bool _applicationPaused;
        private bool _hasFocus;
        public AudioService Service { get; private set; }

        private void Awake() => OnApplicationFocus(Application.isFocused);

        /// <summary>The caller owns the host GameObject and the scene's single AudioListener.</summary>
        public void Initialize(IEnumerable<AudioCue> cues, AudioPreferences preferences)
        {
            if (Service != null) throw new InvalidOperationException("Audio host is already initialized.");
            var output = new UnityAudioOutput(transform);
            try
            {
                Service = new AudioService(output, cues, preferences, () => Time.realtimeSinceStartupAsDouble);
            }
            catch
            {
                output.Dispose();
                throw;
            }
            Service.SetSuspended(AudioPauseReason.ApplicationPaused, _applicationPaused);
            Service.SetSuspended(AudioPauseReason.FocusLost, !_hasFocus);
            Service.SetSuspended(AudioPauseReason.OwnerDisabled, !isActiveAndEnabled);
        }

        private void OnEnable() => SetReason(AudioPauseReason.OwnerDisabled, false);
        private void OnDisable() => SetReason(AudioPauseReason.OwnerDisabled, true);

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            SetReason(AudioPauseReason.ApplicationPaused, paused);
        }

        private void OnApplicationFocus(bool focused)
        {
            _hasFocus = focused;
            SetReason(AudioPauseReason.FocusLost, !focused);
        }

        private void OnDestroy() => Service?.Dispose();

        private void SetReason(AudioPauseReason reason, bool active)
        {
            if (Service != null && !Service.IsDisposed) Service.SetSuspended(reason, active);
        }
    }
}
