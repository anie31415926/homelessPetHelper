using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Editor.StationHabitat;
using HomelessPetHelper.Game.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Loading
{
    public static class LoadingStartupAuthor
    {
        private const string BackgroundPath = "Assets/Art/Loading/T_Loading_Background.png";
        private const string ElementsPath = "Assets/Art/Loading/T_Loading_Elements.png";
        private const string TitleText = "毛茸茸救助站";

        // 构图比例照搬 tools/wechat/build-cover.mjs（540×960 基准，×0.82 换算后归一化），
        // y 从顶部起算；招牌复用图集 LoadingSign 切片并保持其原生纵横比。
        private static readonly (string name, float cx, float cy, float hw, float hh)[] Composition =
        {
            ("HeartUpper", 0.5f, 0.2354f, 0.04252f, 0.02392f),
            ("Sign", 0.5f, 0.3306f, 0.3265f, 0.03108f),
            ("CoverTitle", 0.5f, 0.3306f, 0.3f, 0.026f),
            ("House", 0.5f, 0.54381f, 0.22929f, 0.12049f),
            ("StarLeft", 0.25856f, 0.40841f, 0.03265f, 0.02055f),
            ("StarRight", 0.75359f, 0.43241f, 0.02961f, 0.01832f),
            ("OrbitGinger", 0.29803f, 0.63836f, 0.10174f, 0.04486f),
            ("DoorRight", 0.705f, 0.63072f, 0.09415f, 0.05326f),
        };

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LoadingStartup.");
            ApplyCoverToExistingPrefab();
            var scene = EditorSceneManager.OpenScene(LoadingSceneRoot.ScenePath);
            var host = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<LoadingSceneRoot>(true)).Single();
            var data = new SerializedObject(host);
            data.FindProperty("_timeoutSeconds").floatValue = 60f;
            data.FindProperty("_previewOnly").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (scene.GetRootGameObjects().All(root => root.GetComponentInChildren<EventSystem>(true) == null))
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Configured existing Loading startup: show cover, prepare Main, and wait for entry.");
        }

        private static void ApplyCoverToExistingPrefab()
        {
            string path = LoadingSceneAuthor.PrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<LoadingView>();
                var safe = root.transform.Find("SafeArea");
                var artwork = safe.Find("ArtworkBounds");
                var progress = safe.Find("Progress");
                var previous = root.transform.Find("TitleCover");
                if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var cover = CreateCover(root.transform, safe.GetSiblingIndex());
                var enterTransform = safe.Find("EnterStation");
                Button enter;
                Text label;
                if (enterTransform == null) enter = CreateEntryButton(safe, out label);
                else
                {
                    enter = enterTransform.GetComponent<Button>();
                    label = enter.GetComponentInChildren<Text>(true);
                }
                root.AddComponent<GraphicRaycaster>();
                var group = root.GetComponent<CanvasGroup>();
                if (group == null) group = root.AddComponent<CanvasGroup>();
                MainSceneAuthor.Bind(view, "_canvasGroup", group);
                MainSceneAuthor.Bind(view, "_cover", cover);
                MainSceneAuthor.Bind(view, "_previewArtwork", artwork.gameObject);
                MainSceneAuthor.Bind(view, "_progress", progress.gameObject);
                MainSceneAuthor.Bind(view, "_enter", enter);
                MainSceneAuthor.Bind(view, "_enterLabel", label);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject CreateCover(Transform parent, int siblingIndex)
        {
            var cover = new GameObject("TitleCover", typeof(RectTransform));
            cover.transform.SetParent(parent, false);
            cover.transform.SetSiblingIndex(siblingIndex);
            var rect = (RectTransform)cover.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var background = new GameObject("CoverBackground", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(cover.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            var image = background.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (image.sprite == null) throw new InvalidOperationException("Missing loading background sprite.");
            image.preserveAspect = false;

            var composition = new GameObject("CoverComposition", typeof(RectTransform), typeof(AspectRatioFitter));
            composition.transform.SetParent(cover.transform, false);
            var compositionRect = (RectTransform)composition.transform;
            compositionRect.anchorMin = compositionRect.anchorMax = new Vector2(.5f, .5f);
            compositionRect.offsetMin = compositionRect.offsetMax = Vector2.zero;
            var fit = composition.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 540f / 960f;

            var slices = AssetDatabase.LoadAllAssetsAtPath(ElementsPath).OfType<Sprite>().ToList();
            foreach (var entry in Composition)
            {
                var node = CreatePlacedNode(composition.transform, entry.name, entry.cx, entry.cy, entry.hw, entry.hh);
                var slice = node.GetComponent<Image>();
                slice.raycastTarget = false;
                slice.preserveAspect = true;
                if (entry.name == "CoverTitle")
                {
                    UnityEngine.Object.DestroyImmediate(slice);
                    var label = node.gameObject.AddComponent<Text>();
                    label.font = AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
                    if (label.font == null) throw new InvalidOperationException("Missing shared game font.");
                    label.text = TitleText;
                    label.color = new Color32(0x36, 0x58, 0x3D, 0xFF);
                    label.alignment = TextAnchor.MiddleCenter;
                    label.raycastTarget = false;
                    label.resizeTextMinSize = 24;
                    label.resizeTextMaxSize = 96;
                    label.resizeTextForBestFit = true;
                    continue;
                }
                slice.sprite = slices.FirstOrDefault(item => item.name == (entry.name == "Sign" ? "LoadingSign" : entry.name));
                if (slice.sprite == null)
                    throw new InvalidOperationException("Missing loading slice sprite: " + entry.name);
            }
            cover.SetActive(false);
            return cover;
        }

        private static RectTransform CreatePlacedNode(Transform parent, string name, float cx, float cy,
            float halfWidth, float halfHeight)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(parent, false);
            var rect = (RectTransform)child.transform;
            rect.anchorMin = new Vector2(cx - halfWidth, 1f - (cy + halfHeight));
            rect.anchorMax = new Vector2(cx + halfWidth, 1f - (cy - halfHeight));
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return rect;
        }

        private static Button CreateEntryButton(Transform parent, out Text label)
        {
            var skin = new StationUiSkin();
            var control = new GameObject("EnterStation", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)control.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
            rect.sizeDelta = new Vector2(520f, skin.Metrics.touchSize);
            rect.anchoredPosition = new Vector2(0f, 176f);
            var image = control.GetComponent<Image>();
            skin.Surface(image, "Action", true);
            var button = control.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = skin.Metrics.buttonHover;
            colors.pressedColor = skin.Metrics.buttonPressed;
            button.colors = colors;

            label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMax = Vector2.zero;
            label.font = AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
            skin.Text(label, skin.Metrics.bodySize, TextAnchor.MiddleCenter);
            control.SetActive(false);
            return button;
        }
    }
}
