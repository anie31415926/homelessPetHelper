using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.WorldTime;
using UnityEngine;

namespace HomelessPetHelper.Game.CityMap
{
    /// <summary>Map navigation with real departure and unresolved return, never a fake field success.</summary>
    public sealed class CityMapPresenter : IDisposable
    {
        private readonly CityMapView _view;
        private readonly OptionPreviewText _text;
        private readonly CityMapPreviewConfig _config;
        private readonly CityMapRules _rules;
        private readonly RescueFlow _flow = new RescueFlow();
        private readonly RescueStation _station;
        private readonly ActionWorldClock _clock;
        private readonly OutingCoordinator _outing;
        private readonly GameLoopSession _loop;
        private readonly Action _departed;
        private readonly Action _returned;
        private readonly Func<MapDeparturePlan, string> _departureRestriction;
        private readonly bool _hasOpeningRoute;
        private string _selection;
        private bool _paused;
        private string _feedbackKey = "ready";
        private string _externalFeedback;
        private bool _disposed;
        private bool _travelling;
        private readonly Func<string, Func<bool>, Action<bool>, bool> _beginTravel;

        public CityMapPresenter(CityMapView view, TextAsset text, CityMapPreviewConfig config,
            GameLoopSession loop = null, Action departed = null, Action returned = null,
            Func<MapDeparturePlan, string> departureRestriction = null, bool hasOpeningRoute = false,
            Func<string, Func<bool>, Action<bool>, bool> beginTravel = null)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _view = view;
            _loop = loop;
            _departed = departed;
            _returned = returned;
            _departureRestriction = departureRestriction;
            _hasOpeningRoute = hasOpeningRoute;
            _beginTravel = beginTravel;
            _text = new OptionPreviewText(text);
            _config.ValidateText(_text);
            _view.ValidateDefinition(config.Definition.BackgroundId,
                config.Definition.Points.Select(point => point.PointId));
            _view.CloseDetails();
            if (loop != null)
            {
                _station = loop.Station;
                _clock = loop.Clock;
                _rules = loop.Map;
                _flow = loop.Flow;
                _outing = loop.Outing;
                _selection = GetInitialSelection();
                Refresh();
            }
            else
            {
                Guid stationId = Guid.NewGuid();
                Guid worldId = Guid.NewGuid();
                _station = new RescueStation(stationId, config.InitialCapacity);
                _clock = new ActionWorldClock(worldId, config.MinutesPerDay);
                RequestPoolState pool = config.CreateRequestPool();
                _rules = config.CreateRules(new PointUnlockState(), pool);
                _outing = new OutingCoordinator(_flow, _station, stationId, _clock, worldId,
                    _rules, pool, config.TimePolicy);
                Begin();
            }
            _view.PlaceSelected += Select;
            _view.PrimaryClicked += Act;
            _view.CancelClicked += Cancel;
            _view.PauseChanged += Pause;
            Pause(_view.IsSuspended);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.PlaceSelected -= Select;
            _view.PrimaryClicked -= Act;
            _view.CancelClicked -= Cancel;
            _view.PauseChanged -= Pause;
        }

        public void ShowFeedback(string feedback)
        {
            if (_disposed) return;
            _externalFeedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
            Refresh();
            _view.ShowNotice(feedback);
        }

        private void Begin()
        {
            if (!_outing.TryBeginOuting(Guid.NewGuid(), _config.Seed, out var failure))
                throw new InvalidOperationException("Cannot open map trial: " + failure);
            _selection = GetInitialSelection();
            _feedbackKey = "ready";
            _externalFeedback = null;
            Refresh();
        }

