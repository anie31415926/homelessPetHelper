using System;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Incremental saved-prefab repair. Run only in a disposable batch copy.</summary>
    public static class LevelVisibilityUpgrade
    {
        private const string Panels = "Assets/Prefabs/Panels/";
        private const string Widgets = "Assets/Prefabs/Widgets/";
        private static UiLayoutMetrics _metrics;

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a disposable batch copy.");
            _metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>("Assets/Settings/UI/LayoutMetrics.asset");
            EditorUtility.SetDirty(_metrics);
            Edit(Widgets + "Widget_LevelOverlays.prefab", Guide);
            Edit(Widgets + "Widget_SiegeChoice.prefab", Choice);
            Edit(Panels + "Panel_Approach.prefab", root => Focus(root.GetComponent<ApproachPanelView>().FieldScene));
            Edit(Panels + "Panel_SiegeTrial.prefab", Siege);
            AssetDatabase.SaveAssets();
        }

        private static void Focus(OutingFieldView scene)
        {
            var data = new SerializedObject(scene);
            data.FindProperty("_fitPlayableArea").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Guide(GameObject root)
        {
            var view = root.GetComponent<LevelOverlayView>();
            var caption = Ref<GameObject>(view, "_guideCaption");
            var fitter = caption.GetComponent<ContentSizeFitter>();
            if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
            var group = caption.GetComponent<LayoutGroup>();
            if (group != null) UnityEngine.Object.DestroyImmediate(group);
            var layout = caption.GetComponent<GuideCaptionLayout>();
            if (layout == null) layout = caption.AddComponent<GuideCaptionLayout>();
            Bind(layout, "_safeArea", caption.transform.parent); Bind(layout, "_metrics", _metrics);
            var text = Ref<Text>(view, "_guideText"); Bind(layout, "_text", text); Bind(view, "_guideLayout", layout);
            Place(text.rectTransform, 0, 0, 1, 1);
            text.rectTransform.offsetMin = Vector2.one * _metrics.gap;
            text.rectTransform.offsetMax = -Vector2.one * _metrics.gap;
            text.fontSize = _metrics.bodySize; text.lineSpacing = 1.15f;
            foreach (var graphic in caption.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var surface = caption.GetComponent<Image>();
            if (surface != null) surface.color = new Color32(255, 255, 255, 245);
            Ref<GuideMaskGraphic>(view, "_guideMask").color = new Color32(0, 0, 0, 56);
        }

        private static void Choice(GameObject root)
        {
            var cell = root.GetComponent<SiegeChoiceCell>();
            var label = Ref<Text>(cell, "_label");
            Place(label.rectTransform, .05f, .20f, .95f, .55f);
            label.lineSpacing = 1.1f;
            Place(Ref<SiegePropIcon>(cell, "_art").rectTransform, .05f, .60f, .35f, .92f);
            var icon = Ref<GameObject>(cell, "_costIcon"); Place((RectTransform)icon.transform, .40f, .69f, .60f, .88f);
            var cost = Ref<Text>(cell, "_cost"); Place(cost.rectTransform, .63f, .65f, .97f, .91f);
            var availability = Ref<Text>(cell, "_availability");
            Place(availability.rectTransform, .05f, .025f, .95f, .20f); availability.lineSpacing = 1;
        }

        private static void Siege(GameObject root)
        {
            var field = root.GetComponent<OutingFieldView>(); Focus(field);
            var view = root.GetComponentInChildren<SiegeFieldView>(true);
            var safe = (RectTransform)view.transform;
            var status = Ref<Text>(view, "_status");
            var statusPaper = (RectTransform)status.transform.parent.GetChild(status.transform.GetSiblingIndex() - 1);
            Top(statusPaper, .035f, .49f, _metrics.innerGap, _metrics.fieldHeaderHeight - _metrics.innerGap);
            Top(status.rectTransform, .055f, .475f, _metrics.gap, 72);
            status.fontSize = _metrics.labelSize; status.lineSpacing = 1;
            var resource = Ref<Text>(view, "_resource");
            Top(resource.rectTransform, .13f, .475f, 96, 72);
            var symbol = view.GetComponentInChildren<SiegeResourceGraphic>(true);
            Top(symbol.rectTransform, .05f, .115f, 108, 48);
            var pause = (RectTransform)Ref<Button>(view, "_pause").transform;
            var objectives = (RectTransform)Ref<Button>(view, "_objectives").transform;
            Top(pause, .52f, .745f, _metrics.gap, _metrics.touchSize);
            Top(objectives, .765f, .99f, _metrics.gap, _metrics.touchSize);
            var intent = Ref<Text>(view, "_intent");
            Top(intent.rectTransform, .54f, .97f, 180, 60);
            intent.fontSize = _metrics.labelSize; intent.lineSpacing = 1;
            var intentPaper = (RectTransform)intent.transform.parent.GetChild(intent.transform.GetSiblingIndex() - 1);
            Top(intentPaper, .52f, .99f, 180, 60);
            var dock = Ref<SiegeHandLayout>(view, "_dock");
            var progress = Ref<RectTransform>(dock, "_progress");
            Top(progress, .055f, .475f, 192, 30);
            var routes = Ref<GameObject>(view, "_routeRow");
            var routeLayout = routes.GetComponent<LayoutGroup>();
            if (routeLayout != null) UnityEngine.Object.DestroyImmediate(routeLayout);
            routes.transform.SetParent(Ref<SiegeBattlefield>(view, "_field").transform, false);
            Place((RectTransform)routes.transform, 0, 0, 1, 1);
            foreach (string key in new[] { "_top", "_mid", "_bottom" })
            {
                var rect = (RectTransform)Ref<Button>(view, key).transform;
                rect.pivot = Vector2.one / 2;
                rect.sizeDelta = new Vector2(_metrics.cardWidth, _metrics.touchSize);
            }
            var context = Ref<RectTransform>(view, "_context");
            var contextLayout = context.GetComponent<LayoutGroup>();
            if (contextLayout != null) UnityEngine.Object.DestroyImmediate(contextLayout);
            var row = context.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = true;
            row.spacing = _metrics.gap;
            foreach (RectTransform item in context)
                if (item.GetComponent<LayoutElement>() == null) item.gameObject.AddComponent<LayoutElement>();
            var hint = Ref<Text>(view, "_hint"); hint.fontSize = _metrics.labelSize; hint.lineSpacing = 1;
            var detail = Ref<Text>(view, "_detail");
            Top(detail.rectTransform, .055f, .475f, 240, 144);
        }

        private static void Top(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(left, 1); rect.anchorMax = new Vector2(right, 1);
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = new Vector2(0, -top);
        }

        private static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
