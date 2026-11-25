using System;
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
        private const string CoverPath = "Assets/Art/Loading/T_WeChat_Cover.jpg";

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
                if (root.transform.Find("TitleCover") != null) return;
                var view = root.GetComponent<LoadingView>();
                var safe = root.transform.Find("SafeArea");
                var artwork = safe.Find("ArtworkBounds");
                var progress = safe.Find("Progress");
                var cover = CreateCover(root.transform, safe.GetSiblingIndex());
                var enter = CreateEntryButton(safe, out Text label);
                root.AddComponent<GraphicRaycaster>();
                MainSceneAuthor.Bind(view, "_canvasGroup", root.AddComponent<CanvasGroup>());
                MainSceneAuthor.Bind(view, "_cover", cover.gameObject);
                MainSceneAuthor.Bind(view, "_previewArtwork", artwork.gameObject);
                MainSceneAuthor.Bind(view, "_progress", progress.gameObject);
                MainSceneAuthor.Bind(view, "_enter", enter);
                MainSceneAuthor.Bind(view, "_enterLabel", label);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Image CreateCover(Transform parent, int siblingIndex)
        {
            var cover = new GameObject("TitleCover", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            cover.transform.SetParent(parent, false);
            cover.transform.SetSiblingIndex(siblingIndex);
            var image = cover.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoverPath);
            if (image.sprite == null) throw new InvalidOperationException("Missing startup cover sprite.");
            image.raycastTarget = false;
            var fit = cover.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            cover.SetActive(false);
            return image;
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
