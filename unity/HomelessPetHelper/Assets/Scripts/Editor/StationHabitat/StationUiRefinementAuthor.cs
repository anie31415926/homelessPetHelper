using System;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationHabitat.StationUiSkin;

namespace HomelessPetHelper.Editor.StationHabitat
{
    /// <summary>Local Prefab Mode edits only; preserves room art, resident bindings and authored gameplay.</summary>
    public static class StationUiRefinementAuthor
    {
        private const string PanelPath = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        private const string OverlayPath = "Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab";
        private const string FontPath = "Assets/Art/Fonts/PetRound-Game.ttf";

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            var skin = new StationUiSkin();
            var paper = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/T_UI_LevelControls.png")
                .OfType<Sprite>().Single(sprite => sprite.name == "Paper");
            skin.Metrics.disabledButtonPaper = paper;
            EditorUtility.SetDirty(skin.Metrics);
            Edit(PanelPath, root => RefineStation(root, skin));
            // Edit shared widgets first so nested instances inherit their new states and font.
            var paths = AssetDatabase.FindAssets("t:Prefab",
                new[] { "Assets/Prefabs/Widgets", "Assets/Prefabs/Panels" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path.Contains("/Widgets/") ? 0 : 1);
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) throw new InvalidOperationException("Build the reviewed font subset before applying.");
            foreach (var path in paths)
                Edit(path, root =>
                {
                    foreach (var text in root.GetComponentsInChildren<Text>(true)) text.font = font;
                    foreach (var control in root.GetComponentsInChildren<Selectable>(true))
                        if (control.targetGraphic is Image image && image.sprite != null &&
                            (image.sprite.name == "Action" || image.sprite.name == "Secondary"))
                            StyleState(control, image, skin.Metrics);
                });
            var content = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Game/GameContent.asset");
            Bind(content, "_font", font);
            AssetDatabase.SaveAssets();
        }

