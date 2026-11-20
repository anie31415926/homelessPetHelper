using System;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio.UI
{
    /// <summary>Local presentation dependency. The composition root owns and binds the player.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Audio/Audio Scope")]
    public sealed class AudioScope : MonoBehaviour
    {
        private AudioService _audio;
        public bool IsBound => _audio != null && !_audio.IsDisposed;

        public void Bind(AudioService audio)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        }

        public void Unbind() => _audio = null;

        public AudioPlayResult TryPlayEffect(string cueId) => _audio != null
            ? _audio.PlayEffect(cueId) : AudioPlayResult.NotBound;

        // Void entry point is available to Inspector UnityEvents, including animation/UI feedback.
        public void Play(string cueId) => TryPlayEffect(cueId);

        private void OnDestroy() => Unbind();
    }
}
