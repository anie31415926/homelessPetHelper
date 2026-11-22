using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>A guide may only point at something the player can actually press. A hole opened over a
    /// hidden or disabled target still swallows every other click, which strands the level with no usable
    /// input, so such a target is refused and the guide reports itself unavailable instead.</summary>
    public static class GuideTargetRules
    {
        public static bool IsClickable(Selectable target) => target != null &&
            target.gameObject.activeInHierarchy && target.IsInteractable();
    }
}
