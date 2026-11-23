using System;
using System.Linq;
using HomelessPetHelper.Editor.StationHabitat;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationHabitat.StationUiSkin;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>One-off Prefab Mode refinement in an isolated editor; retains all landmark anchors and art.</summary>
    public static class CityMapUiRefinementAuthor
    {
        private const string ControlsPath = "Assets/Art/UI/T_UI_LevelControls.png";

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            var root = PrefabUtility.LoadPrefabContents(CityMapAssets.PanelPath);
            try
            {
                if (root.GetComponent<MotionScope>() != null)
                    throw new InvalidOperationException("Map refinement is already saved; edit in Prefab Mode.");
                var skin = new StationUiSkin();
                skin.Scope = root.AddComponent<MotionScope>();
                Bind(skin.Scope, "_library", AssetDatabase.LoadAssetAtPath<MotionLibrary>(
                    "Assets/Settings/UI/LevelMotion.asset"));
                var view = root.GetComponent<CityMapView>();
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                    skin.Text(text, skin.Metrics.bodySize);
                skin.Surface(root.GetComponent<Image>(), "Paper");
                RefineHeader(view, skin);
                RefineDetails(view, skin);
                RefineActions(view, skin);
                RefinePoints(view, skin);
                PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void RefineHeader(CityMapView view, StationUiSkin skin)
        {
            var metrics = skin.Metrics;
            var status = Read<Text>(view, "_status");
            var header = status.transform.parent;
            header.GetComponent<Image>().color = Color.clear;
            var column = skin.Column((RectTransform)header, (int)metrics.margin);
            column.padding.top = column.padding.bottom = (int)metrics.innerGap;
            column.spacing = metrics.innerGap;
            var safe = header.parent.GetComponent<VerticalLayoutGroup>();
            safe.padding = new RectOffset(0, 0, (int)metrics.gap, (int)metrics.gap);
            var pause = Read<Toggle>(view, "_pause");
            var row = pause.transform.parent.GetComponent<HorizontalLayoutGroup>();
            row.spacing = metrics.gap;
            var title = row.GetComponentsInChildren<Text>().Single(text => text.transform.parent == row.transform);
            skin.Text(title, metrics.titleSize);
            RefinePause(pause, skin);
        }

        private static void RefinePause(Toggle toggle, StationUiSkin skin)
        {
            var metrics = skin.Metrics;
            var visual = Rect("MotionVisual", toggle.transform);
            visual.offsetMin = Vector2.one * metrics.innerGap;
            visual.offsetMax = -visual.offsetMin;
            var surface = visual.gameObject.AddComponent<Image>();
            skin.Surface(surface, "Secondary");
            toggle.targetGraphic.color = Color.clear;
            toggle.targetGraphic = surface;
            var check = (Image)toggle.graphic;
            check.transform.SetParent(visual, false);
            check.sprite = Sprite("Paw"); check.preserveAspect = true; check.color = Color.white;
            check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(.18f, .5f);
            check.rectTransform.anchoredPosition = Vector2.zero;
            check.rectTransform.sizeDelta = Vector2.one * metrics.bodySize;
            var label = toggle.GetComponentInChildren<Text>();
            label.transform.SetParent(visual, false);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(metrics.bodySize + metrics.innerGap, metrics.innerGap);
            label.rectTransform.offsetMax = -Vector2.one * metrics.innerGap;
            skin.Text(label, metrics.bodySize, TextAnchor.MiddleCenter);
            var colors = toggle.colors;
            colors.normalColor = colors.disabledColor = Color.white;
            colors.highlightedColor = colors.selectedColor = metrics.buttonHover;
            colors.pressedColor = metrics.buttonPressed; toggle.colors = colors;
            var motion = toggle.gameObject.AddComponent<UiMotionFeedback>();
            Bind(motion, "_scope", skin.Scope); Bind(motion, "_control", toggle); Bind(motion, "_visual", visual);
            var size = Element(toggle.transform, metrics.touchSize);
            size.minWidth = size.preferredWidth = metrics.cardWidth;
            size.flexibleWidth = 0;
        }

        private static void RefineDetails(CityMapView view, StationUiSkin skin)
        {
            var metrics = skin.Metrics;
            var scroll = Read<ScrollRect>(view, "_detailsScroll");
            var surface = scroll.viewport.GetComponent<Image>();
            surface.sprite = null; surface.color = Color.clear; surface.raycastTarget = true;
            scroll.content.GetComponent<Image>().color = Color.clear;
            var column = skin.Column(scroll.content, (int)metrics.margin);
            column.padding.bottom = (int)metrics.innerGap;
            column.padding.right += (int)metrics.bodySize;
            column.spacing = metrics.innerGap;
            skin.Text(Read<Text>(view, "_heading"), metrics.titleSize);
            foreach (string field in new[] { "_details", "_departure", "_feedback" })
                Read<Text>(view, field).lineSpacing = 1.4f;
            var map = Read<CityMapScrollRect>(view, "_mapScroll");
            map.GetComponent<LayoutElement>().flexibleHeight = 3;
            var size = scroll.GetComponent<LayoutElement>();
            size.minHeight = metrics.touchSize; size.flexibleHeight = 1;
            AddTravelSummary(view, scroll.viewport.parent.parent, skin);
            AddOverflowIndicator(view, scroll.viewport, skin);
            // The body keeps map/detail scrolling independent. No animation is added under either mask.
        }

        private static void AddTravelSummary(CityMapView view, Transform safe, StationUiSkin skin)
        {
            var summary = Rect("TravelSummary", safe);
            summary.SetSiblingIndex(Read<Button>(view, "_primary").transform.parent.GetSiblingIndex());
            var layout = skin.Column(summary, (int)skin.Metrics.margin);
            layout.padding.top = layout.padding.bottom = (int)skin.Metrics.innerGap;
            layout.spacing = skin.Metrics.innerGap;
            foreach (string field in new[] { "_departure", "_feedback" })
                Read<Text>(view, field).transform.SetParent(summary, false);
            Element(summary, 0).preferredHeight = -1;
        }

        private static void AddOverflowIndicator(CityMapView view, RectTransform viewport, StationUiSkin skin)
        {
            var indicator = Rect("DetailsOverflow", viewport);
            indicator.anchorMin = indicator.anchorMax = new Vector2(1, .5f);
            indicator.anchoredPosition = new Vector2(-skin.Metrics.margin, 0);
            indicator.sizeDelta = new Vector2(skin.Metrics.labelSize, skin.Metrics.touchSize);
            for (int direction = -1; direction <= 1; direction += 2)
            {
                var arrow = Rect(direction < 0 ? "ScrollUp" : "ScrollDown", indicator);
                arrow.anchorMin = arrow.anchorMax = Vector2.one * .5f;
                arrow.anchoredPosition = new Vector2(0, direction * skin.Metrics.gap);
                arrow.sizeDelta = Vector2.one * skin.Metrics.labelSize;
                arrow.localRotation = Quaternion.Euler(0, 0, direction * 90);
                var image = arrow.gameObject.AddComponent<Image>();
                image.sprite = Sprite("Play"); image.preserveAspect = true; image.raycastTarget = false;
            }
            Bind(view, "_detailsOverflow", indicator.gameObject);
            indicator.gameObject.SetActive(false);
        }

        private static void RefineActions(CityMapView view, StationUiSkin skin)
        {
            var primary = Read<Button>(view, "_primary");
            var cancel = Read<Button>(view, "_cancel");
            skin.Button(primary, primary: true);
            skin.Button(cancel);
            var row = primary.transform.parent.GetComponent<AdaptiveButtonRow>();
            row.padding = new RectOffset((int)skin.Metrics.margin, (int)skin.Metrics.margin,
                (int)skin.Metrics.innerGap, 0);
        }

        private static void RefinePoints(CityMapView view, StationUiSkin skin)
        {
            Bind(view, "_pointPaper", Sprite("Paper"));
            Bind(view, "_selectedPointPaper", Sprite("Action"));
            var bindings = new SerializedObject(view).FindProperty("_pointBindings");
            for (int index = 0; index < bindings.arraySize; index++)
            {
                var binding = bindings.GetArrayElementAtIndex(index);
                var plate = (Image)binding.FindPropertyRelative("_highlight").objectReferenceValue;
                skin.Surface(plate, "Paper", true);
                plate.rectTransform.sizeDelta = new Vector2(skin.Metrics.cardWidth + skin.Metrics.margin,
                    skin.Metrics.touchSize + skin.Metrics.gap);
                var outline = plate.gameObject.AddComponent<Outline>();
                outline.effectColor = skin.Metrics.ink;
                outline.effectDistance = Vector2.one * (skin.Metrics.innerGap / 4);
                outline.enabled = false;
                binding.FindPropertyRelative("_selectionOutline").objectReferenceValue = outline;
                foreach (var text in plate.GetComponentsInChildren<Text>())
                    skin.Text(text, skin.Metrics.labelSize, TextAnchor.MiddleCenter);
            }
            bindings.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            var map = Read<CityMapScrollRect>(view, "_mapScroll");
            foreach (var text in map.content.GetComponentsInChildren<Text>())
            {
                skin.Text(text, skin.Metrics.labelSize, TextAnchor.MiddleCenter);
                var plate = text.transform.parent.GetComponent<Image>();
                if (plate != null && plate.sprite == null) skin.Surface(plate, "Paper", plate.raycastTarget);
            }
        }

        private static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(ControlsPath)
            .OfType<Sprite>().Single(sprite => sprite.name == name);
    }
}
