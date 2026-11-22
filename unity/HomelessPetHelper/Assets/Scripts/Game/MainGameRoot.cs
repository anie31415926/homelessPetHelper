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
        [SerializeField] private Approach.ApproachPanelView _approach;
        [SerializeField] private AudioScope[] _audioScopes = Array.Empty<AudioScope>();
        public MainLoopPresenter Presenter { get; private set; }
        public MainAudioSession Audio { get; private set; }

        private ISaveStorage _progressStorage;
        private Func<int> _outingSeedSource;
        private bool _started;
        private ISaveStorage _audioStorage, _guidanceStorage, _playStorage;
        private bool _injectedPreferences;
        public Settings.PlaySettingsSession PlaySettings { get; private set; }
        public Guidance.GuidanceProgressSession Guidance { get; private set; }

        public void ConfigurePreferenceStorage(ISaveStorage audio, ISaveStorage guidance, ISaveStorage play = null)
        {
            if (_started) throw new InvalidOperationException("Configure UI storage before Start.");
            _audioStorage = audio ?? throw new ArgumentNullException(nameof(audio));
            _guidanceStorage = guidance ?? throw new ArgumentNullException(nameof(guidance));
            _playStorage = play;
            _injectedPreferences = true;
        }

        /// <summary>
        /// Explicit composition hook, called before Start. Tests use isolated durable or memory storage.
        /// </summary>
        public void ConfigureProgressStorage(ISaveStorage storage)
        {
            if (_started) throw new InvalidOperationException("Configure progress storage before Start.");
            _progressStorage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        /// <summary>Explicit randomness dependency for deterministic composition; configure before Start.</summary>
        public void ConfigureOutingSeedSource(Func<int> source)
        {
            if (_started) throw new InvalidOperationException("Configure outing seeds before Start.");
            _outingSeedSource = source ?? throw new ArgumentNullException(nameof(source));
        }

        private void Start()
        {
            _started = true;
            if (_bootstrap == null || _content == null || _station == null || _map == null ||
                _siege == null || _approach == null)
                throw new InvalidOperationException("Incomplete Main scene bindings.");
            Audio = new MainAudioSession(transform, _content.AudioCatalog, _audioStorage ?? CreateAudioStorage());
            PlaySettings = new Settings.PlaySettingsSession(_playStorage ??
                (_injectedPreferences ? null : CreatePlayStorage()));
            Guidance = new Guidance.GuidanceProgressSession(_guidanceStorage ?? CreateGuidanceStorage());
            foreach (AudioScope scope in _audioScopes)
            {
                if (scope == null) throw new InvalidOperationException("Missing Main audio scope.");
                scope.Bind(Audio.Playback);
            }
            Presenter = new MainLoopPresenter(_station, _map, _siege, _approach, _content, Audio.Playback,
                _progressStorage ?? CreateProgressStorage(), _outingSeedSource, Audio, Guidance, PlaySettings);
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

        private static ISaveStorage CreatePlayStorage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WeChatSaveStorage("homeless-pet.play.v1");
#else
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "play-preferences"));
#endif
        }

        private static ISaveStorage CreateGuidanceStorage()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new WeChatSaveStorage("homeless-pet.guidance.v1");
#else
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "guidance-progress"));
#endif
        }
    }
}
