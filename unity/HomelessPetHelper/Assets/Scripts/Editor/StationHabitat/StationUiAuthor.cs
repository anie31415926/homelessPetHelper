using System;
using System.Linq;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationHabitat.StationUiSkin;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.StationHabitat
{
    /// <summary>Incremental saved-prefab edit; consumes shared art and presets without reimporting them.</summary>
    public static class StationUiAuthor
    {
        private const string PanelPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        private static StationUiSkin _skin;

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated station-ui.ps1.");
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                if (root.GetComponent<MotionScope>() != null)
                    throw new InvalidOperationException("Station UI already styled. Edit saved Prefab Mode contents.");
                _skin = new StationUiSkin();
                var panel = root.GetComponent<StationPanelView>();
                var chrome = root.GetComponent<StationSceneChrome>();
                _skin.Scope = root.AddComponent<MotionScope>();
                Bind(_skin.Scope, "_library", AssetDatabase.LoadAssetAtPath<MotionLibrary>(
                    "Assets/Settings/UI/LevelMotion.asset"));
                Bind(chrome, "_motion", _skin.Scope);
                var layer = Rect("StationModalLayer", root.transform);
                layer.SetAsLastSibling();
                var backdrop = Read<Button>(chrome, "_backdrop");
                backdrop.transform.SetParent(layer, false); Stretch((RectTransform)backdrop.transform);
                backdrop.GetComponent<Image>().color = new Color32(0, 0, 0, 88);
                var safe = Rect("StationModalSafeArea", layer);
                safe.gameObject.AddComponent<SafeAreaView>();
                var detail = Read<GameObject>(panel, "_detail").GetComponent<RectTransform>();
                detail.SetParent(safe, false);
                Bind(chrome, "_detailInput", detail.gameObject.AddComponent<CanvasGroup>());
                Detail(panel, detail);
                Menu(panel, chrome, safe);
                Summary(panel, safe);
                Header(panel, root);
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button == backdrop) { button.GetComponent<ButtonAudio>().Silent = true; continue; }
                    if (button.GetComponentInParent<StationResidentView>(true) != null)
                    { button.GetComponent<ButtonAudio>().CueId = "ui.open"; continue; }
                    if (button.GetComponentInParent<StationHabitatView>(true) != null) continue;
                    _skin.Button(button, button.name == "Restart" || button.name == "Clean" ||
                        button.name == "Finish" || button.name == "Enter", button.name == "StationMenu" ?
                        "Settings" : null, Opens(button) ? "ui.open" : "ui.tap");
                }
                Read<CanvasGroup>(chrome, "_toast").transform.SetParent(safe, false);
                Toast(chrome);
                MenuGeometry(panel, chrome);
                CareGeometry(panel, chrome);
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                    if (text.name == "ObservationLabel") _skin.Text(text, _skin.Metrics.auxiliarySize,
                        TextAnchor.MiddleCenter);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Refine()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated station-ui.ps1.");
            var root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                _skin = new StationUiSkin { Scope = root.GetComponent<MotionScope>() };
                var panel = root.GetComponent<StationPanelView>();
                HeaderGeometry(panel, root);
                var detail = Read<GameObject>(panel, "_detail").GetComponent<RectTransform>();
                var content = detail.GetComponent<PopupContentLayout>();
                var body = Read<RectTransform>(content, "_content");
                var check = Read<Button>(panel, "_check");
                var clean = Read<Button>(panel, "_clean");
                check.transform.parent.SetParent(body, false);
                clean.transform.parent.SetParent(body, false);
                Read<Button>(panel, "_finish").transform.SetParent(detail, false);
                var image = body.GetComponent<Image>();
                if (image != null) image.color = Color.clear;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    foreach (var label in button.GetComponentsInChildren<Text>(true)) label.color = _skin.Metrics.ink;
                MenuGeometry(panel, root.GetComponent<StationSceneChrome>());
                CareGeometry(panel, root.GetComponent<StationSceneChrome>());
                Element(Read<Image>(panel, "_summaryAnimal").transform.parent, 300);
                foreach (var paper in root.GetComponentsInChildren<RectTransform>(true)
                    .Where(rect => rect.name == "PopupPaper")) Element(paper, 0);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static bool Opens(Button button) => new[] { "StationMenu", "History", "Clean", "Groom" }
            .Contains(button.name);

        private static void Detail(StationPanelView panel, RectTransform page)
        {
            var scroll = page.GetComponent<ScrollRect>();
            var content = scroll.content;
            var heading = Read<Text>(panel, "_name").transform.parent;
            var commands = Read<Button>(panel, "_check").transform.parent;
            var tools = Read<Button>(panel, "_clean").transform.parent;
            Remove<ScrollRect>(page); Remove<RectMask2D>(page);
            Remove<MinimumViewportHeight>(content);
            _skin.Column(page, (int)_skin.Metrics.margin);
            BottomPage(page);
            var body = Rect("StationDetailBodyViewport", page);
            body.gameObject.AddComponent<RectMask2D>();
            body.gameObject.AddComponent<Image>().color = Color.clear;
            heading.SetParent(page, false); heading.SetAsFirstSibling();
            commands.SetParent(content, false);
            if (tools != commands) tools.SetParent(content, false);
            Read<Button>(panel, "_finish").transform.SetParent(page, false);
            content.SetParent(body, false);
            _skin.Column(content, (int)_skin.Metrics.innerGap);
            var contentImage = content.GetComponent<Image>();
            if (contentImage != null) contentImage.color = Color.clear;
            _skin.Text(Read<Text>(panel, "_name"), _skin.Metrics.titleSize);
            Read<Text>(panel, "_name").GetComponent<LayoutElement>().flexibleWidth = 1;
            var close = Read<Button>(panel, "_back");
            var closeSize = Element(close.transform, _skin.Metrics.touchSize);
            closeSize.minWidth = closeSize.preferredWidth = _skin.Metrics.touchSize; closeSize.flexibleWidth = 0;
            var headingLayout = heading.GetComponent<HorizontalLayoutGroup>();
            headingLayout.spacing = _skin.Metrics.gap;
            headingLayout.childForceExpandWidth = headingLayout.childForceExpandHeight = false;
            Element(heading, _skin.Metrics.touchSize);
            _skin.Text(Read<Text>(panel, "_condition"), _skin.Metrics.bodySize);
            _skin.Text(Read<Text>(panel, "_progress"), _skin.Metrics.labelSize);
            SetupScroll(page, body, content);
            _skin.Paper(page);
        }

        private static void Menu(StationPanelView panel, StationSceneChrome chrome, RectTransform safe)
        {
            var menu = Read<GameObject>(chrome, "_menu").GetComponent<RectTransform>();
            menu.SetParent(safe, false); Stretch(menu);
            var items = menu.GetComponentsInChildren<VerticalLayoutGroup>(true).Single().GetComponent<RectTransform>();
            items.anchorMin = items.anchorMax = Vector2.one * .5f;
            items.pivot = Vector2.one * .5f; items.anchoredPosition = Vector2.zero;
            items.sizeDelta = new Vector2(600, 0);
            _skin.Column(items, (int)_skin.Metrics.margin);
            var fitter = items.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var close = menu.GetComponentsInChildren<Button>(true).Single(button => button.name == "CloseStationMenu");
            close.transform.SetParent(items, false); close.transform.SetAsFirstSibling();
            close.GetComponentInChildren<Text>(true).text = "X";
            Bind(chrome, "_menuClose", close);
            Element(close.transform, _skin.Metrics.touchSize);
            var toggle = Read<Toggle>(panel, "_pause");
            var label = toggle.GetComponentInChildren<Text>(true);
            _skin.Text(label, _skin.Metrics.bodySize);
            Element(toggle.transform, _skin.Metrics.touchSize);
            foreach (var image in toggle.GetComponentsInChildren<Image>(true))
                if (image != toggle.graphic) _skin.Surface(image, "Secondary", true);
            var graphicRect = (RectTransform)toggle.graphic.transform;
            graphicRect.anchorMin = graphicRect.anchorMax = new Vector2(0, .5f);
            graphicRect.sizeDelta = Vector2.one * 72; graphicRect.anchoredPosition = new Vector2(60, 0);
            label.rectTransform.anchorMin = new Vector2(.28f, 0); label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            var motion = toggle.gameObject.AddComponent<UiMotionFeedback>();
            var visual = Rect("ToggleMotionVisual", toggle.transform);
            foreach (Transform child in toggle.transform.Cast<Transform>().Where(child => child != visual).ToArray())
                child.SetParent(visual, false);
            Bind(motion, "_scope", _skin.Scope); Bind(motion, "_control", toggle); Bind(motion, "_visual", visual);
            UnityEventTools.AddStringPersistentListener(toggle.onValueChanged,
                panel.GetComponent<AudioScope>().Play, "ui.tap");
            _skin.Paper(items);
            var bounds = items.gameObject.AddComponent<BoundedPopup>(); Bind(bounds, "_bounds", safe);
        }

        private static void Summary(StationPanelView panel, RectTransform safe)
        {
            var page = Read<GameObject>(panel, "_summary").GetComponent<RectTransform>();
            page.SetParent(safe, false);
            page.anchorMin = page.anchorMax = Vector2.one * .5f;
            page.pivot = Vector2.one * .5f; page.anchoredPosition = Vector2.zero;
            var children = page.Cast<Transform>().ToArray();
            _skin.Column(page, (int)_skin.Metrics.margin);
            var viewport = Rect("StationSummaryBodyViewport", page);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var content = Rect("SummaryScrollContent", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            _skin.Column(content, (int)_skin.Metrics.innerGap);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var child in children)
                if (child.GetComponent<Button>() == null) child.SetParent(content, false);
            viewport.SetAsFirstSibling();
            Element(Read<Image>(panel, "_summaryAnimal").transform.parent, 300);
            foreach (var text in content.GetComponentsInChildren<Text>(true))
                _skin.Text(text, text == Read<Text>(panel, "_summaryBody") ?
                    _skin.Metrics.bodySize : _skin.Metrics.titleSize);
            SetupScroll(page, viewport, content);
            _skin.Paper(page);
        }

        private static void MenuGeometry(StationPanelView panel, StationSceneChrome chrome)
        {
            var close = Read<Button>(chrome, "_menuClose");
            var items = close.GetComponentInParent<VerticalLayoutGroup>(true).GetComponent<RectTransform>();
            if (close.transform.parent.name != "MenuHeading")
            {
                var heading = Rect("MenuHeading", items); heading.SetSiblingIndex(1);
                Element(heading, _skin.Metrics.touchSize);
                var row = heading.gameObject.AddComponent<HorizontalLayoutGroup>();
                row.spacing = _skin.Metrics.gap; row.childControlWidth = row.childControlHeight = true;
                row.childForceExpandWidth = row.childForceExpandHeight = false;
                var title = Rect("MenuTitle", heading).gameObject.AddComponent<Text>();
                var source = panel.GetComponentsInChildren<Text>(true).Single(text => text.name == "StationTitle");
                title.font = source.font; title.text = source.text; _skin.Text(title, _skin.Metrics.titleSize);
                Element(title.transform, _skin.Metrics.touchSize).flexibleWidth = 1;
                close.transform.SetParent(heading, false);
                var closeSize = Element(close.transform, _skin.Metrics.touchSize);
                closeSize.minWidth = closeSize.preferredWidth = _skin.Metrics.touchSize; closeSize.flexibleWidth = 0;
            }
            var menuTitle = close.transform.parent.GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "MenuTitle");
            Element(menuTitle.transform, _skin.Metrics.touchSize).flexibleWidth = 1;
            var toggle = Read<Toggle>(panel, "_pause");
            Remove<HorizontalLayoutGroup>(toggle);
            var visual = toggle.GetComponentInChildren<UiMotionFeedback>(true);
            var visualRect = Read<RectTransform>(visual, "_visual");
            Stretch(visualRect); visualRect.offsetMin = Vector2.one * _skin.Metrics.innerGap;
            visualRect.offsetMax = -Vector2.one * _skin.Metrics.innerGap;
            var label = toggle.GetComponentInChildren<Text>(true);
            var check = (Image)toggle.graphic;
            check.transform.SetParent(visualRect, false); label.transform.SetParent(visualRect, false);
            foreach (Transform child in visualRect.Cast<Transform>().ToArray())
                if (child != check.transform && child != label.transform) Object.DestroyImmediate(child.gameObject);
            var rootImage = toggle.GetComponent<Image>();
            rootImage.color = Color.clear; rootImage.raycastTarget = true;
            var image = visualRect.GetComponent<Image>();
            if (image == null) image = visualRect.gameObject.AddComponent<Image>();
            _skin.Surface(image, "Secondary"); toggle.targetGraphic = image;
            var box = Rect("ToggleBox", visualRect).gameObject.AddComponent<Image>();
            box.color = new Color32(240, 244, 236, 255); box.raycastTarget = false;
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = _skin.Metrics.ink; outline.effectDistance = new Vector2(2, -2);
            box.rectTransform.anchorMin = box.rectTransform.anchorMax = new Vector2(0, .5f);
            box.rectTransform.sizeDelta = Vector2.one * 66; box.rectTransform.anchoredPosition = new Vector2(66, 0);
            box.transform.SetAsFirstSibling();
            check.color = _skin.Metrics.ink; check.raycastTarget = false;
            check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0, .5f);
            check.rectTransform.sizeDelta = Vector2.one * 48;
            check.rectTransform.anchoredPosition = new Vector2(66, 0);
            label.gameObject.SetActive(true); _skin.Text(label, _skin.Metrics.bodySize);
            label.rectTransform.anchorMin = new Vector2(.28f, 0); label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        }

        private static void Header(StationPanelView panel, GameObject root)
        {
            var title = root.GetComponentsInChildren<Text>(true).Single(text => text.name == "StationTitle");
            var header = (RectTransform)title.transform.parent;
            var paper = Rect("StationHudPaper", header); paper.SetAsFirstSibling();
            paper.anchorMin = new Vector2(.02f, .02f); paper.anchorMax = new Vector2(.73f, .98f);
            _skin.Surface(paper.gameObject.AddComponent<Image>(), "Paper");
            HeaderGeometry(panel, root);
        }

        private static void CareGeometry(StationPanelView panel, StationSceneChrome chrome)
        {
            var size = Read<GameObject>(chrome, "_careStage").GetComponent<LayoutElement>();
            Bind(chrome, "_careStageSize", size);
            var feedback = Read<Text>(chrome, "_careFeedback");
            if (feedback == null)
            {
                feedback = Rect("CareFeedback", Read<RectTransform>(chrome, "_content"))
                    .gameObject.AddComponent<Text>();
                feedback.font = Read<Text>(panel, "_feedback").font;
                _skin.Text(feedback, _skin.Metrics.bodySize);
                var element = Element(feedback.transform, 0); element.preferredHeight = -1;
                Bind(chrome, "_careFeedback", feedback);
            }
            feedback.gameObject.SetActive(false);
        }

        private static void HeaderGeometry(StationPanelView panel, GameObject root)
        {
            var title = root.GetComponentsInChildren<Text>(true).Single(text => text.name == "StationTitle");
            var header = (RectTransform)title.transform.parent;
            header.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 276);
            Read<CanvasGroup>(panel, "_actions").GetComponent<RectTransform>().offsetMax = new Vector2(0, -276);
            title.rectTransform.anchorMin = new Vector2(.06f, .50f);
            title.rectTransform.anchorMax = new Vector2(.70f, .96f);
            _skin.Text(title, _skin.Metrics.titleSize);
            var clock = Read<Text>(panel, "_clock");
            clock.rectTransform.anchorMin = new Vector2(.06f, .26f);
            clock.rectTransform.anchorMax = new Vector2(.70f, .50f);
            _skin.Text(clock, _skin.Metrics.labelSize);
            var capacity = Read<Text>(panel, "_capacity");
            capacity.rectTransform.anchorMin = new Vector2(.06f, .03f);
            capacity.rectTransform.anchorMax = new Vector2(.70f, .26f);
            _skin.Text(capacity, _skin.Metrics.labelSize);
            foreach (var text in new[] { title, clock, capacity })
                text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        }

        private static void Toast(StationSceneChrome chrome)
        {
            var toast = Read<CanvasGroup>(chrome, "_toast").GetComponent<RectTransform>();
            var layout = _skin.Column(toast, (int)_skin.Metrics.innerGap);
            layout.spacing = 0;
            _skin.Surface(toast.GetComponent<Image>(), "Paper");
            foreach (var text in toast.GetComponentsInChildren<Text>(true))
                _skin.Text(text, _skin.Metrics.bodySize, TextAnchor.MiddleCenter);
        }
        private static void BottomPage(RectTransform page)
        {
            page.anchorMin = page.anchorMax = new Vector2(.5f, 0);
            page.pivot = new Vector2(.5f, 0); page.anchoredPosition = new Vector2(0, _skin.Metrics.margin);
        }
        private static void SetupScroll(RectTransform page, RectTransform viewport, RectTransform content)
        {
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var size = Element(viewport, 0);
            var controller = page.gameObject.AddComponent<PopupContentLayout>();
            Bind(controller, "_metrics", _skin.Metrics); Bind(controller, "_page", page);
            Bind(controller, "_viewport", viewport); Bind(controller, "_content", content);
            Bind(controller, "_bodySize", size); Bind(controller, "_scroll", scroll);
        }
    }
}
