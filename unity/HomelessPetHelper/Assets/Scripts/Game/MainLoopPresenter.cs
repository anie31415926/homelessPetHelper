using HomelessPetHelper.Modules.Animals;
using System;
using HomelessPetHelper.Game.CityMap.Travel;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Game.Guidance;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.UI;

namespace HomelessPetHelper.Game
{
    public sealed class MainLoopPresenter : IDisposable
    {
        private readonly StationPanelView _station;
        private readonly CityMapView _map;
        private readonly OutingWeatherGraphic _mapWeather;
        public OutingWeather WorldWeather { get; private set; } = OutingWeather.InitialWorld;
        private OutingWeather? _outingWeather;
        private readonly SiegeTrialView _siege;
        private readonly ApproachPanelView _approach;
        private readonly GameContent _content;
        private readonly CityMapPreviewConfig _config;
        private readonly StationPreviewSession _stationPresenter;
        private readonly OptionPreviewText _text;
        private readonly OptionPreviewText _mapText;
        private readonly AudioService _audio;
        private readonly Func<int> _outingSeedSource;
        private CityMapPresenter _mapPresenter;
        private SiegeRescueActivity _activity;
        private readonly OutingFieldCatalog _fields;
        private readonly OutingFieldView _battleScene;
        private readonly SiegePetMotion _petMotion;
        private OutingFieldLayout _activeField;
        private AnimalIndividual _outingAnimal;
        public AnimalSnapshot OutingAnimal => _outingAnimal?.Current;
        private bool _disposed;
        private bool _restoringProgress;
        private bool _openingPending;
        private int _savedOpeningVersion = -1, _savedBattleRevision = -1;
        private readonly MainAudioSession _audioSession;
        private readonly Settings.PlaySettingsSession _playSettings;
        private readonly GuidanceProgressSession _guidance;
        private readonly LevelGuideCatalog _guides;
        private readonly OptionPreviewText _uiText;
        private LevelOverlaySession _overlays;
        public LevelOverlaySession Overlays => _overlays;
        public MainProgressSession Progress { get; }
        public GameLoopSession Session => Progress.Session;

        public MainLoopPresenter(StationPanelView station, CityMapView map, SiegeTrialView siege,
            ApproachPanelView approach, GameContent content,
            AudioService audio = null, ISaveStorage progressStorage = null, Func<int> outingSeedSource = null,
            MainAudioSession audioSession = null, GuidanceProgressSession guidance = null,
            Settings.PlaySettingsSession playSettings = null, CityMap.Travel.MapTravelView travel = null,
            bool showOpening = true, OutingWeatherGraphic mapWeather = null)
        {
            _travel = travel;
            _station = station;
            _map = map;
            _mapWeather = mapWeather;
            SetWorldWeather(WorldWeather);
            _siege = siege;
            _approach = approach ?? throw new ArgumentNullException(nameof(approach));
            _content = content;
            _audio = audio;
            _audioSession = audioSession;
            _playSettings = playSettings ?? new Settings.PlaySettingsSession();
            _playSettings.Changed += ApplyPlaySettings;
            _guidance = guidance;
            _guides = content.CreateGuides();
            _uiText = content.CreateLevelUiText();
            _outingSeedSource = outingSeedSource;
            content.Validate();
            _fields = content.CreateFields();
            _battleScene = siege.GetComponent<OutingFieldView>();
            if (_battleScene == null) throw new InvalidOperationException("Missing battle field scene binding.");
            _petMotion = siege.GetComponent<SiegePetMotion>() ??
                throw new InvalidOperationException("Missing animated animal binding.");
            _config = content.CreateMapConfig();
            ValidateTravel();
            _text = new OptionPreviewText(content.StationText);
            _mapText = new OptionPreviewText(content.MapText);
            Progress = new MainProgressSession(_config, content.Artwork, progressStorage,
                content.CreateFieldProgressContent(_fields));
            _openingPending = showOpening && Progress.IsNewGame;
            _map.gameObject.SetActive(false);
            _siege.gameObject.SetActive(false);
            _approach.gameObject.SetActive(false);
            if (!Progress.CanContinue)
            {
                _station.gameObject.SetActive(true);
                _station.ApplyFont(content.Font);
                _station.ShowPage(false, false, false);
                _station.SetSuspended(true);
                _station.SetSessionCommand(_text.Get("loop.outing"), false);
                _station.ShowBlockingError(_text.Get("save.errorTitle"), _text.Get("save.loadFailed"));
                return;
            }
            _stationPresenter = new StationPreviewSession(station, content.StationText,
                content.Artwork, content.Font, Session, OpenMap, Progress.Care, Checkpoint,
                Progress, OpenStationDialog);
            _station.SettingsRequested += OpenStationSettings;
            _station.SetSettingsAvailable(_audioSession != null);
            _approach.CheckpointRequested += FlushFieldProgress;
            _station.gameObject.SetActive(true);
            Session.SetSuspended(false);
            if (Progress.Recovered) _stationPresenter.ReportPersistenceRecovery();
            _restoringProgress = true;
            try
            {
                if (Progress.Fields.HasActive)
                {
                    _station.gameObject.SetActive(false);
                    Session.SetSuspended(false);
                    EnterApproach();
                    if (_activity != null) _overlays?.OpenPause();
                }
            }
            finally { _restoringProgress = false; }
        }

