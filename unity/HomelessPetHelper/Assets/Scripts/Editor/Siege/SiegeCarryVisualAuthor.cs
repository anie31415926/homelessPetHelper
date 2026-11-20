using System;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Siege
{
    public static class SiegeCarryVisualAuthor
    {
        public static void Apply()
        {
            if (!Application.isBatchMode || !Application.dataPath.Replace('\\', '/').Contains("/test-results/unity/"))
                throw new InvalidOperationException("Carry authoring requires an isolated batch project.");
            const string path = "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab";
            var stage = PrefabStageUtility.OpenPrefab(path);
            try
            {
                var field = stage.prefabContentsRoot.GetComponentInChildren<SiegeBattlefield>(true);
                var binding = new SerializedObject(field);
                var animal = (Image)binding.FindProperty("_cat").objectReferenceValue;
                var overlay = (SiegeCarryOverlay)binding.FindProperty("_carryOverlay").objectReferenceValue;
                if (overlay == null)
                {
                    var node = new GameObject("CarriedProp", typeof(RectTransform), typeof(CanvasRenderer),
                        typeof(SiegeCarryOverlay));
                    var rect = (RectTransform)node.transform;
                    rect.SetParent(animal.transform, false);
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
                    overlay = node.GetComponent<SiegeCarryOverlay>();
                    overlay.raycastTarget = false;
                    binding.FindProperty("_carryOverlay").objectReferenceValue = overlay;
                    binding.ApplyModifiedPropertiesWithoutUndo();
                }
                if (overlay.transform.parent != animal.transform)
                    throw new InvalidOperationException("Carry overlay must render in front of the animal image.");
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path);
                Debug.Log("Saved the carry overlay in Prefab Mode; existing sprite bindings preserved.");
            }
            finally { StageUtility.GoToMainStage(); }
        }
    }
}
