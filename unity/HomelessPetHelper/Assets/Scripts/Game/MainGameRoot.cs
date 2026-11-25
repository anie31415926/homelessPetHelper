using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Framework.Persistence.WeChat;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using UnityEngine;

namespace HomelessPetHelper.Game
{
    [DisallowMultipleComponent]
    public sealed class MainGameRoot : MonoBehaviour
    {
        private const int SaveSlotCount = 3;
        private const float SavingIndicatorSeconds = 0.45f;
        private const int SavingIndicatorSteps = 3;
        private enum SlotState { Empty, Occupied, Unavailable }

        private Func<int, ISaveStorage> _saveSlotStorage = CreateProgressStorage;
        private LevelOverlaySession _titleOverlays;
        private Coroutine _manualSave;
        private OptionPreviewText _saveText;
        private int _activeSlot;

        [SerializeField] private GameBootstrap _bootstrap;
        [SerializeField] private GameContent _content;
        [SerializeField] private StationPanelView _station;
        [SerializeField] private CityMapView _map;
        [SerializeField] private OutingWeatherGraphic _mapWeather;
        [SerializeField] private SiegeTrialView _siege;
        [SerializeField] private Approach.ApproachPanelView _approach;
        [SerializeField] private CityMap.Travel.MapTravelView _travel;
        [SerializeField] private AudioScope[] _audioScopes = Array.Empty<AudioScope>();
        public MainLoopPresenter Presenter { get; private set; }
        public MainAudioSession Audio { get; private set; }
        public MainMusicSession Music { get; private set; }

        private ISaveStorage _progressStorage;
        private bool _showOpening = true;
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
        public void ConfigureProgressStorage(ISaveStorage storage, bool showOpening = true)
        {
            if (_started) throw new InvalidOperationException("Configure progress storage before Start.");
            _progressStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            _showOpening = showOpening;
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
            Audio = new MainAudioSession(transform, _content.AudioCatalog, _audioStorage ?? CreateAudioStorage(),
                _content.MusicCatalog);
            PlaySettings = new Settings.PlaySettingsSession(_playStorage ??
                (_injectedPreferences ? null : CreatePlayStorage()));
            Guidance = new Guidance.GuidanceProgressSession(_guidanceStorage ?? CreateGuidanceStorage());
            foreach (AudioScope scope in _audioScopes)
            {
                if (scope == null) throw new InvalidOperationException("Missing Main audio scope.");
                scope.Bind(Audio.Playback);
            }
            if (_progressStorage != null) StartGame(_progressStorage);
            else ShowSaveMenu();
            Music = new MainMusicSession(Audio.Playback, _content.MusicCatalog);
            RefreshMusic();
            InvokeRepeating(nameof(SaveFieldProgress), 5f, 5f);
            if (Audio.PersistenceError != null)
                Debug.LogWarning("Audio preferences are session-only; original save is preserved. " +
                    Audio.PersistenceError, this);
        }

        private void Update()
        {
            RefreshMusic();
            if (Application.isFocused) Presenter?.PresentPendingRescueNotice();
        }

        private void RefreshMusic()
        {
            if (Presenter == null || Music == null) return;
            var session = Presenter.Session;
            // UI overlays (pause/settings/objectives) suspend gameplay but not audio: music and effects
            // keep playing so volume changes are audible. Only a broken/absent session stays silent;
            // application-level focus/background/interruption pauses are owned by AudioHost.
            bool gameplayPaused = !Presenter.Progress.CanContinue || session == null;
            Music.Refresh(session?.Flow.Stage ?? Modules.Rescue.RescueStage.Station, gameplayPaused);
        }

        private void OnDestroy()
        {
            Music?.Dispose();
            Music = null;
            CancelInvoke(nameof(SaveFieldProgress));
            Presenter?.FlushFieldProgress();
            if (_bootstrap != null) _bootstrap.BindSession(null);
            CloseSaveMenu();
            Presenter?.Dispose();
            Presenter = null;
            foreach (AudioScope scope in _audioScopes)
                if (scope != null) scope.Unbind();
            Audio?.Dispose();
            Audio = null;
        }

        private void SaveFieldProgress() => Presenter?.FlushFieldProgress();

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveFieldProgress();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveFieldProgress();
        }