        private bool Checkpoint()
        {
            if (_restoringProgress) return true;
            bool saved = Progress.TryCheckpoint();
            if (!saved)
            {
                CloseOverlays();
                _map.gameObject.SetActive(false);
                _approach.gameObject.SetActive(false);
                _siege.gameObject.SetActive(false);
                _station.gameObject.SetActive(true);
                _stationPresenter.ReportPersistenceFailure();
                Session.SetSuspended(true);
            }
            return saved;
        }

        /// <summary>Coalesces continuous work; discrete actions and lifecycle boundaries call this directly.</summary>
        public void FlushFieldProgress()
        {
            if (_disposed || !Progress.CanContinue || !Progress.Fields.HasActive || _restoringProgress) return;
            var opening = Progress.Fields.Opening;
            int version = opening?.Version ?? -1, revision = _activity?.Battle.Revision ?? -1;
            if (version == _savedOpeningVersion && revision == _savedBattleRevision) return;
            if (Checkpoint()) { _savedOpeningVersion = version; _savedBattleRevision = revision; }
        }

        private void SetFieldSuspended(bool suspended)
        {
            _fieldSuspendedRequested = suspended;
            Session.SetSuspended(suspended || _returnTravelling || !Progress.CanContinue);
            if (suspended) FlushFieldProgress();
        }

        private void OpenMap() => OpenMap(null);

        public void SetWorldWeather(OutingWeather weather)
        {
            WorldWeather = weather;
            _mapWeather?.ShowOnMap(weather);
        }

        private void OpenMap(string noticePoint)
        {
            if (!Progress.CanContinue) return;
            // Validate the care gate before saving or opening an outing.
            foreach (var animal in Session.Station.Animals)
            {
                if (Session.Station.CanCompleteNecessaryCare(Session.StationSessionId, animal.AnimalId,
                    out _)) continue;
                _stationPresenter.ReportOutingFailure(OutingAssemblyFailure.BasicAssessmentRequired);
                return;
            }
            if (!Checkpoint()) return;
            _outingWeather = null;
            Guid outingId = Guid.NewGuid();
            int seed = _outingSeedSource != null ? _outingSeedSource() :
                _config.Seed ^ (BitConverter.ToInt32(outingId.ToByteArray(), 0) & int.MaxValue);
            if (!Session.TryOpenMap(outingId, seed, out var failure))
            {
                _stationPresenter.ReportOutingFailure(failure);
                return;
            }
            CloseOverlays();
            _station.gameObject.SetActive(false);
            _map.gameObject.SetActive(true);
            Session.SetSuspended(false);
            _mapPresenter = new CityMapPresenter(_map, _content.MapText, _config, Session,
                EnterApproach, () => ShowStation(false), GetDepartureRestriction,
                hasOpeningRoute: true, beginTravel: BeginMapDeparture);
            _mapPresenter.ShowFeedback(_text.Get("save.outingBoundary"));
            if (noticePoint != null) _mapPresenter.Select(noticePoint);
        }

