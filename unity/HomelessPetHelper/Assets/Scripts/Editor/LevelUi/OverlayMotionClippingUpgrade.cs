using System;
using HomelessPetHelper.Framework.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Reserve animation space inside existing masked popup bodies; keep masks and hit roots.</summary>
    public static class OverlayMotionClippingUpgrade
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a disposable batch copy.");
            const string path = "Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab";
            var metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>("Assets/Settings/UI/LayoutMetrics.asset");
            int gutter = Mathf.CeilToInt(metrics.gap);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
                {
                    var layout = scroll.content.GetComponent<VerticalLayoutGroup>();
                    if (layout == null) throw new InvalidOperationException("Missing authored body layout.");
                    layout.padding = new RectOffset(gutter, gutter, gutter, gutter);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
    }
}