        private static ISaveStorage CreateProgressStorage(int slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            const string key = "homeless-pet.main.v1";
            return new WeChatSaveStorage(slot == 0 ? key : key + ".profile-" + slot);
#else
            const string folder = "main-progress";
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath,
                slot == 0 ? folder : folder + "-profile-" + slot));
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

        public void ConfigureSaveSlots(Func<int, ISaveStorage> storage)
        {
            if (_started) throw new InvalidOperationException("Configure save slots before Start.");
            _saveSlotStorage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        private OptionPreviewText SaveText => _saveText ??= new OptionPreviewText(_content.StationText);

        private bool StartGame(ISaveStorage storage, bool? expectedNewGame = null)
        {
            Presenter = new MainLoopPresenter(_station, _map, _siege, _approach, _content, Audio.Playback,
                storage, _outingSeedSource, Audio, Guidance, PlaySettings, _travel, _showOpening, _mapWeather,
                expectedNewGame.HasValue ? (Action)ShowStationSaveMenu : null, expectedNewGame);
            if (expectedNewGame.HasValue && !Presenter.AcceptedSaveSelection)
            {
                Presenter.Dispose();
                Presenter = null;
                return false;
            }
            _bootstrap.BindSession(Presenter.Session);
            RefreshMusic();
            return true;
        }

        private void ShowSaveMenu(string feedback = null)
        {
            _map.gameObject.SetActive(false);
            _siege.gameObject.SetActive(false);
            _approach.gameObject.SetActive(false);
            _station.gameObject.SetActive(true);
            _station.ApplyFont(_content.Font);
            _station.ShowPage(false, false, false);
            var target = _station.MenuTarget;
            _titleOverlays ??= new LevelOverlaySession(_station.GetComponent<LevelOverlayAnchor>().View,
                new LevelOverlayBindings(_station.SetOverlaySuspended, () => { }, () => string.Empty,
                    () => null, (overlay, guide) => _station.BindOverlay(overlay),
                    (action, targets) => false, target, target),
                Audio, null, _content.CreateLevelUiText(), PlaySettings, settingsOnly: true);
            _titleOverlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.title"), Body = feedback ?? SaveText.Get("slots.welcome"),
                ConfirmLabel = SaveText.Get("slots.new"), Accept = () => ShowSlotSelection(true),
                BrowseLabel = SaveText.Get("slots.load"), Browse = () => ShowSlotSelection(false),
                HideCancel = true, CenterBody = true
            });
        }

        private void ShowSlotSelection(bool newGame, int index = 0, string feedback = null)
        {
            var states = Enumerable.Range(0, SaveSlotCount).Select(ReadSlotState).ToArray();
            bool Matches(SlotState state) => newGame ? state == SlotState.Empty : state == SlotState.Occupied;
            var candidates = Enumerable.Range(0, SaveSlotCount).Where(slot => Matches(states[slot])).ToArray();
            if (candidates.Length == 0)
            {
                ShowSaveMenu(SaveText.Get(states.Contains(SlotState.Unavailable) ? "slots.readFailed" :
                    newGame ? "slots.full" : "slots.empty"));
                return;
            }
            index %= candidates.Length;
            int slot = candidates[index];
            _titleOverlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get(newGame ? "slots.new" : "slots.load"),
                Body = feedback ?? SaveText.Get(newGame ? "slots.emptySlot" : "slots.occupiedSlot", slot + 1),
                ConfirmLabel = SaveText.Get("slots.select"), Accept = () => SelectSlot(slot, newGame),
                BrowseLabel = SaveText.Get("slots.next"),
                Browse = candidates.Length > 1 ? (Action)(() => ShowSlotSelection(newGame, index + 1)) : null,
                CancelLabel = SaveText.Get("slots.back"), Cancel = () => ShowSaveMenu(), CenterBody = true
            });
        }

        private SlotState ReadSlotState(int slot)
        {
            try
            {
                var storage = _saveSlotStorage(slot);
                return storage.Read(0) == null && storage.Read(1) == null ? SlotState.Empty : SlotState.Occupied;
            }
            catch (Exception) { return SlotState.Unavailable; }
        }

        private void SelectSlot(int slot, bool newGame)
        {
            CloseSaveMenu();
            _activeSlot = slot;
            if (!StartGame(_saveSlotStorage(slot), newGame))
                ShowSaveMenu(SaveText.Get(newGame ? "slots.createFailed" : "save.loadFailed"));
        }

        private void CloseSaveMenu()
        {
            _titleOverlays?.Dispose();
            _titleOverlays = null;
        }

        private void ShowStationSaveMenu()
        {
            if (_manualSave != null) return;
            var overlays = Presenter.StationSaveOverlays;
            overlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.current", _activeSlot + 1), Body = SaveText.Get("slots.actions"),
                ConfirmLabel = SaveText.Get("slots.save"),
                Accept = () => _manualSave = StartCoroutine(SaveManually(overlays)),
                BrowseLabel = SaveText.Get("slots.settings"), Browse = () =>
                {
                    overlays.DismissStationDialog();
                    overlays.OpenStandaloneSettings();
                },
                CancelLabel = SaveText.Get("slots.back")
            });
        }

        private IEnumerator SaveManually(LevelOverlaySession overlays)
        {
            overlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.save"), Body = SaveText.Get("slots.saving", "…"),
                HideCancel = true, CenterBody = true
            });
            // Yield a frame so synchronous platform writes never precede the first visual feedback.
            yield return null;
            float step = SavingIndicatorSeconds / SavingIndicatorSteps;
            for (int frame = 1; frame <= SavingIndicatorSteps; frame++)
            {
                overlays.UpdateStationDialogBody(SaveText.Get("slots.saving", new string('.', frame)));
                yield return new WaitForSecondsRealtime(step);
            }
            bool saved = Presenter.TryManualSave();
            _manualSave = null;
            overlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.save"),
                Body = SaveText.Get(saved ? "slots.saved" : "save.writeFailed"), CenterBody = true,
                CancelLabel = SaveText.Get("slots.back"), Cancel = saved ? (Action)null : ReturnToSaveMenu,
                ConfirmLabel = SaveText.Get("slots.title"), Accept = saved ? (Action)ReturnToSaveMenu : null
            }, replaceWhileInactive: true);
        }

        private void ReturnToSaveMenu()
        {
            _bootstrap.BindSession(null);
            Presenter.Dispose();
            Presenter = null;
            ShowSaveMenu();
        }
    }
}