        internal void PresentPendingRescueNotice()
        {
            if (_disposed || !Progress.CanContinue || !_station.isActiveAndEnabled ||
                !_stationPresenter.CanPresentRescueNotice || _overlays?.Top != null ||
                Session.Flow.IsSuspended || !_station.gameObject.activeInHierarchy) return;
            if (TryPresentOpening()) return;
            var request = Progress.UnshownRescueNotice;
            if (request == null || !_config.Definition.TryGetPoint(request.PointId, out var point)) return;
            Progress.MarkRescueNoticeShown();
            if (!Checkpoint()) return;

            string place = _mapText.Get(point.DisplayNameKey);
            bool available = request.State == RequestState.Available;
            bool canDepart = available && !Session.Station.IsFull;
            var body = available ? _mapText.Get("dispatch.body", place,
                _mapText.Get(_config.GetRequestTemplate(request.TemplateId).TextKey)) :
                _mapText.Get("dispatch.closed", place);
            if (available && !canDepart) body += "\n" + _mapText.Get("full");
            OpenStationDialog(new StationDialog
            {
                Title = _mapText.Get("dispatch.title"), Body = body,
                CancelLabel = _mapText.Get("dispatch.later"), ConfirmLabel = _mapText.Get("dispatch.map"),
                Accept = canDepart ? (Action)(() => OpenMap(request.PointId)) : null
            });
        }

        private bool TryPresentOpening()
        {
            if (!_openingPending) return false;
            _openingPending = false;
            string pointId = _config.InitialPointId;
            if (!Session.Map.TrySelectPoint(pointId, out var request, out _) ||
                _config.GetRescue(request.TemplateId)?.AnimalDefinitionId != "cat.shy" ||
                !_config.Definition.TryGetPoint(pointId, out var point)) return false;
            OpenStationDialog(new StationDialog
            {
                Title = _text.Get("opening.title"),
                Body = _text.Get("opening.body", _mapText.Get(point.DisplayNameKey)),
                ConfirmLabel = _text.Get("opening.go"), CancelLabel = _mapText.Get("dispatch.later"),
                Accept = () => OpenMap(pointId), Cancel = () => Checkpoint()
            });
            return true;
        }

        private void OpenStationSettings()
        {
            if (_disposed || !Progress.CanContinue || _audioSession == null || !_station.isActiveAndEnabled) return;
            GetStationOverlays().OpenStandaloneSettings();
        }

        private void OpenStationDialog(StationDialog dialog)
        {
            if (_disposed || !Progress.CanContinue || !_station.isActiveAndEnabled) return;
            GetStationOverlays().ShowStationDialog(dialog);
        }

        private LevelOverlaySession GetStationOverlays()
        {
            if (_overlays == null)
            {
                var target = _station.MenuTarget;
                _overlays = new LevelOverlaySession(_station.GetComponent<LevelOverlayAnchor>().View,
                    new LevelOverlayBindings(suspended =>
                        {
                            if (_station != null && _station.isActiveAndEnabled)
                                _station.SetOverlaySuspended(suspended);
                        },
                        () => { },
                        () => string.Empty, () => null, (overlay, guide) => _station.BindOverlay(overlay),
                        (action, targets) => false, target, target),
                    _audioSession, null, _uiText, _playSettings, settingsOnly: true);
            }
            return _overlays;
        }

        private string GetDepartureRestriction(MapDeparturePlan plan)
        {
            if (plan.EntryKind == MapEntryKind.OwnerReunion) return _mapText.Get("unavailable.reunion");
            if (plan.EntryKind == MapEntryKind.Exploration) return _mapText.Get("unavailable.exploration");
            if (!Session.Requests.TryGet(plan.RequestId, out var request) ||
                _config.GetRescue(request.TemplateId) == null)
                return _mapText.Get("request.unavailable");
            return null;
        }

        private OutingFieldLayout SelectField(string pointId, int seed)
        {
            var selected = _fields.Get(pointId, seed);
            var intro = _fields.Get(pointId, seed, introductory: true);
            if (intro == null || !intro.Introductory) return selected;
            using var trial = intro.Opening.Create(Guid.NewGuid(), seed);
            var guide = _guides.Create(trial.Template.Id);
            return guide != null && (_guidance == null || !_guidance.HasCompleted(guide.MechanicId))
                ? intro : selected;
        }