        private string GetInitialSelection()
        {
            // Recommend only on opening. Later refreshes keep the player's explicit selection.
            MapPointView[] points = _rules.BuildSnapshot().Points
                .OrderByDescending(point => point.PointId == _config.InitialPointId).ToArray();
            foreach (MapPointView point in points)
                if (point.DisplayState == PointDisplayState.HasHelpRequest &&
                    CanDepart(point.PointId, out _, out _)) return point.PointId;
            return points.FirstOrDefault(point => point.DisplayState != PointDisplayState.Locked)?.PointId
                ?? _config.InitialPointId;
        }

        internal void Select(string pointId)
        {
            if (_disposed || _paused || _travelling || _outing.Session.Stage != OutingStage.MapSelecting) return;
            MapPointView point = _rules.BuildSnapshot().Points.SingleOrDefault(item => item.PointId == pointId);
            if (point == null || point.DisplayState == PointDisplayState.Locked) return;
            _selection = pointId;
            _feedbackKey = "ready";
            _externalFeedback = null;
            Refresh();
            _view.OpenDetails();
        }

        private void Act()
        {
            if (_disposed || _paused || _travelling) return;
            _externalFeedback = null;
            var session = _outing.Session;
            if (session.IsSettled)
            {
                Begin();
                return;
            }

            // Recheck at command time: the rendered button is not the authority for capacity or requests.
            if (session.Stage == OutingStage.MapSelecting && !CanDepart(_selection, out _, out _))
            {
                Refresh();
                return;
            }

            if (session.Stage == OutingStage.MapSelecting && _beginTravel != null)
            {
                string pointId = _selection;
                CanDepart(pointId, out var plan, out _);
                Guid sessionId = session.SessionId, requestId = plan.RequestId;
                _travelling = true;
                Refresh();
                bool accepted = _beginTravel(pointId,
                    () => CommitDeparture(sessionId, pointId, requestId),
                    _ => { if (!_disposed) { _travelling = false; Refresh(); } });
                if (!accepted) { _travelling = false; Refresh(); }
                return;
            }

            bool success = session.Stage == OutingStage.MapSelecting ?
                _outing.TrySelectAndDepart(session.SessionId, _selection, out var failure) :
                _outing.TryEndUnresolved(session.SessionId, out _, out failure);
            _feedbackKey = success ? "ready" :
                failure == OutingAssemblyFailure.StationFull ? "full" : "failure";
            Refresh();
            if (success && _loop != null)
            {
                if (session.IsSettled) _returned?.Invoke();
                else _departed?.Invoke();
            }
        }

        private bool CommitDeparture(Guid sessionId, string pointId, Guid requestId)
        {
            if (_disposed || _paused || !_view.isActiveAndEnabled ||
                _outing.Session.SessionId != sessionId ||
                _outing.Session.Stage != OutingStage.MapSelecting ||
                !CanDepart(pointId, out var plan, out _) || plan.RequestId != requestId)
                return false;
            bool success = _outing.TrySelectAndDepart(sessionId, pointId, out var failure);
            _feedbackKey = success ? "ready" :
                failure == OutingAssemblyFailure.StationFull ? "full" : "failure";
            if (success) _departed?.Invoke();
            else Refresh();
            return success;
        }

        private void Pause(bool paused)
        {
            if (_disposed) return;
            _paused = paused;
            if (_loop != null) _loop.SetSuspended(paused);
            else
            {
                _outing.Session.SetSuspended(paused);
                _flow.SetSuspended(paused);
            }
            Refresh();
        }

        private void Cancel()
        {
            if (_disposed || _paused || _travelling || _outing.Session.Stage != OutingStage.MapSelecting) return;
            _externalFeedback = null;
            if (!_outing.TryEndUnresolved(_outing.Session.SessionId, out _, out _))
                _feedbackKey = "failure";
            Refresh();
            if (_loop != null && _outing.Session.IsSettled) _returned?.Invoke();
        }

