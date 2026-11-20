using System;
using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Incremental Prefab Mode edit. Keeps all existing artwork, audio and point identities.</summary>
    public static class CityMapInfoUpgrade
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated MapInfoAssets authoring.");
            GameObject root = PrefabUtility.LoadPrefabContents(CityMapAssets.PanelPath);
            try
            {
                var view = new SerializedObject(root.GetComponent<CityMapView>());
                var heading = (Text)view.FindProperty("_heading").objectReferenceValue;
                heading.GetComponent<LayoutElement>().preferredHeight = 64;
                var details = (Text)view.FindProperty("_details").objectReferenceValue;
                var departure = (Text)view.FindProperty("_departure").objectReferenceValue;
                if (departure == null)
                {
                    departure = UnityEngine.Object.Instantiate(details, details.transform.parent);
                    departure.name = "Departure";
                    departure.transform.SetSiblingIndex(details.transform.GetSiblingIndex() + 1);
                    departure.text = string.Empty;
                    view.FindProperty("_departure").objectReferenceValue = departure;
                }
                departure.fontSize = 30;
                departure.GetComponent<LayoutElement>().preferredHeight = 100;
                SerializedProperty bindings = view.FindProperty("_pointBindings");
                for (int i = 0; i < bindings.arraySize; i++) UpgradePoint(bindings.GetArrayElementAtIndex(i));
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpgradePoint(SerializedProperty binding)
        {
            var plate = (Image)binding.FindPropertyRelative("_highlight").objectReferenceValue;
            var name = (Text)binding.FindPropertyRelative("_label").objectReferenceValue;
            var state = (Text)binding.FindPropertyRelative("_stateLabel").objectReferenceValue;
            if (state != null) return;
            plate.rectTransform.sizeDelta = new Vector2(plate.rectTransform.sizeDelta.x, 96);
            name.rectTransform.anchorMin = new Vector2(0, .46f);
            name.rectTransform.anchorMax = Vector2.one;
            name.rectTransform.offsetMin = new Vector2(6, 0);
            name.rectTransform.offsetMax = new Vector2(-6, -4);
            state = UnityEngine.Object.Instantiate(name, plate.transform);
            state.name = "State";
            state.text = string.Empty;
            state.fontSize = 24;
            state.rectTransform.anchorMin = Vector2.zero;
            state.rectTransform.anchorMax = new Vector2(1, .46f);
            state.rectTransform.offsetMin = new Vector2(6, 4);
            state.rectTransform.offsetMax = new Vector2(-6, 0);
            binding.FindPropertyRelative("_stateLabel").objectReferenceValue = state;
        }
    }
}
