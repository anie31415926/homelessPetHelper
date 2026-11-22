using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Fits a light guide caption into free safe-area space without covering permitted input.</summary>
    public sealed class GuideCaptionLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform _safeArea;
        [SerializeField] private Text _text;
        [SerializeField] private UiLayoutMetrics _metrics;
        private readonly List<RectTransform> _targets = new List<RectTransform>(16);
        private readonly List<Rect> _obstacles = new List<Rect>(64);
        private readonly List<float> _xs = new List<float>(132), _ys = new List<float>(132);
        private readonly Selectable[] _controls = new Selectable[256];
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;
        private Canvas _canvas;
        private int _stamp;

        public void SetTargets(IReadOnlyList<RectTransform> targets)
        {
            _targets.Clear();
            foreach (var target in targets) _targets.Add(target);
            _stamp = 0;
        }

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>().rootCanvas;
            _stamp = 0;
        }

        private void OnDisable() { _targets.Clear(); _obstacles.Clear(); _stamp = 0; }

        private void LateUpdate()
        {
            if (_targets.Count == 0) return;
            CollectObstacles();
            int stamp = _safeArea.rect.GetHashCode() ^ _text.text.GetHashCode();
            foreach (var obstacle in _obstacles) unchecked { stamp = stamp * 31 + obstacle.GetHashCode(); }
            if (stamp == _stamp) return;
            _stamp = stamp;
            Fit();
        }

        private void CollectObstacles()
        {
            _obstacles.Clear();
            foreach (var target in _targets) AddObstacle(target);
            int count = Selectable.AllSelectablesNoAlloc(_controls);
            for (int index = 0; index < count; index++)
            {
                var control = _controls[index];
                if (control != null && control.isActiveAndEnabled &&
                    control.transform.IsChildOf(_canvas.transform)) AddObstacle((RectTransform)control.transform);
                _controls[index] = null;
            }
        }

        private void AddObstacle(RectTransform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            target.GetWorldCorners(_corners);
            Vector2 min = _safeArea.InverseTransformPoint(_corners[0]);
            Vector2 max = _safeArea.InverseTransformPoint(_corners[2]);
            var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            if (!_obstacles.Contains(rect)) _obstacles.Add(rect);
        }

        private void Fit()
        {
            Rect safe = _safeArea.rect;
            // Keep the top centre clear even when a simulated safe area has no camera cutout.
            var available = Rect.MinMaxRect(safe.xMin + _metrics.margin, safe.yMin + _metrics.margin,
                safe.xMax - _metrics.margin, safe.yMax - _metrics.touchSize - _metrics.margin);
            _rect.anchorMin = _rect.anchorMax = new Vector2(.5f, .5f);
            _rect.pivot = new Vector2(.5f, .5f);
            bool placed = TryFit(Mathf.Min(_metrics.guideMaxWidth, available.width), available);
            // A wider safe-area strip can remove a wrapped line when a short screen has no tall opening.
            if (!placed && available.width > _metrics.guideMaxWidth)
                placed = TryFit(available.width, available);
            _text.enabled = placed;
            var surface = GetComponent<Image>();
            if (surface != null) surface.enabled = placed;
        }

        private bool TryFit(float width, Rect available)
        {
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            float height = _text.preferredHeight + 2 * _metrics.gap;
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (width <= 0 || height > available.height) return false;
            _xs.Clear(); _ys.Clear();
            AddCandidate(_xs, available.xMin, available.xMin, available.xMax - width);
            AddCandidate(_xs, available.center.x - width / 2, available.xMin, available.xMax - width);
            AddCandidate(_xs, available.xMax - width, available.xMin, available.xMax - width);
            AddCandidate(_ys, available.yMin, available.yMin, available.yMax - height);
            AddCandidate(_ys, available.yMax - height, available.yMin, available.yMax - height);
            foreach (var obstacle in _obstacles)
            {
                AddCandidate(_xs, obstacle.xMin - width - _metrics.gap, available.xMin, available.xMax - width);
                AddCandidate(_xs, obstacle.xMax + _metrics.gap, available.xMin, available.xMax - width);
                AddCandidate(_ys, obstacle.yMin - height - _metrics.gap, available.yMin, available.yMax - height);
                AddCandidate(_ys, obstacle.yMax + _metrics.gap, available.yMin, available.yMax - height);
            }
            return PlaceCandidate(width, height, available);
        }

        private bool PlaceCandidate(float width, float height, Rect available)
        {
            Vector2 preferred = _obstacles.Count == 0 ? available.center : _obstacles[0].center;
            Rect best = new Rect(available.center - new Vector2(width, height) / 2, new Vector2(width, height));
            float bestScore = float.PositiveInfinity;
            foreach (float x in _xs)
                foreach (float y in _ys)
                {
                    var candidate = new Rect(x, y, width, height);
                    if (OverlapsObstacle(candidate)) continue;
                    float score = (candidate.center - preferred).sqrMagnitude;
                    if (score >= bestScore) continue;
                    best = candidate; bestScore = score;
                }
            if (float.IsPositiveInfinity(bestScore)) return false;
            _rect.anchoredPosition = best.center - _safeArea.rect.center;
            return true;
        }

        private bool OverlapsObstacle(Rect rect)
        {
            foreach (var obstacle in _obstacles)
                if (rect.Overlaps(obstacle)) return true;
            return false;
        }

        private static void AddCandidate(List<float> values, float value, float min, float max)
        {
            value = Mathf.Clamp(value, min, Mathf.Max(min, max));
            if (!values.Contains(value)) values.Add(value);
        }
    }
}
