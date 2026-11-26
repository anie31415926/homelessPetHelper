using System;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationHabitat.StationUiSkin;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.StationHabitat
{
    public static class StationImmersiveUiAuthor
    {
        private const string PanelPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        private const string ToastPath = "Assets/Prefabs/Widgets/Widget_Toast.prefab";

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated station-ui.ps1.");
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.GetComponent<StationPanelView>();
                var chrome = root.GetComponent<StationSceneChrome>();
                var skin = new StationUiSkin { Scope = root.GetComponent<MotionScope>() };
                FullRoom(panel, root);
                CompactChrome(panel, chrome, skin);
                InstallToast(panel, chrome, skin);
                var detail = (RectTransform)Read<GameObject>(panel, "_detail").transform;
                detail.anchorMin = detail.anchorMax = detail.pivot = Vector2.one * .5f;
                detail.anchoredPosition = Vector2.zero;
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void FullRoom(StationPanelView panel, GameObject root)
        {
            var overview = Read<GameObject>(panel, "_overview");
            overview.transform.SetParent(root.transform, false);
            overview.transform.SetAsFirstSibling(); Stretch((RectTransform)overview.transform);
            var scene = Read<StationSceneView>(panel, "_scene");
            Stretch((RectTransform)scene.transform);
            var room = scene.Room;
            room.Content.pivot = new Vector2(.5f, 0);
            var fit = room.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            // Keep wandering on the visible foreground after the room's edges are cropped on tall screens.
            var fields = new SerializedObject(room);
            fields.FindProperty("_min").vector2Value = new Vector2(.22f, .12f);
            fields.FindProperty("_max").vector2Value = new Vector2(.78f, .54f);
            fields.ApplyModifiedPropertiesWithoutUndo();
            room.CreateArea();
        }

        private static void CompactChrome(StationPanelView panel, StationSceneChrome chrome, StationUiSkin skin)
        {
            var menu = Read<GameObject>(chrome, "_menu");
            var close = Read<Button>(chrome, "_menuClose");
            var items = (RectTransform)close.transform.parent.parent;
            var heading = close.transform.parent;
            foreach (var text in new[] { Read<Text>(panel, "_clock"), Read<Text>(panel, "_capacity") })
            {
                text.transform.SetParent(items, false);
                text.transform.SetSiblingIndex(heading.GetSiblingIndex() + 1);
                Element(text.transform, skin.Metrics.captionHeight).preferredHeight = -1;
                skin.Text(text, skin.Metrics.bodySize);
            }
            var button = Read<Button>(chrome, "_menuButton");
            var header = panel.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "StationTitle").transform.parent;
            var safe = header.parent;
            button.transform.SetParent(safe, false);
            header.gameObject.SetActive(false);
            Corner((RectTransform)button.transform, Vector2.one,
                new Vector2(-skin.Metrics.margin, -skin.Metrics.margin),
                Vector2.one * skin.Metrics.touchSize);
            var visual = Read<RectTransform>(button.GetComponent<UiMotionFeedback>(), "_visual");
            visual.offsetMin = Vector2.one * skin.Metrics.gap;
            visual.offsetMax = -Vector2.one * skin.Metrics.gap;
            var actions = Read<CanvasGroup>(panel, "_actions");
            Stretch((RectTransform)actions.transform);
            var footer = Read<GameObject>(chrome, "_footer");
            Stretch((RectTransform)footer.transform);
            var outing = Read<Button>(panel, "_restart");
            Corner((RectTransform)outing.transform, new Vector2(1, 0),
                new Vector2(-skin.Metrics.margin, skin.Metrics.margin), new Vector2(312, skin.Metrics.touchSize));
            foreach (var image in footer.GetComponents<Image>()) image.color = Color.clear;
            items.sizeDelta = new Vector2(720, items.sizeDelta.y);
            var metrics = new SerializedObject(menu.GetComponentInChildren<BoundedPopup>(true));
            metrics.FindProperty("_bounds").objectReferenceValue = items.parent;
            metrics.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallToast(StationPanelView panel, StationSceneChrome chrome, StationUiSkin skin)
        {
            Read<Text>(panel, "_feedback").transform.parent.gameObject.SetActive(false);
            var detail = Read<GameObject>(panel, "_detail");
            var safe = (RectTransform)detail.transform.parent;
            var font = Read<Text>(panel, "_feedback").font;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ToastPath) == null)
            {
                var toast = Rect("Widget_Toast", null);
                try
                {
                    toast.anchorMin = toast.anchorMax = new Vector2(.5f, .70f); toast.pivot = new Vector2(.5f, .5f);
                    toast.sizeDelta = new Vector2(600, 120);
                    var group = toast.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0; group.blocksRaycasts = group.interactable = false;
                    skin.Surface(toast.gameObject.AddComponent<Image>(), "Paper");
                    var label = Rect("ToastLine", toast).gameObject.AddComponent<Text>();
                    label.font = font; skin.Text(label, skin.Metrics.bodySize, TextAnchor.MiddleCenter);
                    label.supportRichText = false; label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.rectTransform.offsetMin = Vector2.one * skin.Metrics.gap;
                    label.rectTransform.offsetMax = -label.rectTransform.offsetMin;
                    var view = toast.gameObject.AddComponent<ToastView>();
                    Bind(view, "_group", group); Bind(view, "_label", label); Bind(view, "_metrics", skin.Metrics);
                    PrefabUtility.SaveAsPrefabAsset(toast.gameObject, ToastPath);
                }
                finally { Object.DestroyImmediate(toast.gameObject); }
            }
            var existing = Read<ToastView>(chrome, "_notice");
            if (existing != null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(ToastPath), safe);
            var notice = instance.GetComponent<ToastView>();
            Bind(notice, "_bounds", safe); Bind(chrome, "_notice", notice);
        }

        private static void Corner(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = offset; rect.sizeDelta = size;
        }
    }
}
