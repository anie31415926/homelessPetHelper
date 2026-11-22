using System.Collections.Generic;
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
        private int _capacity = 1;
        private Vector2 _size = Vector2.negativeInfinity;
        private float _buttonWidth = -1;
        private bool _contextVisible;
        private bool _hintVisible;
        private float _contextWidth;
        private readonly List<Text> _contextLabels = new List<Text>(4);
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
            float contextWidth = contextVisible ? PreferredContextWidth() : 0;
            if (_size == safe.rect.size && Mathf.Approximately(_buttonWidth, buttonWidth) &&
                _contextVisible == contextVisible && _hintVisible == hintVisible &&
                Mathf.Approximately(contextWidth, _contextWidth)) return;
            _contextWidth = contextWidth;
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
            _grid.cellSize = new Vector2(cardWidth, _metrics.fieldCardHeight);
            _grid.spacing = Vector2.one * _metrics.gap;
            float handHeight = rows * _metrics.fieldCardHeight + (rows - 1) * _metrics.gap;
            float bottom = _metrics.margin;
            Set(_submit, width - buttonWidth + _metrics.margin, bottom, buttonWidth, _metrics.actionHeight);
            if (!sameRow) bottom += _metrics.actionHeight + _metrics.gap;
            Set(_hand, _metrics.margin, bottom, handWidth, handHeight);
            if (_empty != null) Set(_empty, _metrics.margin, bottom, handWidth, handHeight);
            bottom += handHeight + _metrics.gap;
            // One stable utility strip replaces stacked context, hint and charge rows.
            float hintWidth = contextVisible ? width - contextWidth - _metrics.gap : width;
            Set(_hint, _metrics.margin, bottom, Mathf.Max(_metrics.touchSize, hintWidth), _metrics.touchSize);
            if (_context != null)
            {
                Set(_context, _metrics.margin + width - contextWidth, bottom,
                    Mathf.Max(_metrics.touchSize, contextWidth), _metrics.touchSize);
            }
            bottom += _metrics.touchSize + _metrics.gap;
            _sceneArea.offsetMin = new Vector2(_sceneArea.offsetMin.x, bottom);
            _sceneArea.offsetMax = new Vector2(_sceneArea.offsetMax.x, -_metrics.fieldHeaderHeight);
        }

        private float PreferredContextWidth()
        {
            float width = 0;
            int count = 0;
            for (int index = 0; index < _context.childCount; index++)
            {
                var child = (RectTransform)_context.GetChild(index);
                if (!child.gameObject.activeSelf) continue;
                float itemWidth = _metrics.touchSize;
                child.GetComponentsInChildren(false, _contextLabels);
                foreach (var label in _contextLabels)
                    itemWidth = Mathf.Max(itemWidth, label.preferredWidth + _metrics.margin);
                var element = child.GetComponent<LayoutElement>();
                element.minWidth = element.preferredWidth = itemWidth;
                width += itemWidth; count++;
            }
            return width + Mathf.Max(0, count - 1) * _metrics.gap;
        }
        private static void Set(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
