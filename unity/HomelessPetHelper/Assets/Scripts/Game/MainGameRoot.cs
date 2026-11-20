using System;
using HomelessPetHelper.Bootstrap;
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
        public MainLoopPresenter Presenter { get; private set; }

        private void Start()
        {
            if (_bootstrap == null || _content == null || _station == null || _map == null || _siege == null)
                throw new InvalidOperationException("Incomplete Main scene bindings.");
            Presenter = new MainLoopPresenter(_station, _map, _siege, _content);
            _bootstrap.BindSession(Presenter.Session);
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.BindSession(null);
            Presenter?.Dispose();
            Presenter = null;
        }
    }
}
