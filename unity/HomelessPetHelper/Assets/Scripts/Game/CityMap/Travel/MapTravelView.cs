using System;
using System.Collections.Generic;
using UnityEngine;

namespace HomelessPetHelper.Game.CityMap.Travel
{
    /// <summary>Main-owned presentation. Never persists progress or rolls back committed business.</summary>
    [DisallowMultipleComponent]
    public sealed class MapTravelView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _cover;
        [SerializeField] private CanvasGroup[] _inputRoots = Array.Empty<CanvasGroup>();
        [SerializeField] private MapTravelPointBinding[] _points = Array.Empty<MapTravelPointBinding>();
        private readonly Dictionary<string, MapTravelPointBinding> _lookup =
            new Dictionary<string, MapTravelPointBinding>(StringComparer.Ordinal);
        private MapTravelPointBinding _point;
        private MapTravelStep[] _steps;
        private MapTravelDirection _direction;
        private Func<bool> _canContinue, _commit;
        private Action<bool> _finished;
        private bool[] _interactable, _raycasts;
        private Vector2 _zoomPosition;
        private Vector3 _zoomScale;
        private int _index, _revision;
        private float _elapsed;
        private bool _afterSwitch, _committed, _focused = true, _backgrounded, _validated;
        public bool IsPlaying { get; private set; }

        public void Validate(IEnumerable<string> pointIds)
        {
            if (_cover == null || _points == null || _inputRoots == null)
                throw new InvalidOperationException("Incomplete map travel bindings.");
            var ids = new HashSet<string>(pointIds, StringComparer.Ordinal);
            var lookup = new Dictionary<string, MapTravelPointBinding>(StringComparer.Ordinal);
            foreach (var point in _points)
            {
                if (point == null) throw new InvalidOperationException("Null map travel binding.");
                point.Validate();
                if (!ids.Contains(point.PointId) || lookup.ContainsKey(point.PointId))
                    throw new InvalidOperationException("Unknown or duplicate travel point: " + point.PointId);
                lookup.Add(point.PointId, point);
            }
            var roots = new HashSet<CanvasGroup>();
            foreach (var root in _inputRoots)
                if (root == null || !roots.Add(root) || _cover.transform.IsChildOf(root.transform))
                    throw new InvalidOperationException("Travel cover must be outside unique input roots.");
            _lookup.Clear();
            foreach (var pair in lookup) _lookup.Add(pair.Key, pair.Value);
            ResetCover();
            _validated = true;
        }

        /// <summary>Returns false without callbacks when busy/inactive. Accepted requests finish exactly once.
        /// The commit callback revalidates business and switches views; false keeps its source unchanged.</summary>
        public bool TryPlay(string pointId, MapTravelDirection direction, Func<bool> canContinue,
            Func<bool> commit, Action<bool> finished)
        {
            if (canContinue == null || commit == null || finished == null)
                throw new ArgumentNullException(nameof(commit));
            if (!Enum.IsDefined(typeof(MapTravelDirection), direction))
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (!_validated) throw new InvalidOperationException("Validate map travel before playback.");
            if (IsPlaying || !isActiveAndEnabled || !_focused || _backgrounded) return false;
            if (string.IsNullOrWhiteSpace(pointId)) throw new ArgumentException("Missing travel point ID.");
            _lookup.TryGetValue(pointId, out _point);
            _direction = direction;
            _canContinue = canContinue;
            _commit = commit;
            _finished = finished;
            _afterSwitch = _committed = false;
            _index = 0;
            _elapsed = 0;
            _revision++;
            IsPlaying = true;
            LockInput();
            try
            {
                if (_point?.ZoomSurface != null)
                {
                    _zoomPosition = _point.ZoomSurface.anchoredPosition;
                    _zoomScale = _point.ZoomSurface.localScale;
                }
                _point?.Visual?.Begin(direction);
                _steps = _point?.Sequence(direction).BeforeSwitch ?? Array.Empty<MapTravelStep>();
                Advance(0);
            }
            catch
            {
                Cancel();
                throw;
            }
            return true;
        }

