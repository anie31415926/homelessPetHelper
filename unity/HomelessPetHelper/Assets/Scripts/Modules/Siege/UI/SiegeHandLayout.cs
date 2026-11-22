using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    /// <summary>Capacity-aware saved dock. Keeps the primary action stable and reserves the scene above it.</summary>
    public sealed class SiegeHandLayout : MonoBehaviour
    {
        [SerializeField] private UiLayoutMetrics _metrics;
        [SerializeField] private RectTransform _hand, _submit, _hint, _progress, _sceneArea, _context;
        [SerializeField] private GridLayoutGroup _grid;
        [SerializeField] private RectTransform _empty;
        [SerializeField] private RectTransform _routes;
        private int _capacity = 1;
        private Vector2 _size = Vector2.negativeInfinity;
        private float _buttonWidth = -1;
        private bool _contextVisible;
        private bool _hintVisible;
        private float _contextHeight;
        public float ContextHeight => _context == null ? 0 : _context.rect.height;
        public float ContextCenter { get; private set; }
        public float CardWidth => _grid.cellSize.x;
        public int Revision { get; private set; }
        public void SetCapacity(int capacity)
        {
            if (_capacity == Mathf.Max(1, capacity)) return;
            _capacity = Mathf.Max(1, capacity); _size = Vector2.negativeInfinity;
        }
        private void OnEnable() => _size = Vector2.negativeInfinity;
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            var safe = (RectTransform)transform;
            float buttonWidth = _metrics.actionWidth;
            var label = _submit.GetComponentInChildren<Text>();
            if (label != null) buttonWidth = Mathf.Max(buttonWidth, label.preferredWidth + _metrics.margin);
            bool contextVisible = _context != null && _context.gameObject.activeSelf;
            bool hintVisible = _hint.gameObject.activeSelf;
            float routesHeight = _routes != null && _routes.gameObject.activeSelf ?
                _metrics.touchSize + _metrics.gap : 0;
            float contextHeight = (contextVisible ? LayoutUtility.GetPreferredHeight(_context) : 0) + routesHeight;
            if (_size == safe.rect.size && Mathf.Approximately(_buttonWidth, buttonWidth) &&
                _contextVisible == contextVisible && _hintVisible == hintVisible &&
                Mathf.Approximately(contextHeight, _contextHeight)) return;
            _contextHeight = contextHeight;
            _contextVisible = contextVisible;
            _hintVisible = hintVisible;
            _size = safe.rect.size; _buttonWidth = buttonWidth; Revision++;
            float width = Mathf.Max(_metrics.cardWidth, safe.rect.width - 2 * _metrics.margin);
            bool sameRow = _capacity * _metrics.cardWidth + (_capacity - 1) * _metrics.gap +
                buttonWidth + _metrics.gap <= width;
            float handWidth = sameRow ? width - buttonWidth - _metrics.gap : width;
            int columns = Mathf.Max(1, Mathf.Min(_capacity,
                Mathf.FloorToInt((handWidth + _metrics.gap) / (_metrics.cardWidth + _metrics.gap))));
            int rows = Mathf.CeilToInt(_capacity / (float)columns);
            float cardWidth = Mathf.Min(_metrics.cardWidth * 1.25f,
                (handWidth - (columns - 1) * _metrics.gap) / columns);
            _grid.constraintCount = columns;
            _grid.cellSize = new Vector2(cardWidth, _metrics.cardHeight);
            _grid.spacing = Vector2.one * _metrics.gap;
            float handHeight = rows * _metrics.cardHeight + (rows - 1) * _metrics.gap;
            float bottom = _metrics.margin;
            Set(_submit, width - buttonWidth + _metrics.margin, bottom, buttonWidth, _metrics.actionHeight);
            if (!sameRow) bottom += _metrics.actionHeight + _metrics.gap;
            Set(_hand, _metrics.margin, bottom, handWidth, handHeight);
            if (_empty != null) Set(_empty, _metrics.margin, bottom, handWidth, handHeight);
            bottom += handHeight + _metrics.gap;
            ContextCenter = bottom + (contextHeight - routesHeight) / 2;
            if (_routes != null) Set(_routes, _metrics.margin, bottom + contextHeight - routesHeight,
                width, _metrics.touchSize);
            if (contextVisible) bottom += contextHeight + _metrics.gap;
            Set(_progress, _metrics.margin, bottom, width, _metrics.progressHeight);
            bottom += _metrics.progressHeight + _metrics.innerGap;
            Set(_hint, _metrics.margin, bottom, width, _metrics.captionHeight);
            if (hintVisible) bottom += _metrics.captionHeight + _metrics.gap;
            _sceneArea.offsetMin = new Vector2(_sceneArea.offsetMin.x, bottom);
        }
        private static void Set(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