        private void Refresh()
        {
            var session = _outing.Session;
            MapViewSnapshot snapshot = _rules.BuildSnapshot();
            bool selecting = session.Stage == OutingStage.MapSelecting;
            string currentPointId = selecting ? _selection : session.SelectedPointId;
            MapPointView currentPoint = snapshot.Points.SingleOrDefault(point => point.PointId == currentPointId);
            string pointName = currentPoint == null ? string.Empty : _text.Get(currentPoint.DisplayNameKey);
            string heading = selecting ? pointName : session.IsSettled ?
                _text.Get("station") : _text.Get("arrived", pointName);
            string details = selecting ? GetDetails(currentPoint) :
                session.IsSettled ?
                _text.Get(!session.HasDeparted ? "cancelled" : session.AcceptedRequestId == Guid.Empty ?
                    "returned.exploring" : "returned", _outing.LastSummary.ElapsedMinutes) :
                _text.Get(session.AcceptedRequestId == Guid.Empty ? "arrived.exploring" : "arrived.detail");
            string command = selecting ? "depart" : session.IsSettled ? "reopen" : "return";
            bool canDepart = CanDepart(_selection, out MapDeparturePlan plan, out string restriction);
            string feedback = selecting && !canDepart ? restriction :
                _externalFeedback ?? (_feedbackKey == "ready" ? string.Empty : _text.Get(_feedbackKey));
            WorldTimeSnapshot time = _clock.Current;
            _view.RenderTimeOfDay(time.MinuteOfDay, time.MinutesPerDay);
            _view.Render(_text.Get("status", _station.AvailableTemporaryPlaces, time.ElapsedMinutes),
                heading, details, selecting ? GetDeparturePreview(plan) : string.Empty,
                _paused ? _text.Get("paused") : feedback, _text.Get(command),
                selecting, _paused || _travelling, !selecting || canDepart);
            foreach (MapPointView point in snapshot.Points)
                _view.RenderPoint(point.PointId, _text.Get(point.DisplayNameKey),
                    _text.Get("point." + point.DisplayState), point.DisplayState, point.PointId == _selection,
                    selecting && !_paused && !_travelling && point.DisplayState != PointDisplayState.Locked);
        }

        private string GetDetails(MapPointView point)
        {
            string place = _text.Get(_config.GetDetailKey(point.PointId));
            return point.RequestTextKey == null ? _text.Get("detail.noRequest", place) :
                _text.Get("detail.request", place, _text.Get(point.RequestTextKey));
        }

        private bool CanDepart(string pointId, out MapDeparturePlan plan, out string restriction)
        {
            if (!_rules.TryPlanDeparture(pointId, _station.AvailableTemporaryPlaces, out plan, out var failure))
            {
                restriction = _text.Get(failure == OutingFailure.CapacityNotAvailable ? "full" :
                    failure == OutingFailure.PointLocked ? "point.Locked" : "request.unavailable");
                return false;
            }
            restriction = _departureRestriction?.Invoke(plan);
            return string.IsNullOrEmpty(restriction);
        }

        private string GetDeparturePreview(MapDeparturePlan plan)
        {
            // Planning can be rejected by capacity. Read the same plan without that restriction for display only.
            if (plan == null && !_rules.TryPlanDeparture(_selection, int.MaxValue, out plan, out _))
                return string.Empty;
            string space = _text.Get(plan.RequiresStationSpace ? "space.required" : "space.directReception");
            if (!string.IsNullOrEmpty(_departureRestriction?.Invoke(plan))) return space;
            bool entersRescue = _loop != null && plan.EntryKind == MapEntryKind.HelpRequest;
            OutingReturnProgress progress = entersRescue ? OutingReturnProgress.HandingOver :
                OutingReturnProgress.Exploring;
            if (!_config.TimePolicy.TryGetDuration(progress, _hasOpeningRoute, out long minutes, out var failure))
                throw new InvalidOperationException("Cannot preview outing duration: " + failure);
            return _text.Get(entersRescue ? "departure.rescue" : "departure.preview", minutes, space);
        }
    }
}
