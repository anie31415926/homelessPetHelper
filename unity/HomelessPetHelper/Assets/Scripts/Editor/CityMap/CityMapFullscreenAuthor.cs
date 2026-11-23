using System;
using System.Linq;
using HomelessPetHelper.Editor.StationHabitat;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationHabitat.StationUiSkin;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Incremental, isolated Prefab Mode edit; never recreates terrain or landmark bindings.</summary>
    public static class CityMapFullscreenAuthor
    {
        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            var root = PrefabUtility.LoadPrefabContents(CityMapAssets.PanelPath);
            try
            {
                var view = root.GetComponent<CityMapView>();
                if (Read<GameObject>(view, "_detailsRoot") != null)
                    throw new InvalidOperationException("Fullscreen map is saved; edit the Prefab directly.");
                var skin = new StationUiSkin { Scope = root.GetComponent<MotionScope>() };
                var safe = (RectTransform)root.GetComponentInChildren<SafeAreaView>(true).transform;
                Remove<VerticalLayoutGroup>(safe);
                var map = Read<CityMapScrollRect>(view, "_mapScroll");
                map.transform.SetParent(root.transform, false);
                map.transform.SetAsFirstSibling();
                Remove<LayoutElement>(map);
                Stretch((RectTransform)map.transform);
                root.GetComponent<Image>().color = Color.clear;
                var back = Read<Button>(view, "_cancel");
                back.transform.SetParent(safe, false);
                MakeBackIcon(back, skin);
                var backRect = (RectTransform)back.transform;
                backRect.anchorMin = backRect.anchorMax = new Vector2(0, 1);
                backRect.pivot = new Vector2(0, 1);
                backRect.anchoredPosition = new Vector2(skin.Metrics.margin, -skin.Metrics.margin);
                backRect.sizeDelta = Vector2.one * skin.Metrics.touchSize;
                CreateDetails(view, safe, back, skin);
                CreateBackdrop(view, root.transform);
                CreateToast(view, safe);
                RefineLabels(view, skin);
                foreach (var child in safe.Cast<Transform>().ToArray())
                    if (child != back.transform && child != Read<GameObject>(view, "_detailsRoot").transform &&
                        child != Read<ToastView>(view, "_toast").transform)
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void CreateDetails(CityMapView view, RectTransform safe, Button back, StationUiSkin skin)
        {
            var page = Rect("PlacePopup", safe);
            page.anchorMin = page.anchorMax = page.pivot = Vector2.one * .5f;
            page.sizeDelta = new Vector2(skin.Metrics.guideMaxWidth, skin.Metrics.guideMaxWidth);
            skin.Paper(page);
            skin.Column(page, (int)skin.Metrics.margin);
            var header = Rect("PlaceHeader", page);
            Element(header, skin.Metrics.touchSize);
            var row = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = skin.Metrics.gap;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            var heading = Read<Text>(view, "_heading");
            heading.transform.SetParent(header, false);
            Remove<LayoutElement>(heading);
            heading.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            skin.Text(heading, skin.Metrics.titleSize);
            var close = UnityEngine.Object.Instantiate(back, header);
            close.name = "ClosePlace";
            Element(close.transform, skin.Metrics.touchSize).preferredWidth = skin.Metrics.touchSize;
            Bind(view, "_closeDetails", close);
            var scroll = Read<ScrollRect>(view, "_detailsScroll");
            scroll.transform.SetParent(page, false);
            skin.Column(scroll.content, 0);
            var size = Element(scroll.transform, skin.Metrics.touchSize);
            foreach (string field in new[] { "_details", "_status", "_departure", "_feedback" })
            {
                var text = Read<Text>(view, field);
                text.transform.SetParent(scroll.content, false);
                text.transform.SetAsLastSibling();
                Remove<LayoutElement>(text);
                skin.Text(text, skin.Metrics.bodySize);
            }
            var primary = Read<Button>(view, "_primary");
            var actions = (RectTransform)primary.transform.parent;
            actions.SetParent(page, false);
            var adaptive = actions.GetComponent<AdaptiveButtonRow>();
            adaptive.padding = new RectOffset((int)skin.Metrics.innerGap, (int)skin.Metrics.innerGap,
                (int)skin.Metrics.innerGap, (int)skin.Metrics.innerGap);
            Remove<LayoutElement>(actions);
            var layout = page.gameObject.AddComponent<PopupContentLayout>();
            Bind(layout, "_metrics", skin.Metrics); Bind(layout, "_page", page);
            Bind(layout, "_viewport", scroll.viewport); Bind(layout, "_content", scroll.content);
            Bind(layout, "_bodySize", size); Bind(layout, "_scroll", scroll);
            Bind(view, "_detailsRoot", page.gameObject);
            page.gameObject.SetActive(false);
        }

        private static void CreateBackdrop(CityMapView view, Transform root)
        {
            var rect = Rect("MapModalBackdrop", root);
            rect.SetSiblingIndex(1);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color32(0, 0, 0, 88);
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            rect.gameObject.AddComponent<ButtonAudio>().Silent = true;
            Bind(view, "_backdrop", button);
            rect.gameObject.SetActive(false);
        }

        private static void CreateToast(CityMapView view, RectTransform safe)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Widgets/Widget_Toast.prefab");
            var node = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safe);
            var toast = node.GetComponent<ToastView>();
            Bind(toast, "_bounds", safe);
            Bind(view, "_toast", toast);
        }

        private static void MakeBackIcon(Button button, StationUiSkin skin)
        {
            Remove<LayoutElement>(button);
            foreach (var label in button.GetComponentsInChildren<Text>(true))
                UnityEngine.Object.DestroyImmediate(label.gameObject);
            var visual = (RectTransform)button.targetGraphic.transform;
            var icon = Rect("BackIcon", visual).gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/T_UI_LevelControls.png")
                .OfType<Sprite>().Single(sprite => sprite.name == "Play");
            icon.color = Color.white; icon.raycastTarget = false; icon.preserveAspect = true;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = Vector2.one * .5f;
            icon.rectTransform.sizeDelta = Vector2.one * skin.Metrics.titleSize;
            icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        }

        private static void RefineLabels(CityMapView view, StationUiSkin skin)
        {
            var serialized = new SerializedObject(view);
            var bindings = serialized.FindProperty("_pointBindings");
            for (int index = 0; index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                var plate = (Image)binding.FindPropertyRelative("_highlight").objectReferenceValue;
                plate.color = Color.clear;
                plate.rectTransform.sizeDelta = new Vector2(skin.Metrics.cardWidth + skin.Metrics.margin,
                    skin.Metrics.captionHeight);
                var label = (Text)binding.FindPropertyRelative("_label").objectReferenceValue;
                CuteLabel(label, skin);
                var state = (Text)binding.FindPropertyRelative("_stateLabel").objectReferenceValue;
                state.gameObject.SetActive(false);
            }
            var map = Read<CityMapScrollRect>(view, "_mapScroll");
            foreach (var label in map.content.GetComponentsInChildren<Text>(true)
                .Where(text => text.name != "State"))
            {
                CuteLabel(label, skin);
                var plate = label.transform.parent.GetComponent<Image>();
                if (plate != null) plate.color = Color.clear;
            }
        }

        public static void FinishLabels()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            var root = PrefabUtility.LoadPrefabContents(CityMapAssets.PanelPath);
            try
            {
                RefineLabels(root.GetComponent<CityMapView>(), new StationUiSkin());
                var backdrop = Read<Button>(root.GetComponent<CityMapView>(), "_backdrop");
                if (backdrop.GetComponent<ButtonAudio>() == null)
                    backdrop.gameObject.AddComponent<ButtonAudio>().Silent = true;
                PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CuteLabel(Text label, StationUiSkin skin)
        {
            Stretch(label.rectTransform);
            skin.Text(label, skin.Metrics.bodySize, TextAnchor.MiddleCenter);
            label.color = new Color32(255, 253, 237, 255);
            label.fontStyle = FontStyle.Bold;
            var outline = label.GetComponent<Outline>();
            if (outline == null) outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(58, 87, 55, 255);
            outline.effectDistance = Vector2.one * (skin.Metrics.innerGap / 4);
            outline.useGraphicAlpha = true;
        }
    }
}
