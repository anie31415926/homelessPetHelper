using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Progress;
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
        private bool _disposed;
        public MainProgressSession Progress { get; }
        public GameLoopSession Session => Progress.Session;

        public MainLoopPresenter(StationPanelView station, CityMapView map, SiegeTrialView siege,
            ApproachPanelView approach, GameContent content,
            AudioService audio = null, ISaveStorage progressStorage = null, Func<int> outingSeedSource = null)
        {
            _station = station;
            _map = map;
            _siege = siege;
            _approach = approach ?? throw new ArgumentNullException(nameof(approach));
            _content = content;
            _audio = audio;
            _outingSeedSource = outingSeedSource;
            content.Validate();
            _config = content.CreateMapConfig();
            _text = new OptionPreviewText(content.StationText);
            _mapText = new OptionPreviewText(content.MapText);
            Progress = new MainProgressSession(_config, content.Artwork, progressStorage);
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
                _station.SetStatus(string.Empty, string.Empty, _text.Get("save.loadFailed"));
                return;
            }
            _stationPresenter = new StationPreviewSession(station, content.StationText,
                content.Artwork, content.Font, Session, OpenMap, Progress.Care, Checkpoint);
            _station.gameObject.SetActive(true);
            Session.SetSuspended(false);
            if (Progress.Recovered) _stationPresenter.ReportPersistenceRecovery();
        }

        private bool Checkpoint()
        {
            bool saved = Progress.TryCheckpoint();
            if (!saved) _stationPresenter.ReportPersistenceFailure();
            return saved;
        }

        private void OpenMap()
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
            Guid outingId = Guid.NewGuid();
            int seed = _outingSeedSource != null ? _outingSeedSource() :
                _config.Seed ^ (BitConverter.ToInt32(outingId.ToByteArray(), 0) & int.MaxValue);
            if (!Session.TryOpenMap(outingId, seed, out var failure))
            {
                _stationPresenter.ReportOutingFailure(failure);
                return;
            }
            _station.gameObject.SetActive(false);
            _map.gameObject.SetActive(true);
            Session.SetSuspended(false);
            _mapPresenter = new CityMapPresenter(_map, _content.MapText, _config, Session,
                EnterApproach, () => ShowStation(false), GetDepartureRestriction, hasOpeningRoute: true);
            _mapPresenter.ShowFeedback(_text.Get("save.outingBoundary"));
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

        private void EnterApproach()
        {
            var outing = Session.Outing.Session;
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest ||
                !Session.Requests.TryGet(outing.AcceptedRequestId, out var request)) return;
            var encounter = _config.GetRescue(request.TemplateId);
            if (encounter == null) throw new InvalidOperationException("Missing authored rescue encounter.");
            var opening = _content.CreateApproachCatalog().Create(outing.SessionId, outing.Seed);
            if (!Session.TryStartOpeningRoute(opening, out var failure))
            {
                opening.Dispose();
                _mapPresenter.ShowFeedback(_text.Get("loop.failure." + failure));
                return;
            }
            _mapPresenter.Dispose();
            _mapPresenter = null;
            _map.gameObject.SetActive(false);
            _approach.Bind(opening, _content.ApproachText, encounter.DogArtwork, EnterSiege,
                ReturnFromApproach, Session.SetSuspended);
            _approach.gameObject.SetActive(true);
        }

        private void ReturnFromApproach()
        {
            if (Session.TryReturnUnresolved(out var failure)) ShowStation(false);
            else _approach.ShowExternalFeedback(_text.Get("loop.failure." + failure));
        }

        private void EnterSiege()
        {
            if (_disposed) return;
            var outing = Session.Outing.Session;
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest ||
                !Session.Requests.TryGet(outing.AcceptedRequestId, out var request)) return;
            var encounter = _config.GetRescue(request.TemplateId);
            if (encounter == null)
                throw new InvalidOperationException("No authored siege encounter for this request.");
            var activity = new SiegeRescueActivity(outing.SessionId, outing.Seed,
                _content.CreateSiegeCatalog(), encounter);
            if (!Session.TryEnterRescueAfterOpening(activity, out var failure))
            {
                activity.Dispose();
                _approach.ShowExternalFeedback(_text.Get("loop.failure." + failure));
                return;
            }
            _activity = activity;
            _approach.gameObject.SetActive(false);
            _approach.Unbind();
            _siege.BindEncounter(activity.Battle, _content.SiegeText, ConfirmRescue, ReturnFromSiege,
                Session.SetSuspended);
            _siege.gameObject.SetActive(true);
            Session.SetSuspended(false);
        }

        private void ConfirmRescue()
        {
            _activity.SynchronizeOutcome();
            if (!Session.TryConfirmRescue(out var failure))
                throw new InvalidOperationException("Siege completion could not be confirmed: " + failure);
            _audio?.PlayEffect(GameAudioCues.RescueSuccess);
        }

        private void ReturnFromSiege()
        {
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
            _mapPresenter?.Dispose();
            _mapPresenter = null;
            _approach.gameObject.SetActive(false);
            _approach.Unbind();
            _siege.gameObject.SetActive(false);
            _siege.UnbindEncounter();
            _activity = null;
            _map.gameObject.SetActive(false);
            Session.SetSuspended(false);
            _stationPresenter.ShowSharedStation(arrival);
            _station.gameObject.SetActive(true);
            Checkpoint();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _mapPresenter?.Dispose();
            _approach.Unbind();
            _siege.UnbindEncounter();
            _stationPresenter?.Dispose();
            Progress.Dispose();
        }
    }
}
