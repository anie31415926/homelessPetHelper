using System;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Incremental Prefab Mode upgrade; leaves existing artwork and point bindings intact.</summary>
    public static class CityMapPanUpgrade
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated authoring snapshot.");
            UpgradePrefab();
            RemoveLegacyOverrides("Assets/Scenes/Main.unity");
            RemoveLegacyOverrides(CityMapAssets.ScenePath);
        }

        private static void UpgradePrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CityMapAssets.PanelPath);
            try
            {
                var existing = root.GetComponentInChildren<CityMapScrollRect>(true);
                if (existing != null)
                {
                    existing.enabled = true;
                    PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
                    return;
                }
                var view = root.GetComponent<CityMapView>();
                var destination = (RectTransform)Ref<Text>(view, "_heading").transform.parent;
                var oldContent = (RectTransform)destination.parent;
                var body = (RectTransform)oldContent.parent;
                var map = (RectTransform)oldContent.GetChild(0);
                var art = (RectTransform)map.GetChild(0);
                UnityEngine.Object.DestroyImmediate(body.GetComponent<ScrollRect>());
                UnityEngine.Object.DestroyImmediate(body.GetComponent<RectMask2D>());
                UnityEngine.Object.DestroyImmediate(oldContent.GetComponent<ContentSizeFitter>());
                UnityEngine.Object.DestroyImmediate(oldContent.GetComponent<VerticalLayoutGroup>());
                var column = body.gameObject.AddComponent<VerticalLayoutGroup>();
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = column.childForceExpandHeight = true;
                map.SetParent(body, false);
                map.SetAsFirstSibling();
                ConfigureMap(view, map, art);
                ConfigureDetails(view, oldContent, destination);
                PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureMap(CityMapView view, RectTransform viewport, RectTransform art)
        {
            UnityEngine.Object.DestroyImmediate(viewport.GetComponent<PreferredAspectHeight>());
            UnityEngine.Object.DestroyImmediate(art.GetComponent<AspectRatioFitter>());
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(.5f, .5f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(1680, 2240);
            var size = viewport.GetComponent<LayoutElement>();
            size.minHeight = 432; size.preferredHeight = 0; size.flexibleHeight = 2;
            Surface(viewport, Color.clear, true);
            var pan = viewport.gameObject.AddComponent<CityMapScrollRect>();
            pan.viewport = viewport; pan.content = art;
            pan.horizontal = pan.vertical = true;
            pan.movementType = ScrollRect.MovementType.Clamped;
            pan.decelerationRate = .08f; pan.scrollSensitivity = 48;
            Bind(pan, "_mapImage", art.GetComponent<Image>());
            Bind(view, "_mapScroll", pan);
        }

        private static void RemoveLegacyOverrides(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var map = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<CityMapScrollRect>(true)).Single();
            var rect = new SerializedObject(map.content);
            // The old AspectRatioFitter wrote these overrides when the preview scenes were saved.
            foreach (string property in new[] { "m_AnchorMax.x", "m_AnchorMax.y", "m_SizeDelta.x" })
                PrefabUtility.RevertPropertyOverride(rect.FindProperty(property), InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureDetails(CityMapView view, RectTransform viewport, RectTransform content)
        {
            viewport.name = "DestinationViewport";
            viewport.gameObject.AddComponent<RectMask2D>();
            Surface(viewport, Color.white, true);
            var size = viewport.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 216; size.preferredHeight = 0; size.flexibleHeight = 1;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Bind(view, "_detailsScroll", scroll);
        }
    }
}