        public static void Polish()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            Edit(PanelPath, root =>
            {
                var menu = Read<Button>(root.GetComponent<StationSceneChrome>(), "_menuButton");
                var caption = menu.GetComponentInChildren<Text>(true);
                caption.rectTransform.offsetMin = Vector2.one * new StationUiSkin().Metrics.innerGap;
                caption.rectTransform.offsetMax = -caption.rectTransform.offsetMin;
            });
            AssetDatabase.SaveAssets();
        }

        public static void CompleteBindings()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch editor.");
            Edit(OverlayPath, root =>
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var audio = button.GetComponent<ButtonAudio>() ?? button.gameObject.AddComponent<ButtonAudio>();
                    audio.CueId = button.name == "OverlaySettings" || button.name == "OverlayResetAudio" ||
                        button.name == "OverlayReturn" || button.name == "OverlayReplay" ? "ui.open" : "ui.tap";
                }
            });
            Edit("Assets/Prefabs/Panels/Panel_StationWhiteboard.prefab", root =>
            {
                var panel = root.GetComponent<StationPanelView>();
                if (Read<Text>(panel, "_careAvailability") != null) return;
                var condition = Read<Text>(panel, "_condition");
                var reason = Rect("CareAvailability", condition.transform.parent).gameObject.AddComponent<Text>();
                reason.font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                var skin = new StationUiSkin(); skin.Text(reason, skin.Metrics.auxiliarySize);
                Element(reason.transform, 0).preferredHeight = -1;
                reason.gameObject.SetActive(false);
                Bind(panel, "_careAvailability", reason);
            });
            AssetDatabase.SaveAssets();
        }

        private static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RefineStation(GameObject root, StationUiSkin skin)
        {
            var panel = root.GetComponent<StationPanelView>();
            var chrome = root.GetComponent<StationSceneChrome>();
            skin.Scope = root.GetComponent<MotionScope>();
            var copy = new OptionPreviewText(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/StationPreviewText.json"));
            var menu = Read<Button>(chrome, "_menuButton");
            var visual = Read<RectTransform>(menu.GetComponent<UiMotionFeedback>(), "_visual");
            var icon = visual.GetComponentsInChildren<Image>(true).Single(image => image.name == "SharedIcon");
            icon.color = Color.white;
            icon.gameObject.SetActive(false);
            var caption = menu.GetComponentInChildren<Text>(true);
            caption.text = copy.Get("menu.caption"); caption.gameObject.SetActive(true);
            skin.Text(caption, skin.Metrics.labelSize, TextAnchor.MiddleCenter);
            caption.rectTransform.offsetMin = Vector2.one * skin.Metrics.innerGap;
            caption.rectTransform.offsetMax = -caption.rectTransform.offsetMin;
            visual.offsetMin = Vector2.one * skin.Metrics.innerGap;
            visual.offsetMax = -Vector2.one * skin.Metrics.innerGap;
            // Keep the corner target unchanged; the label makes this general menu unambiguous.
            var close = Read<Button>(chrome, "_menuClose");
            var items = close.transform.parent.parent;
            var settingsRect = Rect("StationSettings", items);
            Element(settingsRect, skin.Metrics.touchSize);
            var settings = settingsRect.gameObject.AddComponent<Button>();
            var label = Rect("Label", settingsRect).gameObject.AddComponent<Text>();
            label.font = caption.font; label.text = copy.Get("menu.settings");
            skin.Button(settings, cue: "ui.open");
            Bind(chrome, "_settings", settings);
            settings.gameObject.SetActive(false); // Preview has no persistence/audio owner; Main explicitly enables it.
            var content = Read<RectTransform>(chrome, "_content");
            var availability = Rect("CareAvailability", content).gameObject.AddComponent<Text>();
            availability.font = caption.font;
            skin.Text(availability, skin.Metrics.auxiliarySize);
            Element(availability.transform, 0).preferredHeight = -1;
            availability.transform.SetSiblingIndex(
                Read<Button>(panel, "_clean").transform.parent.GetSiblingIndex() + 1);
            availability.gameObject.SetActive(false);
            Bind(panel, "_careAvailability", availability);
            var toggle = Read<Toggle>(panel, "_pause");
            RefineCheck(toggle, skin.Metrics);
            var safe = (RectTransform)Read<GameObject>(chrome, "_menu").transform.parent;
            CreateError(chrome, safe, skin, caption.font);
            var overlays = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPath), root.transform);
            Stretch((RectTransform)overlays.transform);
            Bind(root.AddComponent<LevelOverlayAnchor>(), "_view", overlays.GetComponent<LevelOverlayView>());
        }

        private static void CreateError(StationSceneChrome chrome, RectTransform safe, StationUiSkin skin, Font font)
        {
            var page = Rect("StationBlockingError", safe);
            page.anchorMin = page.anchorMax = page.pivot = Vector2.one * .5f;
            page.anchoredPosition = Vector2.zero;
            skin.Column(page, (int)skin.Metrics.margin);
            skin.Paper(page);
            var title = Rect("ErrorTitle", page).gameObject.AddComponent<Text>();
            title.font = font; skin.Text(title, skin.Metrics.titleSize);
            title.color = skin.Metrics.errorInk;
            Element(title.transform, 0).preferredHeight = -1;
            var viewport = Rect("ErrorBodyViewport", page);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var content = Rect("ErrorContent", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            skin.Column(content, (int)skin.Metrics.innerGap);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var message = Rect("ErrorMessage", content).gameObject.AddComponent<Text>();
            message.font = font; skin.Text(message, skin.Metrics.bodySize);
            Element(message.transform, 0).preferredHeight = -1;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var layout = page.gameObject.AddComponent<PopupContentLayout>();
            Bind(layout, "_metrics", skin.Metrics); Bind(layout, "_page", page);
            Bind(layout, "_viewport", viewport); Bind(layout, "_content", content);
            Bind(layout, "_scroll", scroll); Bind(layout, "_bodySize", Element(viewport, 0));
            Bind(chrome, "_blockingError", page.gameObject);
            Bind(chrome, "_errorTitle", title); Bind(chrome, "_errorMessage", message);
            page.gameObject.SetActive(false);
        }

        private static void RefineCheck(Toggle toggle, UiLayoutMetrics metrics)
        {
            var check = (Image)toggle.graphic;
            check.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/T_UI_LevelControls.png")
                .OfType<Sprite>().Single(sprite => sprite.name == "Paw");
            check.color = Color.white; check.preserveAspect = true;
            // The shared paw is the explicit checked marker; Toggle owns its alpha and fade.
        }

        private static void StyleState(Selectable control, Image image, UiLayoutMetrics metrics)
        {
            var colors = control.colors;
            colors.normalColor = colors.disabledColor = Color.white;
            colors.highlightedColor = colors.selectedColor = metrics.buttonHover;
            colors.pressedColor = metrics.buttonPressed;
            control.colors = colors;
            var state = control.GetComponent<PaperButtonState>() ?? control.gameObject.AddComponent<PaperButtonState>();
            Bind(state, "_control", control); Bind(state, "_surface", image); Bind(state, "_metrics", metrics);
            foreach (var text in control.GetComponentsInChildren<Text>(true)) text.color = metrics.ink;
        }
    }
}
