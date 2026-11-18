using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    public sealed class HoldActionButton : Button, IDragHandler, IBeginDragHandler
    {
        [SerializeField] private Image _progress;
        [SerializeField, Min(0.1f)] private float _holdSeconds = 0.4f;
        private bool _holding;
        private int _pointerId;
        private Vector2 _origin;
        private Vector2Int _resolution;
        private float _elapsed;

        public event Action Confirmed;

        public override void OnPointerDown(PointerEventData data)
        {
            if (_holding)
            {
                CancelHold();
                return;
            }

            if (data.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            base.OnPointerDown(data);
            _holding = true;
            _pointerId = data.pointerId;
            _origin = data.position;
            _resolution = new Vector2Int(Screen.width, Screen.height);
            _elapsed = 0;
        }

        public override void OnPointerUp(PointerEventData data)
        {
            base.OnPointerUp(data);
            if (data.pointerId == _pointerId) CancelHold();
        }

        public override void OnPointerExit(PointerEventData data)
        {
            base.OnPointerExit(data);
            if (data.pointerId == _pointerId) CancelHold();
        }

        public void OnBeginDrag(PointerEventData data) => CancelHold();
        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId == _pointerId && Vector2.Distance(data.position, _origin) > 12) CancelHold();
        }

        // Pointer clicks never submit. EventSystem submit remains an accessible keyboard alternative.
        public override void OnPointerClick(PointerEventData data) { }
        public override void OnSubmit(BaseEventData data)
        {
            CancelHold();
            if (IsActive() && IsInteractable()) Confirmed?.Invoke();
        }

        private void Update()
        {
            if (!_holding) return;
            if (!IsInteractable() || Input.touchCount > 1 ||
                _resolution != new Vector2Int(Screen.width, Screen.height))
            {
                CancelHold();
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_elapsed / _holdSeconds);
            _progress.fillAmount = progress;
            _progress.enabled = true;
            _progress.rectTransform.anchorMax = new Vector2(progress, 1);
            if (_elapsed < _holdSeconds) return;
            // Clear before callbacks: submitting may synchronously return this cell to the pool.
            CancelHold();
            Confirmed?.Invoke();
        }

        public void CancelHold()
        {
            _holding = false;
            _elapsed = 0;
            if (_progress != null)
            {
                _progress.fillAmount = 0;
                _progress.enabled = false;
                _progress.rectTransform.anchorMax = new Vector2(0, 1);
            }
        }

        protected override void OnDisable()
        {
            CancelHold();
            base.OnDisable();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) CancelHold(); }
        private void OnApplicationPause(bool paused) { if (paused) CancelHold(); }
    }
}
