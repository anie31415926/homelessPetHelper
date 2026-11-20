using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.CityMap;
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
        private readonly GameContent _content;
        private readonly CityMapPreviewConfig _config;
        private readonly StationPreviewSession _stationPresenter;
        private readonly OptionPreviewText _text;
        private CityMapPresenter _mapPresenter;
        private SiegeRescueActivity _activity;
        private bool _disposed;
        public GameLoopSession Session { get; }

        public MainLoopPresenter(StationPanelView station, CityMapView map, SiegeTrialView siege, GameContent content)
        {
            _station = station;
            _map = map;
            _siege = siege;
            _content = content;
            content.Validate();
            _config = content.CreateMapConfig();
            _text = new OptionPreviewText(content.StationText);
            var pool = _config.CreateRequestPool();
            Session = new GameLoopSession(_config.InitialCapacity, _config.MinutesPerDay,
                _config.CreateRules(new PointUnlockState(), pool), pool, _config.TimePolicy);
            _map.gameObject.SetActive(false);
            _siege.gameObject.SetActive(false);
            _stationPresenter = new StationPreviewSession(station, content.StationText,
                content.Artwork, content.Font, Session, OpenMap);
            _station.gameObject.SetActive(true);
            Session.SetSuspended(false);
        }

        private void OpenMap()
        {
            if (!Session.TryOpenMap(Guid.NewGuid(), _config.Seed, out var failure))
            {
                _stationPresenter.ReportOutingFailure(failure);
                return;
            }
            _station.gameObject.SetActive(false);
            _map.gameObject.SetActive(true);
            Session.SetSuspended(false);
            _mapPresenter = new CityMapPresenter(_map, _content.MapText, _config, Session,
                EnterSiege, () => ShowStation(false));
        }

        private void EnterSiege()
        {
            var outing = Session.Outing.Session;
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest ||
                !Session.Requests.TryGet(outing.AcceptedRequestId, out var request)) return;
            var template = _config.GetRequestTemplate(request.TemplateId);
            if (template == null || template.AnimalDefinitionId != "cat.shy" || outing.SelectedPointId != "parking")
                throw new InvalidOperationException("No authored siege encounter for this request.");
            var activity = new SiegeRescueActivity(outing.SessionId, outing.Seed, _content.CreateSiegeCatalog());
            if (!Session.TryEnterRouteFreeRescue(activity, out var failure))
            {
                activity.Dispose();
                _mapPresenter.ShowFeedback(_text.Get("loop.failure." + failure));
                return;
            }
            _activity = activity;
            _mapPresenter.Dispose();
            _mapPresenter = null;
            _map.gameObject.SetActive(false);
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
                if (Session.TryReturnRescued(condition, out failure)) { ShowStation(true); return; }
            }
            else if (Session.TryReturnUnresolved(out failure)) { ShowStation(false); return; }
            _siege.ShowExternalFeedback(_text.Get("loop.failure." + failure));
        }

        private void ShowStation(bool arrival)
        {
            _mapPresenter?.Dispose();
            _mapPresenter = null;
            _siege.gameObject.SetActive(false);
            _siege.UnbindEncounter();
            _activity = null;
            _map.gameObject.SetActive(false);
            Session.SetSuspended(false);
            _stationPresenter.ShowSharedStation(arrival);
            _station.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _mapPresenter?.Dispose();
            _siege.UnbindEncounter();
            _stationPresenter.Dispose();
            Session.Dispose();
        }
    }
}
