using System;
using System.Collections.Generic;
using UnityEngine;
using HomelessPetHelper.Framework.UI.Motion;

namespace HomelessPetHelper.Game.CityMap.Travel
{
    /// <summary>Composite artwork only. Keep this visual hierarchy below the stable point hit root.</summary>
    [DisallowMultipleComponent]
    public sealed class MapPointVisual : MonoBehaviour
    {
        [Serializable]
        private sealed class Part
        {
            public RectTransform target;
            public CanvasGroup opacity;
            public Vector2 offset;
            public Vector2 scale = Vector2.one;
            public float angleDegrees;
            [Range(0, 1)] public float endOpacity = 1;
        }

        private sealed class SavedPart
        {
            internal Part Part;
            internal Vector2 Position;
            internal Vector3 Scale;
            internal Quaternion Rotation;
            internal float Alpha;
        }

        [SerializeField] private Part[] _enter = Array.Empty<Part>();
        [SerializeField] private Part[] _exit = Array.Empty<Part>();
        private SavedPart[] _saved;

        public void Validate()
        {
            ValidateParts(_enter);
            ValidateParts(_exit);
        }

        private void ValidateParts(Part[] parts)
        {
            if (parts == null) throw new InvalidOperationException("Missing composite point parts.");
            var targets = new HashSet<RectTransform>();
            foreach (var part in parts)
                if (part == null || part.target == null || !part.target.IsChildOf(transform) ||
                    part.target == transform || !targets.Add(part.target) ||
                    (part.opacity != null && part.opacity.transform != part.target) ||
                    !MapTravelStep.Finite(part.offset.x) || !MapTravelStep.Finite(part.offset.y) ||
                    !MapTravelStep.Finite(part.scale.x) || !MapTravelStep.Finite(part.scale.y) ||
                    part.scale.x <= 0 || part.scale.y <= 0 || !MapTravelStep.Finite(part.angleDegrees) ||
                    !MapTravelStep.Finite(part.endOpacity) || part.endOpacity < 0 || part.endOpacity > 1)
                    throw new InvalidOperationException("Invalid composite point part.");
        }

        public void Begin(MapTravelDirection direction)
        {
            Restore();
            Validate();
            var parts = direction == MapTravelDirection.Enter ? _enter : _exit;
            _saved = new SavedPart[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                _saved[i] = new SavedPart { Part = parts[i], Position = parts[i].target.anchoredPosition,
                    Scale = parts[i].target.localScale, Rotation = parts[i].target.localRotation,
                    Alpha = parts[i].opacity != null ? parts[i].opacity.alpha : 1 };
        }

        public void Sample(float progress)
        {
            if (_saved == null) return;
            foreach (var saved in _saved)
            {
                var part = saved.Part;
                if (part.target == null) continue;
                var pose = MotionPose.Lerp(MotionPose.Identity,
                    new MotionPose(part.offset, part.scale, part.angleDegrees, part.endOpacity),
                    Mathf.Clamp01(progress));
                part.target.anchoredPosition = saved.Position + pose.Offset;
                part.target.localScale = Vector3.Scale(saved.Scale, new Vector3(pose.Scale.x, pose.Scale.y, 1));
                part.target.localRotation = saved.Rotation * Quaternion.Euler(0, 0, pose.AngleDegrees);
                if (part.opacity != null) part.opacity.alpha = saved.Alpha * pose.Opacity;
            }
        }

        public void Restore()
        {
            if (_saved == null) return;
            foreach (var saved in _saved)
            {
                if (saved.Part.target == null) continue;
                saved.Part.target.anchoredPosition = saved.Position;
                saved.Part.target.localScale = saved.Scale;
                saved.Part.target.localRotation = saved.Rotation;
                if (saved.Part.opacity != null) saved.Part.opacity.alpha = saved.Alpha;
            }
            _saved = null;
        }

        private void OnDisable() => Restore();
    }
}
