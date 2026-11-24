using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI.Motion
{
    /// <summary>Animates an authored visual child; the Selectable hit rectangle stays fixed.</summary>
    public sealed class UiMotionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler, ICancelHandler
    {
        [SerializeField] private MotionScope _scope;
        [SerializeField] private Selectable _control;
        [SerializeField] private RectTransform _visual;
        [SerializeField] private RectTransform _hoverViewport;
        private bool _hovered, _selected, _dragging;
        private int? _pointer;
        private MotionCue _cue;
        private MotionPose _from = MotionPose.Identity, _pose = MotionPose.Identity;
        private float _startedAt;
        private bool _transitioning;

        public void BindScope(MotionScope scope)
        {
            if (_scope == scope) return;
            ResetFeedback();
            _scope = scope;
        }

        public void SetSelected(bool selected) => _selected = selected;
        public void ResetFeedback()
        {
            _hovered = _selected = _transitioning = _dragging = false;
            _pointer = null;
            _cue = MotionCue.None;
            _from = _pose = MotionPose.Identity;
            if (_visual != null) Apply(_pose);
        }

        private bool CanRespond => _scope != null && _scope.isActiveAndEnabled &&
            _control != null && _control.IsActive() && _control.IsInteractable();
        private void OnEnable() => ResetFeedback();
        private void OnDisable() => ResetFeedback();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetFeedback(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetFeedback(); }

        public void OnPointerEnter(PointerEventData data)
        { if (data.pointerId < 0 && CanRespond) _hovered = true; }
        public void OnPointerExit(PointerEventData data)
        { if (data.pointerId < 0) _hovered = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (CanRespond && !_pointer.HasValue && data.button == PointerEventData.InputButton.Left)
                _pointer = data.pointerId;
        }
        public void OnPointerUp(PointerEventData data)
        { if (_pointer == data.pointerId) _pointer = null; }
        public void OnBeginDrag(PointerEventData data)
        {
            _pointer = null;
            _hovered = false;
            _dragging = true;
        }
        public void OnEndDrag(PointerEventData data) => _dragging = false;
        public void OnCancel(BaseEventData data) => ResetFeedback();

        private void LateUpdate()
        {
            if (_scope == null || _visual == null) return;
            if (!CanRespond)
            {
                if (_cue != MotionCue.None || _transitioning || _pointer.HasValue || _hovered || _selected || _dragging)
                    ResetFeedback();
                return;
            }
            var cue = _dragging ? MotionCue.None : _pointer.HasValue ? MotionCue.Press :
                _selected ? MotionCue.Selected :
                _hovered ? MotionCue.Hover : MotionCue.None;
            if (cue != _cue)
            {
                _cue = cue;
                _from = _pose;
                _startedAt = _scope.UiSeconds;
                _transitioning = true;
            }
            if (!_transitioning) return;
            var curve = cue == MotionCue.None ? MotionCue.Hover : cue;
            float elapsed = _scope.UiSeconds - _startedAt;
            _pose = MotionPose.Lerp(_from, _scope.Library.Target(cue), _scope.Library.Weight(curve, elapsed));
            Apply(_pose);
            _transitioning = elapsed < _scope.Library.Duration(curve);
        }

        private void Apply(MotionPose pose)
        {
            _visual.anchoredPosition = pose.Offset;
            _visual.localRotation = Quaternion.Euler(0, 0, pose.AngleDegrees);
            SetVisualScale(pose.Scale);
        }

        /// <summary>Recheck a held hover when its scroll viewport moves relative to the visual.</summary>
        public void RefreshViewportConstraint()
        {
            if (_hoverViewport != null && isActiveAndEnabled && IsGrowing(_pose.Scale))
                SetVisualScale(_pose.Scale);
        }

        private void SetVisualScale(Vector2 scale)
        {
            if (_hoverViewport != null && IsGrowing(scale))
                scale = ConstrainGrowth(scale);
            _visual.localScale = new Vector3(scale.x, scale.y, 1);
        }

        private static bool IsGrowing(Vector2 scale) => scale.x > 1 || scale.y > 1;

        private Vector2 ConstrainGrowth(Vector2 scale)
        {
            var toViewport = _hoverViewport.worldToLocalMatrix * _visual.parent.localToWorldMatrix;
            var origin = toViewport.MultiplyPoint3x4(_visual.localPosition);
            var rotation = _visual.localRotation;
            var rect = _visual.rect;
            var viewport = _hoverViewport.rect;
            float allowed = 1;
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = new Vector2((i & 1) == 0 ? rect.xMin : rect.xMax, i < 2 ? rect.yMin : rect.yMax);
                Vector3 position = origin + toViewport.MultiplyVector(rotation * corner);
                if (!viewport.Contains(position)) return Vector2.one;
                Vector3 growth = toViewport.MultiplyVector(rotation * Vector2.Scale(corner, scale - Vector2.one));
                allowed = Mathf.Min(allowed, AllowedGrowth(position.x, growth.x, viewport.xMin, viewport.xMax));
                allowed = Mathf.Min(allowed, AllowedGrowth(position.y, growth.y, viewport.yMin, viewport.yMax));
            }
            return Vector2.Lerp(Vector2.one, scale, allowed);
        }

        private static float AllowedGrowth(float position, float growth, float min, float max) =>
            growth > 0 ? (max - position) / growth : growth < 0 ? (min - position) / growth : 1;
    }
}
