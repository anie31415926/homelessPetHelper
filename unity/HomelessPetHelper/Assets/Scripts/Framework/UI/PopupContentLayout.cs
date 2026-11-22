using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Bounds content-driven saved popups; only their body scrolls on short screens.</summary>
    public sealed class PopupContentLayout : MonoBehaviour
    {
        [SerializeField] private UiLayoutMetrics _metrics;
        [SerializeField] private RectTransform _page, _viewport, _content;
        [SerializeField] private LayoutElement _bodySize;
        [SerializeField] private ScrollRect _scroll;
        private Vector2 _size = Vector2.negativeInfinity;
        private float _preferred = -1, _fixedHeight = -1;

        private void OnEnable() { _size = Vector2.negativeInfinity; _scroll.verticalNormalizedPosition = 1; }
        private void LateUpdate()
        {
            var parent = (RectTransform)_page.parent;
            float preferred = LayoutUtility.GetPreferredHeight(_content);
            _page.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Min(_metrics.popupMaxWidth, parent.rect.width - 2 * _metrics.margin));
            var layout = _page.GetComponent<VerticalLayoutGroup>();
            float fixedHeight = layout.padding.vertical;
            int count = 0;
            foreach (RectTransform child in _page)
            {
                if (!child.gameObject.activeSelf) continue;
                count++;
                if (child != _viewport) fixedHeight += LayoutUtility.GetPreferredHeight(child);
            }
            fixedHeight += Mathf.Max(0, count - 1) * layout.spacing;
            if (_size == parent.rect.size && Mathf.Approximately(_preferred, preferred) &&
                Mathf.Approximately(_fixedHeight, fixedHeight)) return;
            _size = parent.rect.size; _preferred = preferred; _fixedHeight = fixedHeight;
            float available = Mathf.Max(_metrics.touchSize, parent.rect.height *
                _metrics.popupHeightRatio - fixedHeight);
            _bodySize.minHeight = _bodySize.preferredHeight = Mathf.Min(preferred, available);
            _page.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                fixedHeight + _bodySize.preferredHeight);
            _scroll.vertical = preferred > available + 1;
            if (!_scroll.vertical) _scroll.verticalNormalizedPosition = 1;
        }
    }
}
