using System;
using UnityEngine;

namespace HomelessPetHelper.Game.CityMap.Travel
{
    public enum MapTravelStepKind { PointAnimation, Fade, ZoomAndFade }

    [Serializable]
    public sealed class MapTravelStep
    {
        [SerializeField] private MapTravelStepKind _kind;
        [SerializeField] private float _durationSeconds = .25f;
        [SerializeField, Range(0, 1)] private float _fromOpacity;
        [SerializeField, Range(0, 1)] private float _toOpacity = 1;
        [SerializeField] private Vector2 _offset;
        [SerializeField] private float _scale = 1.2f;
        public MapTravelStepKind Kind => _kind;
        public float DurationSeconds => _durationSeconds;
        public float Opacity(float progress) => Mathf.Lerp(_fromOpacity, _toOpacity, progress);
        public Vector2 Offset => _offset;
        public float Scale => _scale;

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(MapTravelStepKind), _kind) || !Finite(_durationSeconds) ||
                _durationSeconds <= 0 || _durationSeconds > 30 || !Finite(_fromOpacity) ||
                !Finite(_toOpacity) || _fromOpacity < 0 || _fromOpacity > 1 ||
                _toOpacity < 0 || _toOpacity > 1 || !Finite(_scale) || _scale <= 0 ||
                !Finite(_offset.x) || !Finite(_offset.y))
                throw new InvalidOperationException("Invalid map travel step.");
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
