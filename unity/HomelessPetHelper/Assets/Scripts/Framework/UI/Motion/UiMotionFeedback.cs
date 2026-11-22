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
            _visual.localScale = new Vector3(pose.Scale.x, pose.Scale.y, 1);
            _visual.anchoredPosition = pose.Offset;
            _visual.localRotation = Quaternion.Euler(0, 0, pose.AngleDegrees);
        }
    }
}
