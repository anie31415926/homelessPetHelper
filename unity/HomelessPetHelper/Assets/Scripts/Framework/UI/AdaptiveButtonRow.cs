using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Wraps authored controls at their readable preferred width instead of shrinking them.</summary>
    public sealed class AdaptiveButtonRow : LayoutGroup
    {
        [SerializeField] private UiLayoutMetrics _metrics;
        private int _columns = 1;
        private float _width, _height;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            _width = _metrics.touchSize;
            _height = _metrics.touchSize;
            foreach (var child in rectChildren)
            {
                float width = LayoutUtility.GetMinWidth(child);
                foreach (var label in child.GetComponentsInChildren<Text>(false))
                    width = Mathf.Max(width, label.preferredWidth + _metrics.margin);
                _width = Mathf.Max(_width, width);
                _height = Mathf.Max(_height, LayoutUtility.GetMinHeight(child));
            }
            SetLayoutInputForAxis(_width + padding.horizontal, _width + padding.horizontal, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float available = Mathf.Max(0, rectTransform.rect.width - padding.horizontal);
            _columns = Mathf.Max(1, Mathf.Min(rectChildren.Count,
                Mathf.FloorToInt((available + _metrics.gap) / (_width + _metrics.gap))));
            int rows = Mathf.CeilToInt(rectChildren.Count / (float)_columns);
            float height = padding.vertical + rows * _height + Mathf.Max(0, rows - 1) * _metrics.gap;
            SetLayoutInputForAxis(height, height, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            float available = Mathf.Max(0, rectTransform.rect.width - padding.horizontal);
            float width = (available - (_columns - 1) * _metrics.gap) / _columns;
            for (int index = 0; index < rectChildren.Count; index++)
                SetChildAlongAxis(rectChildren[index], 0, padding.left + index % _columns *
                    (width + _metrics.gap), width);
        }

        public override void SetLayoutVertical()
        {
            for (int index = 0; index < rectChildren.Count; index++)
                SetChildAlongAxis(rectChildren[index], 1, padding.top + index / _columns *
                    (_height + _metrics.gap), _height);
        }
    }
}
