using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Reports a readable full-width illustration height to its containing scroll layout.</summary>
    public sealed class PreferredAspectHeight : MonoBehaviour, ILayoutElement
    {
        [SerializeField, Min(.01f)] private float _aspect = .75f;
        public float minWidth => 0;
        public float preferredWidth => 0;
        public float flexibleWidth => 1;
        public float minHeight => preferredHeight;
        public float preferredHeight => ((RectTransform)transform).rect.width / _aspect;
        public float flexibleHeight => 0;
        public int layoutPriority => 2;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
    }
}
