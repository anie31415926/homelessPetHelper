using System;
using System.IO;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Framework.Persistence.WeChat;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    [DisallowMultipleComponent]
    public sealed class MainGameRoot : MonoBehaviour
    {
        [SerializeField] private GameBootstrap _bootstrap;
        [SerializeField] private GameContent _content;
        [SerializeField] private StationPanelView _station;
        [SerializeField] private CityMapView _map;
        [SerializeField] private SiegeTrialView _siege;
        [SerializeField] private AudioScope[] _audioScopes = Array.Empty<AudioScope>();
        public MainLoopPresenter Presenter { get; private set; }
        public MainAudioSession Audio { get; private set; }

        private ISaveStorage _progressStorage;
        private bool _started;

        /// <summary>
        /// Explicit composition hook, called before Start. Tests use isolated durable or memory storage.
        /// </summary>
        public void ConfigureProgressStorage(ISaveStorage storage)
        {
            if (_started) throw new InvalidOperationException("Configure progress storage before Start.");
            _progressStorage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        private void Start()
        {
            _started = true;
            if (_bootstrap == null || _content == null || _station == null || _map == null || _siege == null)
                throw new InvalidOperationException("Incomplete Main scene bindings.");
            Audio = new MainAudioSession(transform, _content.AudioCatalog, CreateAudioStorage());
            foreach (AudioScope scope in _audioScopes)
            {
                if (scope == null) throw new InvalidOperationException("Missing Main audio scope.");
                scope.Bind(Audio.Playback);
            }
            Presenter = new MainLoopPresenter(_station, _map, _siege, _content, Audio.Playback,
                _progressStorage ?? CreateProgressStorage());
            _bootstrap.BindSession(Presenter.Session);
            if (Audio.PersistenceError != null)
                Debug.LogWarning("Audio preferences are session-only; original save is preserved. " +
                    Audio.PersistenceError, this);
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.BindSession(null);
            Presenter?.Dispose();
            Presenter = null;
            foreach (AudioScope scope in _audioScopes)
                if (scope != null) scope.Unbind();
            Audio?.Dispose();
            Audio = null;
        }

        private static ISaveStorage CreateProgressStorage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WeChatSaveStorage("homeless-pet.main.v1");
#else
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "main-progress"));
#endif
        }

        private static ISaveStorage CreateAudioStorage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WeChatSaveStorage("homeless-pet.audio.v1");
#else
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "audio-preferences"));
#endif
        }
    }
}
