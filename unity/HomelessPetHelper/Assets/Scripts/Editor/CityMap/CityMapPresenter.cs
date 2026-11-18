using System;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.WorldTime;
using UnityEngine;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Editor-owned slice: real departure and unresolved return, never a fake field success.</summary>
    public sealed class CityMapPresenter : IDisposable
    {
        private static readonly string[] Points = { "parking", "yard", "porch" };
        private readonly CityMapView _view;
        private readonly OptionPreviewText _text;
        private readonly RescueFlow _flow = new RescueFlow();
        private readonly RescueStation _station;
        private readonly ActionWorldClock _clock;
        private readonly OutingCoordinator _outing;
        private int _selection;
        private bool _paused;
        private string _feedbackKey = "ready";

        public CityMapPresenter(CityMapView view, TextAsset text, int capacity = 2)
        {
            _view = view;
            _text = new OptionPreviewText(text);
            Guid stationId = Guid.NewGuid();
            Guid worldId = Guid.NewGuid();
            _station = new RescueStation(stationId, capacity);
            _clock = new ActionWorldClock(worldId, 1440);
            var pool = new RequestPoolState();
            pool.TryAdd(new HelpRequest(Guid.NewGuid(), "ordinary", "parking", 1), out _);
            Guid petId = Guid.NewGuid();
            pool.TryAdd(new HelpRequest(petId, "reunion", "porch", 1,
                new OwnerReunionPlan(petId, "owner", "safe-receiving")), out _);
            var map = new CityMapDefinition("neighborhood", new[] { "nearby" }, new[]
            {
                new MapPointDefinition("parking", "parking", "nearby", true, new[] { "shed" }),
                new MapPointDefinition("yard", "yard", "nearby", true, new[] { "yard" }),
                new MapPointDefinition("porch", "porch", "nearby", true, new[] { "porch" })
            });
            var rules = new CityMapRules(map, new PointUnlockState(), pool, new[]
            {
                new HelpRequestTemplate("ordinary", "ordinary", "cat", new[] { "parking" }),
                new HelpRequestTemplate("reunion", "reunion", "dog", new[] { "porch" })
            });
            // Trial tuning only. No real-time ticking or per-click time charges.
            _outing = new OutingCoordinator(_flow, _station, stationId, _clock, worldId, rules, pool,
                new OutingTimePolicy(5, 10, 15, 5));
            _view.PlaceSelected += Select;
            _view.PrimaryClicked += Act;
            _view.CancelClicked += Cancel;
            _view.PauseChanged += Pause;
            Begin();
        }

        public void Dispose()
        {
            _view.PlaceSelected -= Select;
            _view.PrimaryClicked -= Act;
            _view.CancelClicked -= Cancel;
            _view.PauseChanged -= Pause;
        }

        private void Begin()
        {
            if (!_outing.TryBeginOuting(Guid.NewGuid(), 20261118, out var failure))
                throw new InvalidOperationException("Cannot open map trial: " + failure);
            _selection = 0;
            _feedbackKey = "ready";
            Refresh();
        }

        private void Select(int index)
        {
            if (_paused || _outing.Session.Stage != OutingStage.MapSelecting) return;
            _selection = index;
            _feedbackKey = "ready";
            Refresh();
        }

        private void Act()
        {
            if (_paused) return;
            var session = _outing.Session;
            if (session.IsSettled)
            {
                Begin();
                return;
            }

            bool success = session.Stage == OutingStage.MapSelecting ?
                _outing.TrySelectAndDepart(session.SessionId, Points[_selection], out var failure) :
                _outing.TryEndUnresolved(session.SessionId, out _, out failure);
            _feedbackKey = success ? "ready" :
                failure == OutingAssemblyFailure.StationFull ? "full" : "failure";
            Refresh();
        }

        private void Pause(bool paused)
        {
            _paused = paused;
            _outing.Session.SetSuspended(paused);
            _flow.SetSuspended(paused);
            Refresh();
        }

        private void Cancel()
        {
            if (_paused || _outing.Session.Stage != OutingStage.MapSelecting) return;
            if (!_outing.TryEndUnresolved(_outing.Session.SessionId, out _, out _))
                _feedbackKey = "failure";
            Refresh();
        }

        private void Refresh()
        {
            var session = _outing.Session;
            bool selecting = session.Stage == OutingStage.MapSelecting;
            string heading = selecting ? _text.Get(Points[_selection]) : session.IsSettled ?
                _text.Get("station") : _text.Get("arrived", _text.Get(Points[_selection]));
            string details = selecting ? _text.Get(Points[_selection] + ".detail") : session.IsSettled ?
                _text.Get(!session.HasDeparted ? "cancelled" : session.AcceptedRequestId == Guid.Empty ?
                    "returned.exploring" : "returned", _outing.LastSummary.ElapsedMinutes) :
                _text.Get(session.AcceptedRequestId == Guid.Empty ? "arrived.exploring" : "arrived.detail");
            string command = selecting ? "depart" : session.IsSettled ? "reopen" : "return";
            string feedback = !selecting && _feedbackKey == "ready" ? string.Empty : _text.Get(_feedbackKey);
            _view.Render(_text.Get("status", _station.AvailableTemporaryPlaces, _clock.Current.ElapsedMinutes),
                heading, details, _paused ? _text.Get("paused") : feedback, _text.Get(command),
                _selection, selecting, _paused);
        }
    }
}