        private void EnterApproach()
        {
            var outing = Session.Outing.Session;
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest ||
                !Session.Requests.TryGet(outing.AcceptedRequestId, out var request)) return;
            var encounter = _config.GetRescue(request.TemplateId);
            if (encounter == null) throw new InvalidOperationException("Missing authored rescue encounter.");
            var opening = Progress.Fields.Opening;
            if (Session.Activity != null)
                opening = Progress.Fields.CreatePresentationOpening(outing.SessionId);
            bool restored = opening != null;
            if (!restored)
            {
                opening = Progress.Fields.Begin(Session, SelectField(outing.SelectedPointId, outing.Seed));
                if ((!opening.IsComplete || Progress.Fields.Layout?.AllowsOptionalCleanup == true) &&
                    !Session.TryStartOpeningRoute(opening, out var failure))
                {
                    opening.Dispose();
                    _mapPresenter.ShowFeedback(_text.Get("loop.failure." + failure));
                    return;
                }
            }
            _activeField = Progress.Fields.Layout;
            _outingAnimal = Progress.Fields.Animal;
            if (!Checkpoint()) return;
            _outingWeather ??= WorldWeather;
            _mapPresenter?.Dispose();
            _mapPresenter = null;
            _map.gameObject.SetActive(false);
            var artwork = _content.GetOutingArtwork(outing.SelectedPointId);
            _approach.SetLocationBackground(artwork.Background);
            _approach.ConfigureField(_activeField, artwork.Background, encounter.DogArtwork, opening,
                artwork.Props, artwork);
            _approach.FieldScene.SetWeather(_outingWeather.Value);
            _approach.Bind(opening, _content.ApproachText, encounter.DogArtwork, EnterSiege,
                ReturnFromApproach, SetFieldSuspended);
            _approach.FieldScene.ShowAnimal(_outingAnimal.Current);
            if (opening.IsComplete && (Session.Activity != null || _activeField?.AllowsOptionalCleanup != true))
            {
                EnterSiege();
                opening.Dispose();
                return;
            }
            _approach.gameObject.SetActive(true);
            if (_activeField != null)
            {
                _overlays = new LevelOverlaySession(_approach.Overlays,
                    new LevelOverlayBindings(_approach.SetOverlaySuspended, ReturnFromApproach,
                        DescribeOpeningObjectives, () => opening.IsComplete ? null :
                            _guides.Create(opening.Template.Id),
                        _approach.BindOverlay, _approach.ResolveGuideTarget,
                        _approach.PauseTarget, _approach.ObjectivesTarget),
                    _audioSession, _guidance, _uiText, _playSettings);
            }
        }

        private string DescribeOpeningObjectives()
        {
            var opening = _approach.Session;
            return _uiText.Get("objectives.opening", _uiText.Get(opening.IsComplete ? "objective.done" :
                "objective.pending"), _approach.FieldInstruction);
        }

        private string DescribeRescueObjectives()
        {
            var battle = _activity.Battle.Snapshot;
            return _uiText.Get("objectives.rescue", battle.Progress, _activity.Battle.Catalog.Rules.Target,
                _uiText.Get(_activity.IsRescueComplete ? "objective.done" : "objective.pending"),
                new Modules.Siege.UI.SiegeText(_content.SiegeText).Get("field.pauseInfo"));
        }

        private Framework.UI.GuideSession CreateRescueGuide()
        {
            var battle = _activity.Battle;
            if (battle.Phase != Modules.Siege.SiegeState.BattlePhase.Decision && !battle.Realtime) return null;
            var state = battle.Snapshot;
            if (!state.Hand.Any(card => battle.Catalog.Unit(card.DefinitionId).Cost <= state.Energy)) return null;
            return _guides.Create("siege.field");
        }

        private void ApplyPlaySettings(bool pause)
        {
            _activity?.Battle.SetPauseAtDecision(pause);
        }

        private void CloseOverlays()
        {
            _overlays?.Dispose();
            _overlays = null;
        }

        private void ReturnFromApproach()
        {
            BeginFieldReturn(ReturnFromApproachImmediately);
        }

        private void ReturnFromApproachImmediately()
        {
            Progress.Fields.Capture(Session);
            if (Session.TryReturnUnresolved(out var failure)) ShowStation(false);
            else _approach.ShowExternalFeedback(_text.Get("loop.failure." + failure));
        }

