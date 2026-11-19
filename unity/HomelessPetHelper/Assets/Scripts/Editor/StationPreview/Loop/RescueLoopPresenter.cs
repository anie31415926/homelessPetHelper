using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Editor.CityMap;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.StationPreview.Loop
{
    /// <summary>Editor-only assembly of saved panels. Provisional rescue rules stay behind IRescueActivity.</summary>
    public sealed class RescueLoopPresenter : IDisposable
    {
        private readonly StationPanelView _stationView;
        private readonly CityMapView _mapView;
        private readonly RescueCaseView _rescueView;
        private readonly CityMapPreviewConfig _config;
        private readonly RescueLoopConfig _encounters;
        private readonly StationPreviewSession _stationPresenter;
        private readonly RescueOptionContentLoader _options;
        private readonly OptionPreviewText _text;
        private CityMapPresenter _mapPresenter;
        private RescueCasePresenter _rescuePresenter;
        private bool _disposed;

        public GameLoopSession Session { get; }
        public RescueCaseTrial Trial => _rescuePresenter?.Trial;

        public RescueLoopPresenter(StationPanelView station, CityMapView map, RescueCaseView rescue)
        {
            _stationView = station;
            _mapView = map;
            _rescueView = rescue;
            _config = new CityMapPreviewConfig(Load(CityMapAssets.ConfigPath));
            _encounters = new RescueLoopConfig(Load(RescueLoopAssets.ConfigPath));
            _options = new RescueOptionContentLoader(Load(RescueCaseAssets.OptionsPath),
                Load(RescueCaseAssets.OptionTextPath));
            _text = new OptionPreviewText(Load(StationPreviewAssets.TextPath));
            RequestPoolState pool = _config.CreateRequestPool();
            Session = new GameLoopSession(_config.InitialCapacity, _config.MinutesPerDay,
                _config.CreateRules(new PointUnlockState(), pool), pool, _config.TimePolicy);
            _mapView.gameObject.SetActive(false);
            _rescueView.gameObject.SetActive(false);
            _stationView.gameObject.SetActive(true);
            _stationPresenter = new StationPreviewSession(station, Load(StationPreviewAssets.TextPath),
                Session, OpenMap);
            Session.SetSuspended(false);
        }

        private static TextAsset Load(string path) => AssetDatabase.LoadAssetAtPath<TextAsset>(path);

        private void OpenMap()
        {
            if (!Session.TryOpenMap(Guid.NewGuid(), _config.Seed, out var failure))
            {
                _stationPresenter.ReportOutingFailure(failure);
                return;
            }
            _stationView.gameObject.SetActive(false);
            Session.SetSuspended(false);
            _mapView.gameObject.SetActive(true);
            _mapPresenter = new CityMapPresenter(_mapView, Load(CityMapAssets.TextPath), _config,
                Session, EnterEncounter, () => ShowStation(false));
        }

        private void EnterEncounter()
        {
            OutingSession outing = Session.Outing.Session;
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest ||
                !Session.Requests.TryGet(outing.AcceptedRequestId, out var request) ||
                !_config.Definition.TryGetPoint(outing.SelectedPointId, out var point)) return;
            HelpRequestTemplate template = _config.GetRequestTemplate(request.TemplateId);
            if (template == null || !_encounters.Supports(point.SceneTemplateIds, template.AnimalDefinitionId)) return;
            var trial = new RescueCaseTrial(outing.SessionId, outing.Seed, catalog: _options.Catalog,
                standaloneStation: false, individual: RescueCaseAnimalFixture.Create(CaseStart.Normal));
            if (!Session.TryEnterRouteFreeRescue(trial, out var failure))
            {
                trial.Dispose();
                _mapPresenter.ShowFeedback(_text.Get("loop.failure." + failure));
                return;
            }
            _mapPresenter.Dispose();
            _mapPresenter = null;
            _mapView.gameObject.SetActive(false);
            Session.SetSuspended(false);
            _rescueView.gameObject.SetActive(true);
            _rescuePresenter = new RescueCasePresenter(_rescueView, Load(RescueCaseAssets.TextPath), _options,
                trial: trial);
            _rescuePresenter.RescueCompleted += ConfirmRescue;
            _rescuePresenter.ReturnRequested += ReturnRescued;
            _rescuePresenter.ExitRequested += ReturnUnresolved;
            _rescuePresenter.SuspensionChanged += Session.SetSuspended;
        }

        private void ConfirmRescue()
        {
            if (!Session.TryConfirmRescue(out var failure))
                throw new InvalidOperationException("Completed rescue could not be confirmed: " + failure);
        }

        private void ReturnRescued()
        {
            var animal = Session.Activity.Individual.Current;
            var condition = new AnimalIntakeCondition(StationHealthAssessment.Unchecked, animal.Coat.Dirtiness,
                animal.Health.Problems.Select(problem => problem.InstanceId).ToArray());
            if (Session.TryReturnRescued(condition, out var failure)) ShowStation(true);
            else _rescuePresenter.ShowFeedback(_text.Get("loop.failure." + failure));
        }

        private void ReturnUnresolved()
        {
            if (Session.TryReturnUnresolved(out var failure)) ShowStation(false);
            else _rescuePresenter.ShowFeedback(_text.Get("loop.failure." + failure));
        }

        private void ShowStation(bool showReturn)
        {
            CloseFieldPresenters();
            _mapView.gameObject.SetActive(false);
            _rescueView.gameObject.SetActive(false);
            Session.SetSuspended(false);
            _stationPresenter.ShowSharedStation(showReturn);
            _stationView.gameObject.SetActive(true);
        }

        private void CloseFieldPresenters()
        {
            _mapPresenter?.Dispose();
            _mapPresenter = null;
            if (_rescuePresenter == null) return;
            _rescuePresenter.RescueCompleted -= ConfirmRescue;
            _rescuePresenter.ReturnRequested -= ReturnRescued;
            _rescuePresenter.ExitRequested -= ReturnUnresolved;
            _rescuePresenter.SuspensionChanged -= Session.SetSuspended;
            _rescuePresenter.Dispose();
            _rescuePresenter = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CloseFieldPresenters();
            _stationPresenter.Dispose();
            Session.Dispose();
        }
    }
}
