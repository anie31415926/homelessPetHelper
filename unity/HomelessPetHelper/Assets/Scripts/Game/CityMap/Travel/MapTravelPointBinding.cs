using System;
using UnityEngine;

namespace HomelessPetHelper.Game.CityMap.Travel
{
    [Serializable]
    public sealed class MapTravelPointBinding
    {
        [SerializeField] private string _pointId;
        [SerializeField] private MapPointVisual _visual;
        [SerializeField] private RectTransform _zoomSurface;
        [SerializeField] private MapTravelSequence _enter = new MapTravelSequence();
        [SerializeField] private MapTravelSequence _exit = new MapTravelSequence();
        public string PointId => _pointId;
        internal MapPointVisual Visual => _visual;
        internal RectTransform ZoomSurface => _zoomSurface;
        internal MapTravelSequence Sequence(MapTravelDirection direction) =>
            direction == MapTravelDirection.Enter ? _enter : _exit;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(_pointId) || _pointId != _pointId.Trim() || _enter == null || _exit == null)
                throw new InvalidOperationException("Invalid map travel point binding.");
            _visual?.Validate();
            _enter.Validate(_visual, _zoomSurface);
            _exit.Validate(_visual, _zoomSurface);
        }
    }
}
