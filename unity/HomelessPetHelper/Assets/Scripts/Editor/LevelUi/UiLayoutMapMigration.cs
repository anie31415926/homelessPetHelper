using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    internal static class UiLayoutMapMigration
    {
        internal static void Apply(GameObject root, UiLayoutMetrics metrics)
        {
            var view = root.GetComponent<CityMapView>();
            foreach (var point in root.GetComponentsInChildren<Button>(true).Where(button =>
                button.GetComponentInParent<AspectRatioFitter>() != null))
            {
                var target = point.gameObject.AddComponent<MinimumTouchTarget>();
                Bind(target, "_metrics", metrics);
            }
            var primary = Ref<Button>(view, "_primary");
            var row = (RectTransform)primary.transform.parent;
            UnityEngine.Object.DestroyImmediate(row.GetComponent<HorizontalLayoutGroup>());
            var wrap = row.gameObject.AddComponent<AdaptiveButtonRow>(); Bind(wrap, "_metrics", metrics);
            wrap.padding = new RectOffset(48, 48, 24, 48);
            var rowSize = row.GetComponent<LayoutElement>();
            if (rowSize != null) { rowSize.minHeight = 0; rowSize.preferredHeight = -1; }
            var art = root.GetComponentsInChildren<AspectRatioFitter>(true).Single();
            SizeNameplates(art, metrics);
            CreateScrollableMap(view, primary, art);
            foreach (var text in new[] { Ref<Text>(view, "_details"), Ref<Text>(view, "_departure"),
                Ref<Text>(view, "_status"), Ref<Text>(view, "_feedback") })
            {
                var size = text.GetComponent<LayoutElement>();
                if (size != null) { size.minHeight = 0; size.preferredHeight = -1; }
            }
            Ref<Text>(view, "_heading").alignment = TextAnchor.MiddleLeft;
        }

        private static void SizeNameplates(AspectRatioFitter art, UiLayoutMetrics metrics)
        {
            foreach (var plate in art.GetComponentsInChildren<Image>(true).Where(image => image.name == "Nameplate"))
            {
                int rows = plate.GetComponentsInChildren<Text>(true).Length;
                var rect = plate.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0);
                rect.sizeDelta = new Vector2(264, rows * 72);
                rect.anchoredPosition = Vector2.zero;
                foreach (var text in plate.GetComponentsInChildren<Text>(true))
                {
                    bool state = text.name == "State";
                    Place(text.rectTransform, 0, state ? 0 : (rows - 1f) / rows, 1, state ? .5f : 1);
                    text.rectTransform.offsetMin = new Vector2(12, 0);
                    text.rectTransform.offsetMax = new Vector2(-12, 0);
                    UiLayoutMigration.Style(text, metrics.labelSize); text.lineSpacing = 1;
                    text.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        private static void CreateScrollableMap(CityMapView view, Button primary, AspectRatioFitter art)
        {
            var safe = (RectTransform)Ref<Text>(view, "_status").transform.parent.parent;
            var map = (RectTransform)art.transform.parent;
            var destination = (RectTransform)primary.transform.parent.parent;
            var actions = (RectTransform)primary.transform.parent;
            actions.SetParent(safe, false);
            var viewport = Rect("MapBodyViewport", safe);
            viewport.SetSiblingIndex(1);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var viewportSize = viewport.gameObject.AddComponent<LayoutElement>();
            viewportSize.flexibleHeight = 1;
            var content = Rect("MapBodyContent", viewport, 0, 1, 1, 1);
            content.pivot = new Vector2(.5f, 1);
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childControlHeight = column.childControlWidth = true;
            column.childForceExpandHeight = false; column.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            map.SetParent(content, false); destination.SetParent(content, false);
            var aspect = map.gameObject.AddComponent<PreferredAspectHeight>();
            var serialized = new SerializedObject(aspect);
            serialized.FindProperty("_aspect").floatValue = art.aspectRatio;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
        }
    }
}
