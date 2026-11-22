using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Fills a scroll viewport while allowing the content's own preferred height to grow.</summary>
    public sealed class MinimumViewportHeight : MonoBehaviour, ILayoutElement
    {
        public float minWidth => -1;
        public float preferredWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => ((RectTransform)transform.parent).rect.height;
        public float preferredHeight => -1;
        public float flexibleHeight => -1;
        public int layoutPriority => 1;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
    }
}
