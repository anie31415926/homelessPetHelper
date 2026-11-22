using UnityEngine;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Authored project design units, shared by saved layouts; not device dp or pt.</summary>
    [CreateAssetMenu(menuName = "Homeless Pet Helper/UI Layout Metrics")]
    public sealed class UiLayoutMetrics : ScriptableObject
    {
        [Min(1)] public float margin = 48;
        [Min(1)] public float gap = 24;
        [Min(1)] public float innerGap = 12;
        [Min(1)] public float touchSize = 144;
        [Min(1)] public float cardWidth = 240;
        [Min(1)] public float cardHeight = 384;
        [Min(1)] public float actionWidth = 192;
        [Min(1)] public float actionHeight = 168;
        [Min(1)] public float captionHeight = 96;
        [Min(1)] public float progressHeight = 48;
        [Min(1)] public float popupMaxWidth = 1080;
        [Range(.5f, .95f)] public float popupHeightRatio = .9f;
        public int titleSize = 66;
        public int bodySize = 48;
        public int labelSize = 42;
        public int auxiliarySize = 36;
        public Color ink = new Color32(42, 61, 48, 255);
    }
}
