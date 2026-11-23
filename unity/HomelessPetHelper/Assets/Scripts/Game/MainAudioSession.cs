using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Audio.Persistence;
using HomelessPetHelper.Framework.Persistence;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    /// <summary>Main owns this session. Applying preferences is memory-only; saving is explicit.</summary>
    public sealed class MainAudioSession : IDisposable
    {
        private readonly GameObject _root;
        private readonly AudioPreferencesStore _store;
        private readonly AudioService _service;
        private AudioPreferences _persisted;
        private bool _disposed;
        public AudioService Playback => _service;
        public SaveError PersistenceError { get; private set; }
        public bool HasUnsavedPreferences => !_service.Preferences.Equals(_persisted);

        public MainAudioSession(Transform parent, AudioCatalog catalog, ISaveStorage storage,
            AudioCatalog musicCatalog = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var cues = new List<AudioCue>();
            if (catalog != null) cues.AddRange(catalog.CreateCues());
            if (musicCatalog != null) cues.AddRange(musicCatalog.CreateCues());
            _store = new AudioPreferencesStore(storage);
            if (!_store.TryLoad(out var preferences, out var error))
            {
                PersistenceError = error;
                preferences = AudioPreferences.Default;
            }
            else _persisted = preferences;
            _root = new GameObject("GameAudio", typeof(AudioListener));
            _root.transform.SetParent(parent, false);
            try
            {
                var host = _root.AddComponent<AudioHost>();
                host.Initialize(cues, preferences);
                _service = host.Service;
            }
            catch
            {
                DestroyRoot();
                throw;
            }
        }

        public bool TrySavePreferences(out SaveError error)
        {
            RequireOpen();
            bool saved = _store.TrySave(_service.Preferences, out error);
            PersistenceError = error;
            if (saved) _persisted = _service.Preferences;
            return saved;
        }

        /// <summary>Explicitly discards pending in-memory preferences only after a successful disk reload.</summary>
        public bool TryReloadPreferences(out SaveError error)
        {
            RequireOpen();
            bool loaded = _store.TryLoad(out var preferences, out error);
            PersistenceError = error;
            if (!loaded) return false;
            _service.ApplyPreferences(preferences);
            _persisted = preferences;
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _service.Dispose();
            DestroyRoot();
        }

        private void DestroyRoot()
        {
            if (_root == null) return;
            _root.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }

        private void RequireOpen()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MainAudioSession));
        }
    }
}