        private void Update()
        {
            if (!IsPlaying || !_focused || _backgrounded) return;
            try { Advance(Mathf.Min(Time.unscaledDeltaTime, .1f)); }
            catch (Exception cause)
            {
                Cancel();
                Debug.LogException(cause, this);
            }
        }

        private void Advance(float seconds)
        {
            if (!_afterSwitch && !_canContinue()) { Cancel(); return; }
            if (_index < _steps.Length)
            {
                var step = _steps[_index];
                _elapsed = Mathf.Min(_elapsed + seconds, step.DurationSeconds);
                float progress = Mathf.SmoothStep(0, 1, _elapsed / step.DurationSeconds);
                Sample(step, progress);
                if (_elapsed < step.DurationSeconds) return;
                _index++;
                _elapsed = 0;
                if (_index < _steps.Length) return;
            }
            if (!_afterSwitch)
            {
                int revision = _revision;
                // The callback owns all rules/persistence. It may disable the owner or cancel this request.
                bool accepted = _canContinue() && _commit();
                if (!IsPlaying || revision != _revision) return;
                _committed = accepted;
                if (!accepted) { Finish(false); return; }
                _afterSwitch = true;
                _steps = _point?.Sequence(_direction).AfterSwitch ?? Array.Empty<MapTravelStep>();
                _index = 0;
                if (_steps.Length != 0) return;
            }
            Finish(true);
        }

        private void Sample(MapTravelStep step, float progress)
        {
            if (step.Kind == MapTravelStepKind.PointAnimation)
            {
                _point.Visual.Sample(progress);
                return;
            }
            _cover.alpha = step.Opacity(progress);
            if (step.Kind != MapTravelStepKind.ZoomAndFade) return;
            var surface = _point.ZoomSurface;
            surface.anchoredPosition = _zoomPosition + step.Offset * progress;
            surface.localScale = _zoomScale * Mathf.Lerp(1, step.Scale, progress);
        }

        public void Cancel()
        {
            if (IsPlaying) Finish(_committed);
        }

        private void Finish(bool committed)
        {
            var finished = _finished;
            IsPlaying = false;
            _revision++;
            _finished = null;
            _commit = _canContinue = null;
            _steps = null;
            _point?.Visual?.Restore();
            if (_point?.ZoomSurface != null)
            {
                _point.ZoomSurface.anchoredPosition = _zoomPosition;
                _point.ZoomSurface.localScale = _zoomScale;
            }
            _point = null;
            UnlockInput();
            ResetCover();
            finished?.Invoke(committed);
        }

        private void LockInput()
        {
            _interactable = new bool[_inputRoots.Length];
            _raycasts = new bool[_inputRoots.Length];
            for (int i = 0; i < _inputRoots.Length; i++)
            {
                _interactable[i] = _inputRoots[i].interactable;
                _raycasts[i] = _inputRoots[i].blocksRaycasts;
                _inputRoots[i].interactable = _inputRoots[i].blocksRaycasts = false;
            }
            _cover.blocksRaycasts = true;
        }

        private void UnlockInput()
        {
            if (_interactable == null) return;
            for (int i = 0; i < _inputRoots.Length; i++)
            {
                if (_inputRoots[i] == null) continue;
                _inputRoots[i].interactable = _interactable[i];
                _inputRoots[i].blocksRaycasts = _raycasts[i];
            }
            _interactable = _raycasts = null;
        }

        private void ResetCover()
        {
            if (_cover == null) return;
            _cover.alpha = 0;
            _cover.interactable = false;
            _cover.blocksRaycasts = false;
        }

        private void OnDisable() { Cancel(); ResetCover(); }
        private void OnApplicationFocus(bool focused) => _focused = focused;
        private void OnApplicationPause(bool paused) => _backgrounded = paused;
    }
}
