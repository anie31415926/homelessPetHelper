using UnityEngine;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Retains authored anchors and expands only a control's undersized hit rectangle.</summary>
    public sealed class MinimumTouchTarget : MonoBehaviour
    {
        [SerializeField] private UiLayoutMetrics _metrics;
        private Vector2 _authoredSize;
        private RectTransform _rect;
        private bool _applying;
        private void Awake() { _rect = (RectTransform)transform; _authoredSize = _rect.sizeDelta; }
        private void OnRectTransformDimensionsChange()
        {
            if (_rect != null && !_applying) Refresh();
        }
        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            _applying = true;
            var parent = (RectTransform)_rect.parent;
            Vector2 stretched = Vector2.Scale(_rect.anchorMax - _rect.anchorMin, parent.rect.size);
            Vector2 original = stretched + _authoredSize;
            float scale = Mathf.Max(1, _metrics.touchSize / Mathf.Max(1, original.x),
                _metrics.touchSize / Mathf.Max(1, original.y));
            Vector2 desired = original * scale - stretched;
            if (_rect.sizeDelta != desired) _rect.sizeDelta = desired;
            _applying = false;
        }
    }
}
