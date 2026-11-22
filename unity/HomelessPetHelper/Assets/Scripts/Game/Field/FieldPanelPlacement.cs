using System;
using UnityEngine;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Prefab-authored placements for the legacy and scene presentations.</summary>
    public sealed class FieldPanelPlacement : MonoBehaviour
    {
        [Serializable]
        private sealed class Placement
        {
            public RectTransform Target;
            public Vector2 LegacyMin, LegacyMax, LegacyOffsetMin, LegacyOffsetMax;
            public Vector2 SceneMin, SceneMax;
        }
        [SerializeField] private Placement[] _placements;

        public void Apply(bool scene)
        {
            foreach (var item in _placements)
            {
                item.Target.anchorMin = scene ? item.SceneMin : item.LegacyMin;
                item.Target.anchorMax = scene ? item.SceneMax : item.LegacyMax;
                item.Target.offsetMin = scene ? Vector2.zero : item.LegacyOffsetMin;
                item.Target.offsetMax = scene ? Vector2.zero : item.LegacyOffsetMax;
            }
        }
    }
}
