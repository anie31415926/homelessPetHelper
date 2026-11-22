using System;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Incremental Prefab Mode edits of the saved UI. Never recreates gameplay panels or artwork.</summary>
    public static class UiLayoutMigration
    {
        private const string MetricsPath = "Assets/Settings/UI/LayoutMetrics.asset";
        private const string Panels = "Assets/Prefabs/Panels/";
        private const string Widgets = "Assets/Prefabs/Widgets/";
        private static UiLayoutMetrics _metrics;

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated saved snapshot.");
            _metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>(MetricsPath);
            if (_metrics == null)
            {
                _metrics = ScriptableObject.CreateInstance<UiLayoutMetrics>();
                AssetDatabase.CreateAsset(_metrics, MetricsPath);
            }
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path.StartsWith(Widgets) ? 0 : 1))
                Edit(path, General);
            Edit(Widgets + "Widget_SiegeChoice.prefab", Choice);
            Edit(Widgets + "Widget_LevelOverlays.prefab", Overlays);
            Edit(Panels + "Panel_SiegeTrial.prefab", Siege);
            Edit(Panels + "Panel_Approach.prefab", Approach);
            Edit(Panels + "Panel_CityMap.prefab", root => UiLayoutMapMigration.Apply(root, _metrics));
            Edit(Panels + "Panel_StationLoop.prefab", Station);
            Edit(Panels + "Panel_StationWhiteboard.prefab", UiLayoutLegacyMigration.Station);
            Edit(Panels + "Panel_OptionWhiteboard.prefab", UiLayoutLegacyMigration.Page);
            Edit(Panels + "Panel_RescueCase.prefab", UiLayoutLegacyMigration.Rescue);
            Edit(Panels + "Panel_PetMotion.prefab", UiLayoutLegacyMigration.PetMotion);
            Edit(Widgets + "Widget_RescueActionBar.prefab", UiLayoutLegacyMigration.ActionBar);
            AssetDatabase.SaveAssets();
        }

        private static void General(GameObject root)
        {
            foreach (var scaler in root.GetComponentsInChildren<CanvasScaler>(true))
                scaler.matchWidthOrHeight = 0;
            StyleTexts(root);
            ConfigureTouchTargets(root);
        }

        private static void StyleTexts(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                // Nested widgets retain one source of styling; inherited values are edited there.
                if (PrefabUtility.IsPartOfPrefabInstance(text) &&
                    PrefabUtility.GetNearestPrefabInstanceRoot(text) != root) continue;
                bool button = text.GetComponentInParent<Selectable>(true) != null;
                int size = button ? _metrics.bodySize : text.name == "Title" || text.name == "Heading" ?
                    _metrics.titleSize : text.name == "State" || text.name.EndsWith("Label") ||
                        text.name == "RestLabel" || text.name == "Supplies" ? _metrics.labelSize : _metrics.bodySize;
                Style(text, size);
                var textSize = text.GetComponent<LayoutElement>();
                if (textSize != null) { textSize.minHeight = 0; textSize.preferredHeight = -1; }
                if (text.name == "ObservationLabel" || text.name == "ResidentName")
                {
                    text.rectTransform.anchorMin = text.rectTransform.anchorMax =
                        new Vector2(.5f, text.name == "ResidentName" ? 0 : 1);
                    text.rectTransform.anchoredPosition = new Vector2(0, -36);
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 420);
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 72);
                }
            }
            foreach (var row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(row) &&
                    PrefabUtility.GetNearestPrefabInstanceRoot(row) != root) continue;
                var height = row.GetComponent<LayoutElement>();
                if (height != null) { height.minHeight = 0; height.preferredHeight = -1; }
            }
        }

        private static void ConfigureTouchTargets(GameObject root)
        {
            foreach (var control in root.GetComponentsInChildren<Selectable>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(control) &&
                    PrefabUtility.GetNearestPrefabInstanceRoot(control) != root) continue;
                var element = control.GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minWidth = Mathf.Max(_metrics.touchSize, element.minWidth);
                    element.minHeight = Mathf.Max(_metrics.touchSize, element.minHeight);
                    if (element.preferredHeight > 0) element.preferredHeight =
                        Mathf.Max(element.preferredHeight, _metrics.touchSize);
                }
                var rect = (RectTransform)control.transform;
                if (rect.anchorMin.y == rect.anchorMax.y)
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                        Mathf.Max(rect.rect.height, _metrics.touchSize));
                if (rect.anchorMin.x == rect.anchorMax.x)
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        Mathf.Max(rect.rect.width, _metrics.touchSize));
                var colors = control.colors;
                colors.disabledColor = new Color32(210, 214, 203, 255);
                control.colors = colors;
                if (control.targetGraphic is Image image)
                {
                    Color linear = image.color.linear;
                    float luminance = .2126f * linear.r + .7152f * linear.g + .0722f * linear.b;
                    Color ink = image.sprite == null && luminance < .2f ? Color.white : _metrics.ink;
                    foreach (var text in control.GetComponentsInChildren<Text>(true)) text.color = ink;
                }
            }
        }

        internal static void Style(Text text, int size)
        {
            text.fontSize = size; text.resizeTextForBestFit = false;
            text.resizeTextMinSize = text.resizeTextMaxSize = size;
            text.color = _metrics.ink; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.lineSpacing = text.name == "Label" ? 1.2f : 1.4f;
        }

        private static void Choice(GameObject root)
        {
            var cell = root.GetComponent<SiegeChoiceCell>();
            var visual = root.GetComponent<Button>().targetGraphic.rectTransform;
            var label = Ref<Text>(cell, "_label");
            Style(label, _metrics.labelSize); label.alignment = TextAnchor.UpperCenter;
            Place(label.rectTransform, .10f, .16f, .90f, .43f);
            label.lineSpacing = 1.2f;
            Place(Ref<SiegePropIcon>(cell, "_art").rectTransform, .20f, .45f, .80f, .73f);
            var roster = Text("RosterLabel", visual, .10f, .10f, .90f, .90f, _metrics.labelSize);
            Style(roster, _metrics.labelSize); roster.alignment = TextAnchor.UpperLeft;
            Bind(cell, "_rosterLabel", roster);
            var cost = Text("Cost", visual, .38f, .75f, .90f, .93f, _metrics.labelSize);
            Style(cost, _metrics.labelSize); cost.alignment = TextAnchor.MiddleRight;
            var resource = Rect("ResourceIcon", visual, .10f, .75f, .36f, .93f)
                .gameObject.AddComponent<SiegeResourceGraphic>();
            resource.color = _metrics.ink; resource.raycastTarget = false;
            var mark = Rect("SelectedMark", visual, .90f, .90f, .96f, .96f).gameObject.AddComponent<Image>();
            mark.color = _metrics.ink; mark.raycastTarget = false; mark.gameObject.SetActive(false);
            var unavailable = Text("Availability", visual, .04f, 0, .96f, .16f, _metrics.labelSize);
            Style(unavailable, _metrics.labelSize); unavailable.gameObject.SetActive(false);
            Bind(cell, "_costIcon", resource.gameObject);
            unavailable.lineSpacing = 1;
            Bind(cell, "_cost", cost); Bind(cell, "_availability", unavailable);
            Bind(cell, "_selectedMark", mark.gameObject);
            var element = root.GetComponent<LayoutElement>();
            element.minWidth = _metrics.cardWidth; element.minHeight = _metrics.cardHeight;
            element.preferredHeight = _metrics.cardHeight;
            ((RectTransform)root.transform).sizeDelta = new Vector2(_metrics.cardWidth, _metrics.cardHeight);
        }

        private static void Siege(GameObject root)
        {
            var view = root.GetComponentInChildren<SiegeFieldView>(true);
            var hand = Ref<RectTransform>(view, "_hand");
            var old = hand.GetComponent<HorizontalLayoutGroup>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var grid = hand.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.cellSize = new Vector2(_metrics.cardWidth, _metrics.cardHeight);
            grid.childAlignment = TextAnchor.UpperLeft;
            var safe = (RectTransform)view.transform;
            var dock = safe.gameObject.AddComponent<SiegeHandLayout>();
            var empty = Text("EmptyHand", safe, 0, 0, 1, 1, _metrics.bodySize);
            Style(empty, _metrics.bodySize); empty.gameObject.SetActive(false);
            Bind(dock, "_empty", empty.rectTransform); Bind(view, "_emptyHand", empty);
            Bind(dock, "_metrics", _metrics); Bind(dock, "_hand", hand); Bind(dock, "_grid", grid);
            Bind(dock, "_submit", Ref<Button>(view, "_submit").transform);
            Bind(dock, "_hint", Ref<Text>(view, "_hint").transform);
            var progress = Rect("ChargeGroup", safe);
            var player = Ref<Slider>(view, "_playerBar"); var animal = Ref<Slider>(view, "_animalBar");
            player.transform.SetParent(progress, false); animal.transform.SetParent(progress, false);
            Place((RectTransform)player.transform, 0, 0, .48f, 1);
            Place((RectTransform)animal.transform, .52f, 0, 1, 1);
            Bind(dock, "_progress", progress);
            var scene = root.GetComponent<OutingFieldView>();
            var sceneArea = (RectTransform)scene.Frame.parent;
            sceneArea.anchorMin = Vector2.zero; sceneArea.anchorMax = Vector2.one;
            sceneArea.offsetMax = new Vector2(0, -_metrics.margin - 2 * (_metrics.touchSize + _metrics.gap) -
                _metrics.captionHeight - _metrics.gap);
            Bind(dock, "_sceneArea", sceneArea); Bind(view, "_dock", dock);
            ConfigureSiegeHud(view, safe);
            ConfigureContext(view, dock, safe);
            ConfigureLegacyHand(root);
        }

        private static void ConfigureSiegeHud(SiegeFieldView view, RectTransform safe)
        {
            var status = Ref<Text>(view, "_status");
            Place(status.rectTransform, .065f, .86f, .465f, .98f); Style(status, _metrics.bodySize);
            var paper = status.transform.parent.GetChild(status.transform.GetSiblingIndex() - 1);
            Place((RectTransform)paper, .04f, .80f, .49f, .995f);
            var resource = Text("ResourceBalance", safe, .13f, .805f, .48f, .855f, _metrics.bodySize);
            resource.alignment = TextAnchor.MiddleLeft; Style(resource, _metrics.bodySize);
            var symbol = Rect("ResourceIcon", safe, .045f, .805f, .12f, .855f).gameObject
                .AddComponent<SiegeResourceGraphic>();
            symbol.color = _metrics.ink; symbol.raycastTarget = false; Bind(view, "_resource", resource);
            var intent = Ref<Text>(view, "_intent");
            float intentTop = _metrics.margin + 2 * (_metrics.touchSize + _metrics.gap);
            TopStrip(intent.rectTransform, .565f, .935f, intentTop + _metrics.innerGap,
                _metrics.captionHeight - 2 * _metrics.innerGap);
            TopStrip((RectTransform)intent.transform.parent.GetChild(intent.transform.GetSiblingIndex() - 1),
                .54f, .96f, intentTop, _metrics.captionHeight);
            Corner(Ref<Button>(view, "_pause"), safe, 0);
            Corner(Ref<Button>(view, "_objectives"), safe, 1);
        }

        private static void TopStrip(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(left, 1); rect.anchorMax = new Vector2(right, 1);
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(0, height);
            rect.anchoredPosition = new Vector2(0, -top);
        }

        private static void ConfigureContext(SiegeFieldView view, SiegeHandLayout dock, RectTransform safe)
        {
            var context = Ref<RectTransform>(view, "_context");
            context.SetParent(safe, false);
            Ref<Button>(view, "_clear").transform.SetParent(context, false);
            Bind(dock, "_context", context);
            var horizontal = context.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null) UnityEngine.Object.DestroyImmediate(horizontal);
            var wrap = context.gameObject.AddComponent<AdaptiveButtonRow>();
            Bind(wrap, "_metrics", _metrics);
            var fitter = context.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = context.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            context.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 864);
            var routes = Rect("RouteActions", safe);
            var routeLayout = routes.gameObject.AddComponent<AdaptiveButtonRow>();
            Bind(routeLayout, "_metrics", _metrics); Bind(dock, "_routes", routes);
            Bind(view, "_routeRow", routes.gameObject);
            foreach (var button in new[] { "_top", "_mid", "_bottom" })
            {
                var rect = (RectTransform)Ref<Button>(view, button).transform;
                rect.SetParent(routes, false);
                rect.sizeDelta = new Vector2(192, _metrics.touchSize);
            }
        }

        private static void ConfigureLegacyHand(GameObject root)
        {
            // The alternate editor presentation shares the upgraded card, so its hand also needs height.
            var legacy = root.GetComponent<SiegeTrialView>();
            var legacyContent = Ref<GameObject>(legacy, "_legacyContent");
            var scroll = ScrollContent((RectTransform)legacyContent.transform);
            Bind(legacy, "_legacyContent", scroll.gameObject);
            var legacyHand = Ref<RectTransform>(legacy, "_handRoot");
            var size = legacyHand.GetComponent<LayoutElement>();
            if (size != null) size.minHeight = size.preferredHeight = _metrics.cardHeight;
            foreach (var row in legacyContent.GetComponentsInChildren<HorizontalLayoutGroup>(true))
            {
                if (row.transform == legacyHand) continue;
                if (Enumerable.Range(0, row.transform.childCount).Any(index =>
                    row.transform.GetChild(index).GetComponent<Selectable>() != null))
                    WrapRow((RectTransform)row.transform);
            }
        }

        private static void Corner(Button button, RectTransform safe, int row)
        {
            var rect = (RectTransform)button.transform;
            rect.SetParent(safe, false); rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one; rect.sizeDelta = new Vector2(288, _metrics.touchSize);
            rect.anchoredPosition = new Vector2(-_metrics.margin,
                -_metrics.margin - row * (_metrics.touchSize + _metrics.gap));
        }

        private static void Approach(GameObject root)
        {
            var view = root.GetComponent<ApproachPanelView>();
            var safe = (RectTransform)view.PauseTarget.parent;
            // Existing placement data owns mode changes; clear only these controls from that table.
            var placement = root.GetComponentInChildren<FieldPanelPlacement>(true);
            var serialized = new SerializedObject(placement);
            var placements = serialized.FindProperty("_placements");
            for (int index = placements.arraySize - 1; index >= 0; index--)
            {
                var target = placements.GetArrayElementAtIndex(index).FindPropertyRelative("Target")
                    .objectReferenceValue as RectTransform;
                if (target == null) continue;
                if (target == view.PauseTarget || target == view.ObjectivesTarget ||
                    new[] { "Litter", "Weeds", "Mud", "Undo", "Continue" }.Contains(target.name))
                    placements.DeleteArrayElementAtIndex(index);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Corner(Ref<ApproachCommandView>(view, "_pause").GetComponent<Button>(), safe, 0);
            Corner(Ref<Button>(view, "_objectives"), safe, 1);
            var dock = Rect("OpeningActions", safe, 0, 0, 1, 0);
            dock.offsetMin = new Vector2(_metrics.margin, _metrics.margin);
            dock.offsetMax = new Vector2(-_metrics.margin, _metrics.margin + 336);
            var wrap = dock.gameObject.AddComponent<AdaptiveButtonRow>(); Bind(wrap, "_metrics", _metrics);
            foreach (string field in new[] { "_litter", "_weeds", "_mud", "_undo", "_continue" })
            {
                var button = Ref<ApproachCommandView>(view, field).GetComponent<Button>();
                button.transform.SetParent(dock, false);
                var size = button.GetComponent<LayoutElement>();
                if (size == null) size = button.gameObject.AddComponent<LayoutElement>();
                size.minHeight = size.preferredHeight = _metrics.touchSize;
            }
            var area = (RectTransform)view.FieldScene.Frame.parent;
            area.anchorMin = Vector2.zero; area.anchorMax = new Vector2(1, .78f);
            area.offsetMin = new Vector2(0, 408); area.offsetMax = Vector2.zero;
        }

        private static void Overlays(GameObject root)
        {
            foreach (var page in root.GetComponentsInChildren<RectTransform>(true).Where(rect =>
                new[] { "PausePage", "SettingsPage", "ObjectivesPage", "ConfirmationPage" }.Contains(rect.name)))
            {
                page.anchorMin = page.anchorMax = new Vector2(.5f, .5f);
                page.sizeDelta = new Vector2(984, 0);
                var fitter = page.GetComponent<ContentSizeFitter>();
                if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
                var layout = page.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(48, 48, 48, 48); layout.spacing = _metrics.gap;
                var children = Enumerable.Range(0, page.childCount).Select(page.GetChild).ToArray();
                var viewport = Rect("ScrollableBody", page);
                var image = viewport.gameObject.AddComponent<Image>(); image.color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>();
                var element = viewport.gameObject.AddComponent<LayoutElement>();
                var content = Rect("BodyContent", viewport, 0, 1, 1, 1);
                content.pivot = new Vector2(.5f, 1);
                var body = content.gameObject.AddComponent<VerticalLayoutGroup>();
                body.spacing = _metrics.gap; body.childControlWidth = body.childControlHeight = true;
                body.childForceExpandWidth = true; body.childForceExpandHeight = false;
                var contentFit = content.gameObject.AddComponent<ContentSizeFitter>();
                contentFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                foreach (var child in children)
                {
                    bool footer = child.name == "CardActions";
                    bool header = child.name == "CardHeader";
                    if (!footer && !header) child.SetParent(content, false);
                    if (footer)
                    {
                        var old = child.GetComponent<HorizontalLayoutGroup>();
                        if (old != null) UnityEngine.Object.DestroyImmediate(old);
                        var wrap = child.gameObject.AddComponent<AdaptiveButtonRow>();
                        Bind(wrap, "_metrics", _metrics);
                        var footerSize = child.GetComponent<LayoutElement>();
                        if (footerSize != null) { footerSize.minHeight = 0; footerSize.preferredHeight = -1; }
                    }
                    if (header)
                    {
                        var size = child.GetComponent<LayoutElement>();
                        size.minHeight = size.preferredHeight = 96;
                    }
                }
                viewport.SetSiblingIndex(1);
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                var controller = page.gameObject.AddComponent<PopupContentLayout>();
                Bind(controller, "_metrics", _metrics); Bind(controller, "_page", page);
                Bind(controller, "_viewport", viewport); Bind(controller, "_content", content);
                Bind(controller, "_bodySize", element); Bind(controller, "_scroll", scroll);
            }
            foreach (var row in root.GetComponentsInChildren<RectTransform>(true).Where(rect =>
                new[] { "MasterRow", "MusicRow", "EffectsRow" }.Contains(rect.name)))
            {
                var slider = row.GetComponentInChildren<Slider>(true);
                var toggle = row.GetComponentInChildren<Toggle>(true);
                row.GetComponent<LayoutElement>().minHeight = row.GetComponent<LayoutElement>().preferredHeight = 216;
                Place((RectTransform)slider.transform, 0, 0, .65f, .68f);
                Place((RectTransform)toggle.transform, .70f, 0, 1, .68f);
            }
        }

        private static void Station(GameObject root)
        {
            var panel = root.GetComponent<StationPanelView>();
            var chrome = panel.GetComponent<HomelessPetHelper.Modules.Station.UI.Habitat.StationSceneChrome>();
            Bind(chrome, "_metrics", _metrics);
            var header = root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "SceneHeader");
            header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1); header.sizeDelta = new Vector2(0, 216);
            header.anchoredPosition = Vector2.zero;
            var body = (RectTransform)Ref<GameObject>(panel, "_detail").transform.parent;
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = Vector2.zero; body.offsetMax = new Vector2(0, -216);
            var footer = Ref<GameObject>(chrome, "_footer").GetComponent<RectTransform>();
            footer.anchorMin = footer.anchorMax = Vector2.zero;
            footer.anchorMax = new Vector2(1, 0); footer.pivot = Vector2.zero;
            footer.offsetMin = new Vector2(_metrics.margin, _metrics.margin);
            footer.offsetMax = new Vector2(-_metrics.margin, _metrics.margin + _metrics.touchSize);
            var outing = Ref<Button>(panel, "_restart");
            Place((RectTransform)outing.transform, .55f, 0, 1, 1);
            var menu = Ref<GameObject>(chrome, "_menu");
            var items = menu.GetComponentsInChildren<VerticalLayoutGroup>(true).Single();
            ((RectTransform)items.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 360);
            var commands = Ref<Button>(panel, "_check").transform.parent;
            WrapRow((RectTransform)commands);
            var tools = Ref<Button>(panel, "_clean").transform.parent;
            if (tools != commands) WrapRow((RectTransform)tools);
            var condition = Ref<Text>(panel, "_condition");
            var animalHeader = Ref<Text>(panel, "_name").transform.parent.GetComponent<LayoutElement>();
            animalHeader.minHeight = animalHeader.preferredHeight = _metrics.touchSize;
            var size = condition.GetComponent<LayoutElement>();
            size.minHeight = 0; size.preferredHeight = -1;
            var detail = Ref<GameObject>(panel, "_detail").GetComponent<RectTransform>();
            var stage = Ref<GameObject>(chrome, "_careStage").GetComponent<LayoutElement>();
            stage.minHeight = stage.preferredHeight = 720;
            detail.gameObject.AddComponent<MinimumViewportHeight>();
            var detailViewport = ScrollContent(detail);
            Bind(panel, "_detail", detailViewport.gameObject);
            Bind(chrome, "_detail", detailViewport); Bind(chrome, "_content", detail);
            Ref<Text>(panel, "_capacity").alignment = TextAnchor.MiddleLeft;
            ConfigureStationToast(panel);
        }

        private static void ConfigureStationToast(StationPanelView panel)
        {
            var toast = (RectTransform)Ref<Text>(panel, "_feedback").transform.parent;
            toast.anchorMin = new Vector2(.06f, .11f); toast.anchorMax = new Vector2(.94f, .11f);
            toast.pivot = new Vector2(.5f, 0); toast.offsetMin = toast.offsetMax = Vector2.zero;
            var toastLayout = toast.gameObject.AddComponent<VerticalLayoutGroup>();
            toastLayout.childControlHeight = toastLayout.childControlWidth = true;
            toastLayout.childForceExpandHeight = false;
            toast.gameObject.AddComponent<Image>().color = new Color32(248, 252, 249, 245);
            toast.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        internal static RectTransform ScrollContent(RectTransform content)
        {
            if (content.GetComponent<HomelessPetHelper.Modules.Rescue.UI.SafeAreaView>() != null)
            {
                var children = Enumerable.Range(0, content.childCount).Select(content.GetChild).ToArray();
                var inner = Rect("ScrollPageContent", content);
                var old = content.GetComponent<VerticalLayoutGroup>();
                var group = inner.gameObject.AddComponent<VerticalLayoutGroup>();
                EditorUtility.CopySerialized(old, group);
                UnityEngine.Object.DestroyImmediate(old);
                foreach (var child in children) child.SetParent(inner, false);
                content = inner;
            }
            bool wasActive = content.gameObject.activeSelf;
            var parent = content.parent;
            var viewport = Rect("LegacyViewport", parent);
            viewport.gameObject.AddComponent<RectMask2D>();
            var image = viewport.gameObject.AddComponent<Image>(); image.color = Color.clear;
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            content.gameObject.SetActive(true);
            viewport.gameObject.SetActive(wasActive);
            return viewport;
        }

        internal static void WrapRow(RectTransform row)
        {
            var old = row.GetComponent<HorizontalLayoutGroup>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var wrap = row.gameObject.AddComponent<AdaptiveButtonRow>(); Bind(wrap, "_metrics", _metrics);
            var size = row.GetComponent<LayoutElement>();
            if (size != null) { size.minHeight = 0; size.preferredHeight = -1; }
        }

        private static void Edit(string path, Action<GameObject> action)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
