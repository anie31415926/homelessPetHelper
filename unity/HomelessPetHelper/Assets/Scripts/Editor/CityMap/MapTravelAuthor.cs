using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.CityMap.Travel;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>One-time isolated scene addition. Does not rebuild or save any existing panel prefab.</summary>
    public static class MapTravelAuthor
    {
        private const string WidgetPath = "Assets/Prefabs/Widgets/Widget_MapTravel.prefab";
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated authoring snapshot.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            if (scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<MapTravelView>(true) != null))
                throw new InvalidOperationException("Travel already authored; edit saved bindings instead.");
            var widget = new GameObject("Widget_MapTravel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MapTravelView));
            var canvas = widget.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = widget.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0;
            var cover = new GameObject("TravelCover", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(CanvasGroup));
            cover.transform.SetParent(widget.transform, false);
            var rect = (RectTransform)cover.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            cover.GetComponent<Image>().color = Color.black;
            var group = cover.GetComponent<CanvasGroup>();
            group.alpha = 0; group.interactable = group.blocksRaycasts = false;
            var travel = widget.GetComponent<MapTravelView>();
            Bind(travel, "_cover", group);
            PrefabUtility.SaveAsPrefabAsset(widget, WidgetPath);
            UnityEngine.Object.DestroyImmediate(widget);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WidgetPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            travel = instance.GetComponent<MapTravelView>();
            var root = scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<MainGameRoot>(true)).Single();
            Bind(root, "_travel", travel);
            var roots = new SerializedObject(travel);
            var inputs = roots.FindProperty("_inputRoots");
            string[] fields = { "_map", "_approach", "_siege", "_station" };
            inputs.arraySize = fields.Length;
            var bindings = new SerializedObject(root);
            for (int i = 0; i < fields.Length; i++)
            {
                var panel = (Component)bindings.FindProperty(fields[i]).objectReferenceValue;
                var input = panel.GetComponent<CanvasGroup>();
                if (input == null) input = panel.gameObject.AddComponent<CanvasGroup>();
                inputs.GetArrayElementAtIndex(i).objectReferenceValue = input;
            }
            roots.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
