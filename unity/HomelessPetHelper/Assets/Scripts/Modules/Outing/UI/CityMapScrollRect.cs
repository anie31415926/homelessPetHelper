using System;
using HomelessPetHelper.Framework.UI.Motion;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    /// <summary>A bounded map canvas. Landmarks remain children of the uniformly sized artwork.</summary>
    public sealed class CityMapScrollRect : ScrollRect
    {
        [SerializeField] private Image _mapImage;
        // Design-basis width (1080 canvas). Must stay at or below the viewport width so the art
        // is never upscaled beyond the screen; anything larger crops most of the map on a phone.
        [SerializeField, Min(1)] private float _minimumMapWidth = 1080;

        [SerializeField] private UiMotionFeedback[] _pointFeedback = Array.Empty<UiMotionFeedback>();

        private RectTransform _pendingFocus;
        private int? _dragPointer;

        protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) return;
            if (_mapImage == null || _mapImage.sprite == null || content != _mapImage.rectTransform ||
                viewport == null || _minimumMapWidth <= 0 || float.IsNaN(_minimumMapWidth) ||
                float.IsInfinity(_minimumMapWidth))
                throw new InvalidOperationException("City map scroll has incomplete saved bindings or size.");
        }

        public override void SetLayoutHorizontal()
        {
            ResizeMap();
            base.SetLayoutHorizontal();
        }

        public override void SetLayoutVertical()
        {
            ResizeMap();
            base.SetLayoutVertical();
        }

        /// <summary>Focus only for an explicit selection/opening, never for an ordinary data refresh.</summary>
        public void FocusOn(RectTransform landmark)
        {
            if (landmark == null || !landmark.IsChildOf(content))
                throw new ArgumentException("Focus target must belong to this map.", nameof(landmark));
            _pendingFocus = landmark;
            if (isActiveAndEnabled) CenterOn(landmark);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (_pendingFocus == null) return;
            CenterOn(_pendingFocus);
            _pendingFocus = null;
        }

        private void ResizeMap()
        {
            if (_mapImage == null || _mapImage.sprite == null || content == null || viewport == null) return;
            float aspect = _mapImage.sprite.rect.width / _mapImage.sprite.rect.height;
            float width = Mathf.Max(_minimumMapWidth, viewport.rect.width, viewport.rect.height * aspect);
            var size = new Vector2(width, width / aspect);
            if ((content.sizeDelta - size).sqrMagnitude < .01f) return;
            Vector2 position = normalizedPosition;
            content.sizeDelta = size;
            normalizedPosition = position;
        }

        private void CenterOn(RectTransform landmark)
        {
            ResizeMap();
            StopMovement();
            Vector2 local = content.InverseTransformPoint(landmark.TransformPoint(landmark.rect.center));
            Vector2 travel = content.rect.size - viewport.rect.size;
            Vector2 offset = local - content.rect.min - viewport.rect.size * .5f;
            normalizedPosition = new Vector2(travel.x > 0 ? Mathf.Clamp01(offset.x / travel.x) : .5f,
                travel.y > 0 ? Mathf.Clamp01(offset.y / travel.y) : .5f);
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (!_dragPointer.HasValue) base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _dragPointer.HasValue ||
                eventData.button != PointerEventData.InputButton.Left) return;
            _pendingFocus = null;
            _dragPointer = eventData.pointerId;
            SetPointFeedbackEnabled(false);
            base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_dragPointer == eventData.pointerId) base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_dragPointer != eventData.pointerId) return;
            base.OnEndDrag(eventData);
            _dragPointer = null;
            SetPointFeedbackEnabled(true);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            onValueChanged.AddListener(RefreshPointMotionViewport);
            SetPointFeedbackEnabled(true);
        }

        protected override void OnDisable()
        {
            onValueChanged.RemoveListener(RefreshPointMotionViewport);
            _pendingFocus = null;
            _dragPointer = null;
            StopMovement();
            SetPointFeedbackEnabled(false);
            base.OnDisable();
        }

        private void RefreshPointMotionViewport(Vector2 _)
        {
            foreach (UiMotionFeedback feedback in _pointFeedback)
                if (feedback != null) feedback.RefreshViewportConstraint();
        }

        // The ScrollRect owns drag events even when a gesture starts over a point button.
        private void SetPointFeedbackEnabled(bool enabled)
        {
            foreach (UiMotionFeedback feedback in _pointFeedback)
                if (feedback != null) feedback.enabled = enabled;
        }
    }
}