        private void EnterSiege()
        {
            if (_disposed) return;
            CloseOverlays();
            if (!Checkpoint()) return;
            var outing = Session.Outing.Session;
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest ||
                !Session.Requests.TryGet(outing.AcceptedRequestId, out var request)) return;
            var encounter = _config.GetRescue(request.TemplateId);
            if (encounter == null)
                throw new InvalidOperationException("No authored siege encounter for this request.");
            FieldSceneSnapshot fieldSnapshot = null;
            if (_activeField != null)
            {
                _activeField.ValidateBattleClear(_approach.Session);
                fieldSnapshot = new FieldSceneSnapshot(_approach.Session);
            }
            var activity = Session.Activity as SiegeRescueActivity;
            if (activity == null)
            {
                activity = new SiegeRescueActivity(outing.SessionId, Progress.Fields.Seed,
                    _activeField == null ? _content.CreateSiegeCatalog() :
                        _content.CreateFieldSiegeCatalog(_activeField, _approach.Session), encounter, _outingAnimal);
                bool entered = Session.OpeningRoute == null
                    ? Session.TryEnterRouteFreeRescue(activity, out var failure)
                    : Session.TryEnterRescueAfterOpening(activity, out failure);
                if (!entered)
                {
                    activity.Dispose();
                    _approach.ShowExternalFeedback(_text.Get("loop.failure." + failure));
                    return;
                }
                Progress.Fields.AttachBattle(activity, Session);
            }
            _activity = activity;
            activity.Battle.EnableRealtime();
            activity.Battle.PhaseChanged += FlushFieldProgress;
            ApplyPlaySettings(_playSettings.PauseAtDecision);
            _approach.gameObject.SetActive(false);
            _approach.Unbind();
            var artwork = _content.GetOutingArtwork(outing.SelectedPointId);
            _siege.SetLocationBackground(artwork.Background);
            _battleScene.Configure(_activeField, artwork.Background, encounter.DogArtwork, null, fieldSnapshot,
                artwork.Props, artwork);
            _battleScene.SetWeather(_outingWeather ?? WorldWeather);
            _battleScene.ShowAnimal(_outingAnimal.Current);
            _siege.Battlefield.ShowAnimal(_outingAnimal.Current);
            _petMotion.ShowAnimal(_outingAnimal.Current);
            _siege.SetFieldMode(_activeField != null);
            if (_activeField != null)
            {
                _siege.Scene.Configure(_battleScene.Animal.sprite, _activeField.AnimalWidthFor(encounter.DogArtwork),
                    Enumerable.Range(0, _activeField.LaneCount).Select(_activeField.Portal).ToArray(),
                    _activeField.IllustratedInterior, artwork.Props);
                _siege.Scene.SetPresentationAudio(PlayBattleEffect);
            }
            _siege.BindEncounter(activity.Battle, _content.SiegeText, ConfirmRescue, ReturnFromSiege,
                SetFieldSuspended);
            _siege.gameObject.SetActive(true);
            Session.SetSuspended(false);
            if (_activeField != null)
            {
                var scene = _siege.Scene;
                _overlays = new LevelOverlaySession(_siege.GetComponent<LevelOverlayAnchor>().View,
                    new LevelOverlayBindings(scene.SetOverlaySuspended, ReturnFromSiege,
                        DescribeRescueObjectives, CreateRescueGuide, scene.BindOverlay,
                        scene.ResolveGuideTarget, scene.PauseTarget, scene.ObjectivesTarget),
                    _audioSession, _guidance, _uiText, _playSettings);
            }
            if (!Checkpoint()) return;
        }

        private void PlayBattleEffect(string cue) => _audio?.PlayEffect(cue);

        private void ConfirmRescue()
        {
            if (Session.Flow.Stage == Modules.Rescue.RescueStage.Rescued) return;
            _activity.SynchronizeOutcome();
            _siege.Battlefield.ShowAnimal(_activity.Individual.Current);
            _petMotion.ShowAnimal(_activity.Individual.Current);
            _battleScene.ShowAnimal(_activity.Individual.Current);
            if (!Session.TryConfirmRescue(out var failure))
                throw new InvalidOperationException("Siege completion could not be confirmed: " + failure);
            _audio?.PlayEffect(GameAudioCues.RescueSuccess);
            Checkpoint();
        }

        private void ReturnFromSiege()
        {
            BeginFieldReturn(ReturnFromSiegeImmediately);
        }

        private void ReturnFromSiegeImmediately()
        {
            if (!Progress.CanContinue) return;
            Progress.Fields.Capture(Session);
            bool rescued = _activity.IsRescueComplete;
            OutingAssemblyFailure failure;
            if (rescued)
            {
                var animal = _activity.Individual.Current;
                var condition = new AnimalIntakeCondition(StationHealthAssessment.Unchecked, animal.Coat.Dirtiness,
                    animal.Health.Problems.Select(problem => problem.InstanceId).ToArray());
                if (Session.TryReturnRescued(condition, out failure))
                {
                    Progress.RecordRescuedReturn(Session.Outing.Session.SessionId,
                        Session.Outing.Session.AcceptedRequestId);
                    ShowStation(true);
                    return;
                }
            }
            else if (Session.TryReturnUnresolved(out failure)) { ShowStation(false); return; }
            _siege.ShowExternalFeedback(_text.Get("loop.failure." + failure));
        }

        private void ShowStation(bool arrival)
        {
            Progress.Fields.EndOuting();
            _outingWeather = null;
            CloseOverlays();
            _mapPresenter?.Dispose();
            _mapPresenter = null;
            _approach.gameObject.SetActive(false);
            _approach.Unbind();
            _siege.gameObject.SetActive(false);
            _siege.UnbindEncounter();
            UnsubscribeBattle();
            _activity = null;
            _outingAnimal = null;
            _savedOpeningVersion = _savedBattleRevision = -1;
            _map.gameObject.SetActive(false);
            Session.SetSuspended(false);
            bool firstRescue = arrival && Session.Requests.Requests.Count(request => request.State == RequestState.Resolved) == 1;
            _stationPresenter.ShowSharedStation(arrival, firstRescue);
            _station.gameObject.SetActive(true);
            Checkpoint();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _travel?.Cancel();
            _station.SettingsRequested -= OpenStationSettings;
            if (_station != null) _station.SetSettingsAvailable(false);
            _approach.CheckpointRequested -= FlushFieldProgress;
            UnsubscribeBattle();
            _playSettings.Changed -= ApplyPlaySettings;
            CloseOverlays();
            _mapPresenter?.Dispose();
            _approach.Unbind();
            _siege.UnbindEncounter();
            _stationPresenter?.Dispose();
            Progress.Dispose();
        }

        private readonly MapTravelView _travel;
        private bool _returnTravelling, _fieldSuspendedRequested;

        private bool BeginMapDeparture(string pointId, Func<bool> commit, Action<bool> finished)
        {
            if (_travel == null) { bool success = commit(); finished(success); return true; }
            return _travel.TryPlay(pointId, MapTravelDirection.Enter,
                () => !_disposed && Progress.CanContinue && _map.isActiveAndEnabled && !_map.IsSuspended,
                commit, finished);
        }

        private void BeginFieldReturn(Action commit)
        {
            if (_disposed || _returnTravelling || !Progress.CanContinue) return;
            if (_travel == null) { commit(); return; }
            string pointId = Session.Outing.Session.SelectedPointId;
            Guid outingId = Session.Outing.Session.SessionId;
            _returnTravelling = true;
            Session.SetSuspended(true);
            bool accepted = _travel.TryPlay(pointId, MapTravelDirection.Exit,
                () => !_disposed && Progress.CanContinue &&
                    Session.Outing.Session.SessionId == outingId &&
                    (_approach.isActiveAndEnabled || _siege.isActiveAndEnabled),
                () =>
                {
                    _returnTravelling = false;
                    Session.SetSuspended(_fieldSuspendedRequested);
                    commit();
                    return Session.Outing.Session.IsSettled;
                }, _ => FinishFieldReturn());
            if (!accepted) FinishFieldReturn();
        }

        private void FinishFieldReturn()
        {
            _returnTravelling = false;
            if (!_disposed && Progress.CanContinue)
            {
                if (Session.Outing.Session.IsSettled) _fieldSuspendedRequested = false;
                Session.SetSuspended(_fieldSuspendedRequested);
            }
        }

        private void ValidateTravel()
        {
            _travel?.Validate(_config.Definition.Points.Select(point => point.PointId));
        }

        private void UnsubscribeBattle()
        {
            if (_activity == null) return;
            // Real-time orders are coalesced by the existing periodic checkpoint; lifecycle flush remains immediate.
            _activity.Battle.PhaseChanged -= FlushFieldProgress;
        }
    }
}
