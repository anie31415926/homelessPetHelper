using System;
using UnityEngine;

namespace HomelessPetHelper.Game.CityMap.Travel
{
    /// <summary>Per direction: source presentation, one authoritative switch, then destination presentation.</summary>
    [Serializable]
    public sealed class MapTravelSequence
    {
        [SerializeField] private MapTravelStep[] _beforeSwitch = Array.Empty<MapTravelStep>();
        [SerializeField] private MapTravelStep[] _afterSwitch = Array.Empty<MapTravelStep>();
        internal MapTravelStep[] BeforeSwitch => _beforeSwitch;
        internal MapTravelStep[] AfterSwitch => _afterSwitch;
        public bool IsDirect => _beforeSwitch.Length == 0 && _afterSwitch.Length == 0;

        internal void Validate(MapPointVisual visual, RectTransform zoomSurface)
        {
            ValidateSteps(_beforeSwitch, visual, zoomSurface);
            ValidateSteps(_afterSwitch, visual, zoomSurface);
        }

        private static void ValidateSteps(MapTravelStep[] steps, MapPointVisual visual, RectTransform zoomSurface)
        {
            if (steps == null) throw new InvalidOperationException("Missing map travel steps.");
            foreach (var step in steps)
            {
                if (step == null) throw new InvalidOperationException("Null map travel step.");
                step.Validate();
                if (step.Kind == MapTravelStepKind.PointAnimation && visual == null)
                    throw new InvalidOperationException("Point animation requires an explicit visual binding.");
                if (step.Kind == MapTravelStepKind.ZoomAndFade && zoomSurface == null)
                    throw new InvalidOperationException("Zoom requires an explicit visual surface.");
            }
        }
    }
}
