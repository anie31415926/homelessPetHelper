using System;
using System.Collections;
using System.Globalization;
using System.IO;
using HomelessPetHelper.Game.Loading;
using UnityEngine.SceneManagement;
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
        private const int SaveSlotCount = StartupSaveSlots.Count;
        private const float SavingIndicatorSeconds = 0.45f;
        private const int SavingIndicatorSteps = 3;
        private enum SlotState { Empty, Occupied, Damaged, Unavailable }

        private sealed class SlotInfo
        {
            public SlotState State;
            public int Rescued;
            public string TimeText;
            public DateTimeOffset? SavedAt;
        }

        private Func<int, ISaveStorage> _saveSlotStorage = StartupSaveSlots.OpenStorage;
        private Func<ISaveStorage> _historyStorageFactory = StartupSaveSlots.OpenHistoryStorage;
        private ISaveStorage _historyStorage;
        private bool _trackLastSlot = true;
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
            var pending = StartupSaveSlots.Pending;
            StartupSaveSlots.Pending = null;
            if (_progressStorage != null) StartGame(_progressStorage);
            else if (pending is { } launch)
            {
                _activeSlot = launch.slot;
                if (!StartGame(launch.storage, launch.create))
                {
                    StartupSaveSlots.FailedEntrySlot = launch.slot;
                    SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
                    return;
                }
                StartupSaveSlots.RecordLastPlayed(launch.history, launch.slot);
            }
            else ShowSaveMenu(); // Direct Main entry remains available for Editor previews.
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

        /// <summary>
        /// Explicit composition hook for tests and alternate launch flows. Passing no history source
        /// disables the last-slot pointer so an isolated test cannot read the developer's real history.
        /// </summary>
        public void ConfigureSaveSlots(Func<int, ISaveStorage> storage, Func<ISaveStorage> history = null)
        {
            if (_started) throw new InvalidOperationException("Configure save slots before Start.");
            _saveSlotStorage = storage ?? throw new ArgumentNullException(nameof(storage));
            _trackLastSlot = history != null;
            if (history != null) _historyStorageFactory = history;
        }

        private OptionPreviewText SaveText => _saveText ??= new OptionPreviewText(_content.StationText);

        private bool StartGame(ISaveStorage storage, bool? expectedNewGame = null)
        {
            Presenter = new MainLoopPresenter(_station, _map, _siege, _approach, _content, Audio.Playback,
                storage, _outingSeedSource, Audio, Guidance, PlaySettings, _travel, _showOpening, _mapWeather,
                expectedNewGame.HasValue ? (Action)ShowStationSaveMenu : null, expectedNewGame);
            if (expectedNewGame.HasValue && !Presenter.AcceptedSaveSelection)
            {
                Debug.LogWarning("Save selection failed: " + Presenter.Progress.PersistenceError, this);
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
            int last = ReadLastSlot();
            bool resumable = last >= 0 && InspectSlot(last).State == SlotState.Occupied;
            var dialog = new StationDialog
            {
                Title = SaveText.Get("slots.title"), Body = feedback ?? SaveText.Get("slots.welcome"),
                CenterBody = true, HideCancel = !resumable
            };
            if (resumable)
            {
                dialog.ConfirmLabel = SaveText.Get("slots.continueGame");
                dialog.Accept = () => SelectSlot(last, false);
                dialog.BrowseLabel = SaveText.Get("slots.new");
                dialog.Browse = () => ShowSlotSelection(true);
                dialog.CancelLabel = SaveText.Get("slots.load");
                dialog.Cancel = () => ShowSlotSelection(false);
            }
            else
            {
                dialog.ConfirmLabel = SaveText.Get("slots.new");
                dialog.Accept = () => ShowSlotSelection(true);
                dialog.BrowseLabel = SaveText.Get("slots.load");
                dialog.Browse = () => ShowSlotSelection(false);
            }
            _titleOverlays.ShowStationDialog(dialog);
        }

        private void ShowSlotSelection(bool newGame, int index = 0, string feedback = null)
        {
            var infos = Enumerable.Range(0, SaveSlotCount).Select(InspectSlot).ToArray();
            var candidates = Enumerable.Range(0, SaveSlotCount)
                .Where(slot => newGame ? infos[slot].State == SlotState.Empty : infos[slot].State != SlotState.Empty)
                .ToArray();
            if (candidates.Length == 0)
            {
                if (newGame) ShowReplaceOldest(infos);
                else ShowSaveMenu(SaveText.Get("slots.empty"));
                return;
            }
            index %= candidates.Length;
            int slot = candidates[index];
            bool readable = newGame || infos[slot].State == SlotState.Occupied;
            _titleOverlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get(newGame ? "slots.new" : "slots.load"),
                Body = feedback ?? DescribeSlot(slot, infos[slot]),
                ConfirmLabel = SaveText.Get(readable ? "slots.select" : "slots.reset"),
                Accept = readable ? (Action)(() => SelectSlot(slot, newGame)) : () => ConfirmReset(slot),
                BrowseLabel = candidates.Length > 1 ? SaveText.Get("slots.next") : null,
                Browse = candidates.Length > 1 ? (Action)(() => ShowSlotSelection(newGame, index + 1)) : null,
                CancelLabel = SaveText.Get("slots.back"), Cancel = () => ShowSaveMenu(), CenterBody = true
            });
        }

        private SlotInfo InspectSlot(int slot)
        {
            var info = new SlotInfo { State = SlotState.Empty };
            try
            {
                var inspected = StartupSaveSlots.Inspect(_saveSlotStorage(slot));
                if (inspected.Status == StartupSaveSlots.State.Empty) return info;
                info.State = inspected.Status == StartupSaveSlots.State.Saved
                    ? SlotState.Occupied : SlotState.Damaged;
                info.Rescued = inspected.Rescued ?? 0;
                info.SavedAt = inspected.SavedAt;
                info.TimeText = inspected.SavedAt?.ToLocalTime()
                    .ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
                    ?? SaveText.Get("slots.timeUnknown");
            }
            catch (Exception) { info.State = SlotState.Unavailable; }
            return info;
        }

        private string DescribeSlot(int slot, SlotInfo info)
        {
            switch (info.State)
            {
                case SlotState.Occupied:
                    return SaveText.Get("slots.slotDetail", slot + 1, info.Rescued, info.TimeText);
                case SlotState.Damaged: return SaveText.Get("slots.damagedSlot", slot + 1);
                case SlotState.Unavailable: return SaveText.Get("slots.readFailed");
                default: return SaveText.Get("slots.emptySlot", slot + 1);
            }
        }

        private void ShowReplaceOldest(SlotInfo[] infos)
        {
            int oldest = -1;
            foreach (int slot in Enumerable.Range(0, SaveSlotCount).Where(s => infos[s].State != SlotState.Empty))
            {
                if (oldest < 0) { oldest = slot; continue; }
                var candidate = infos[slot].SavedAt;
                var current = infos[oldest].SavedAt;
                if (candidate.HasValue && (!current.HasValue || candidate.Value < current.Value)) oldest = slot;
            }
            if (oldest < 0) { ShowSaveMenu(SaveText.Get("slots.full")); return; }
            _titleOverlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.fullTitle"),
                Body = SaveText.Get("slots.fullBody", oldest + 1),
                ConfirmLabel = SaveText.Get("slots.reset"), Accept = () => ConfirmReset(oldest),
                CancelLabel = SaveText.Get("slots.back"), Cancel = () => ShowSaveMenu(), CenterBody = true
            });
        }

        private void ConfirmReset(int slot)
        {
            _titleOverlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.current", slot + 1),
                Body = SaveText.Get("slots.resetConfirm", slot + 1),
                ConfirmLabel = SaveText.Get("slots.reset"), Accept = () => ResetSlot(slot),
                CancelLabel = SaveText.Get("slots.back"), Cancel = () => ShowSaveMenu(), CenterBody = true
            });
        }

        private void ResetSlot(int slot)
        {
            // Archiving happens inside the reset; a failed archive must leave the original untouched.
            if (!SaveService.TryReset(_saveSlotStorage(slot), out var error))
            {
                Debug.LogWarning("Save reset failed: " + error, this);
                ShowSaveMenu(SaveText.Get("slots.resetFailed"));
                return;
            }
            CloseSaveMenu();
            _activeSlot = slot;
            if (!StartGame(_saveSlotStorage(slot), true))
            {
                ShowSaveMenu(SaveText.Get("slots.createFailed"));
                return;
            }
            RememberLastSlot(slot);
        }

        private void SelectSlot(int slot, bool newGame)
        {
            CloseSaveMenu();
            _activeSlot = slot;
            if (!StartGame(_saveSlotStorage(slot), newGame))
            {
                ShowSaveMenu(SaveText.Get(newGame ? "slots.createFailed" : "save.loadFailed"));
                return;
            }
            RememberLastSlot(slot);
        }

        private int ReadLastSlot()
        {
            if (!_trackLastSlot) return -1;
            try
            {
                _historyStorage ??= _historyStorageFactory();
                int? slot = StartupSaveSlots.ReadLastPlayed(_historyStorage);
                return slot.HasValue && slot.Value >= 0 && slot.Value < SaveSlotCount ? slot.Value : -1;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Last played slot could not be read: " + error, this);
                return -1;
            }
        }

        private void RememberLastSlot(int slot)
        {
            if (!_trackLastSlot || slot < 0) return;
            try
            {
                _historyStorage ??= _historyStorageFactory();
                StartupSaveSlots.RecordLastPlayed(_historyStorage, slot);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Last played slot could not be recorded: " + error, this);
            }
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
                CancelLabel = SaveText.Get("slots.home"), Cancel = () => ConfirmReturnHome(overlays)
            });
        }

        private void ConfirmReturnHome(LevelOverlaySession overlays)
        {
            overlays.ShowStationDialog(new StationDialog
            {
                Title = SaveText.Get("slots.home"), Body = SaveText.Get("slots.homeBody"),
                ConfirmLabel = SaveText.Get("slots.home"),
                Accept = () =>
                {
                    if (_manualSave == null) _manualSave = StartCoroutine(ReturnHome(overlays));
                },
                CancelLabel = SaveText.Get("slots.continueGame"), Cancel = () => CloseStationDialogs(overlays),
                CenterBody = true
            });
        }

        private static void CloseStationDialogs(LevelOverlaySession overlays)
        {
            // One station dialog is replaced, not stacked: dismissing the confirmation returns to play.
            overlays.DismissStationDialog();
        }

        private IEnumerator ReturnHome(LevelOverlaySession overlays)
        {
            bool saved = Presenter != null && Presenter.TryManualSave();
            yield return null;
            _manualSave = null;
            if (!saved)
            {
                overlays.ShowStationDialog(new StationDialog
                {
                    Title = SaveText.Get("save.errorTitle"), Body = SaveText.Get("slots.homeFailed"),
                    CancelLabel = SaveText.Get("slots.continueGame"),
                    Cancel = () => CloseStationDialogs(overlays), CenterBody = true
                }, replaceWhileInactive: true);
                yield break;
            }
            ReturnToSaveMenu();
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
                ConfirmLabel = saved ? SaveText.Get("slots.home") : null,
                Accept = saved ? (Action)(() => _manualSave = StartCoroutine(ReturnToTitle())) : null,
                CancelLabel = SaveText.Get(saved ? "slots.continueGame" : "slots.home"),
                Cancel = saved ? (Action)null : () => _manualSave = StartCoroutine(ReturnToTitle())
            }, replaceWhileInactive: true);
        }

        private IEnumerator ReturnToTitle()
        {
            yield return null;
            _manualSave = null;
            ReturnToSaveMenu();
        }

        private void ReturnToSaveMenu()
        {
            _bootstrap.BindSession(null);
            Presenter.Dispose();
            Presenter = null;
            // The loading scene owns the slot list, continue entry and deletion; Main keeps only the
            // in-run menu so both entry points cannot drift apart.
            SceneManager.LoadSceneAsync(LoadingSceneRoot.ScenePath);
        }
    }
}
