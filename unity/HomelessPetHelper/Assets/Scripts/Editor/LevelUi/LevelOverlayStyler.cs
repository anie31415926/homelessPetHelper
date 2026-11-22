using System;
using System.Linq;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;
using static HomelessPetHelper.Editor.LevelUi.LevelOverlaySkin;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Single incremental edit of the saved widget and controls; preserves every business binding.</summary>
    public static class LevelOverlayStyler
    {
        private const string WidgetPath = "Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab";
        private const string ApproachPath = "Assets/Prefabs/Panels/Panel_Approach.prefab";
        private const string SiegePath = "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab";

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LevelUiStyle.");
            var skin = new LevelOverlaySkin();
            Edit(WidgetPath, root => Widget(root.GetComponent<LevelOverlayView>(), skin));
            Edit(ApproachPath, root => Approach(root.GetComponent<ApproachPanelView>(), skin));
            Edit(SiegePath, root => Siege(root.GetComponentInChildren<SiegeFieldView>(true), skin));
            AssetDatabase.SaveAssets();
        }

        public static void RefineTypography()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated saved snapshot.");
            Edit(WidgetPath, root =>
            {
                var audio = root.GetComponentInChildren<AudioSettingsView>(true);
                foreach (string name in new[] { "Master", "Music", "Effects" })
                {
                    var row = audio.GetComponentsInChildren<RectTransform>(true)
                        .Single(rect => rect.name == name + "Row");
                    Place(row.GetComponentsInChildren<Text>(true)
                        .Single(text => text.name == name + "Label").rectTransform, 0, .63f, .68f, 1);
                    Place((RectTransform)row.GetComponentInChildren<Slider>(true).transform, 0, 0, .68f, .63f);
                    RefineSlider(row.GetComponentInChildren<Slider>(true));
                }
            });
            AssetDatabase.SaveAssets();
        }

        private static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Widget(LevelOverlayView view, LevelOverlaySkin skin)
        {
            if (Ref<GameObject>(view, "_pausePage").GetComponent<ContentSizeFitter>() != null)
                throw new InvalidOperationException("Already styled; edit the saved prefab rather than rebuilding it.");
            Ref<GameObject>(view, "_modalRoot").GetComponent<Image>().color = new Color32(38, 38, 28, 135);
            var pause = Page(view, "_pausePage", skin, "Pause");
            MoveButton(view, "_resume", pause, skin, true, "Play");
            MoveButton(view, "_settings", pause, skin, false, "Settings");
            MoveButton(view, "_return", pause, skin, false, "Home");
            Settings(view, Page(view, "_settingsPage", skin, "Settings"), skin);
            var objectives = Page(view, "_objectivesPage", skin, "Objectives");
            Body(Ref<Text>(view, "_objectivesText"), objectives, 38);
            MoveButton(view, "_replay", objectives, skin, false);
            MoveButton(view, "_closeObjectives", objectives, skin, true);
            var confirmation = Page(view, "_confirmationPage", skin, "Paw");
            Body(Ref<Text>(view, "_confirmationText"), confirmation, 38);
            Footer(view, confirmation, skin, "_cancel", "_confirm");
            var caption = (RectTransform)Ref<GameObject>(view, "_guideCaption").transform;
            caption.anchorMin = new Vector2(.075f, .22f); caption.anchorMax = new Vector2(.925f, .22f);
            caption.pivot = new Vector2(.5f, 0); caption.anchoredPosition = Vector2.zero;
            caption.sizeDelta = Vector2.zero;
            skin.Surface(caption.GetComponent<Image>(), "Paper");
            Content(caption, 32, 24).spacing = 0;
            Body(Ref<Text>(view, "_guideText"), caption, 38, 64);
            foreach (var text in view.GetComponentsInChildren<Text>(true)) text.raycastTarget = false;
        }

        private static RectTransform Page(LevelOverlayView view, string field, LevelOverlaySkin skin, string icon)
        {
            var page = (RectTransform)Ref<GameObject>(view, field).transform;
            var title = page.GetComponentsInChildren<Text>(true).Single(text => text.name == "Title");
            page.anchorMin = new Vector2(.08f, .5f); page.anchorMax = new Vector2(.92f, .5f);
            page.pivot = new Vector2(.5f, .5f); page.anchoredPosition = Vector2.zero;
            page.sizeDelta = Vector2.zero;
            skin.Surface(page.GetComponent<Image>(), "Paper", true);
            Content(page, 44, 36);
            var header = Rect("CardHeader", page);
            header.SetAsFirstSibling();
            Element(header, 80);
            var layout = Horizontal(header, 18); layout.childForceExpandWidth = false;
            var symbol = skin.Icon(header, icon, LevelOverlaySkin.Ink);
            Element(symbol.transform, 60).preferredWidth = 60;
            title.transform.SetParent(header, false);
            LevelOverlaySkin.Text(title, 52, TextAnchor.MiddleCenter);
            return page;
        }

        private static void Body(Text text, Transform parent, int size, float minHeight = 96)
        {
            text.transform.SetParent(parent, false); text.transform.SetAsLastSibling();
            LevelOverlaySkin.Text(text, size, TextAnchor.MiddleLeft);
            var element = Element(text.transform, 0);
            element.minHeight = minHeight;
        }

        private static void MoveButton(LevelOverlayView view, string field, Transform parent,
            LevelOverlaySkin skin, bool primary, string icon = null)
        {
            var button = Ref<Button>(view, field);
            button.transform.SetParent(parent, false); button.transform.SetAsLastSibling();
            skin.Button(button, primary, icon);
        }

        private static void Footer(LevelOverlayView view, Transform parent, LevelOverlaySkin skin,
            string cancel, string confirm)
        {
            var footer = Rect("CardActions", parent);
            Element(footer, 116); Horizontal(footer, 20);
            MoveButton(view, cancel, footer, skin, false);
            MoveButton(view, confirm, footer, skin, true);
        }

        private static void Settings(LevelOverlayView view, RectTransform page, LevelOverlaySkin skin)
        {
            var audio = Ref<AudioSettingsView>(view, "_audio");
            AudioRow(audio, page, skin, "Master", "_master", "_masterMute");
            AudioRow(audio, page, skin, "Music", "_music", "_musicMute");
            AudioRow(audio, page, skin, "Effects", "_effects", "_effectsMute");
            var note = page.GetComponentsInChildren<Text>(true).Single(text => text.name == "AudioNote");
            Body(note, page, 32, 48);
            var status = Ref<Text>(view, "_settingsStatus");
            Body(status, page, 32, 0); status.color = new Color32(142, 77, 48, 255);
            status.gameObject.SetActive(false);
            MoveButton(view, "_resetSettings", page, skin, false);
            Footer(view, page, skin, "_cancelSettings", "_saveSettings");
        }

        private static void AudioRow(AudioSettingsView audio, Transform page, LevelOverlaySkin skin,
            string name, string volume, string mute)
        {
            var label = page.GetComponentsInChildren<Text>(true).Single(text => text.name == name + "Label");
            var slider = Ref<Slider>(audio, volume);
            var toggle = Ref<Toggle>(audio, mute);
            var row = Rect(name + "Row", page); Element(row, 144);
            label.transform.SetParent(row, false); slider.transform.SetParent(row, false);
            toggle.transform.SetParent(row, false);
            Place(label.rectTransform, 0, .63f, .68f, 1);
            LevelOverlaySkin.Text(label, 36, TextAnchor.MiddleLeft);
            Place((RectTransform)slider.transform, 0, 0, .68f, .63f);
            slider.GetComponent<Image>().color = Color.clear;
            var track = Rect("SkinTrack", slider.transform, 0, .38f, 1, .62f);
            var trackImage = track.gameObject.AddComponent<Image>();
            skin.Surface(trackImage, "Secondary"); track.SetAsFirstSibling(); slider.targetGraphic = trackImage;
            var fillArea = Rect("SkinFillArea", slider.transform, 0, .38f, 1, .62f);
            fillArea.SetSiblingIndex(1);
            slider.fillRect.SetParent(fillArea, false);
            Place(slider.fillRect, 0, 0, 1, 1);
            skin.Surface(slider.fillRect.GetComponent<Image>(), "Action");
            var handleArea = (RectTransform)slider.handleRect.parent;
            Place(handleArea, .025f, .5f, .975f, .5f); handleArea.sizeDelta = new Vector2(0, 64);
            slider.handleRect.anchorMin = new Vector2(0, 0);
            slider.handleRect.anchorMax = new Vector2(0, 1);
            slider.handleRect.sizeDelta = new Vector2(64, 0);
            skin.Surface(slider.handleRect.GetComponent<Image>(), "Paper");
            Place((RectTransform)toggle.transform, .73f, .1f, 1, .84f);
            skin.Surface(toggle.GetComponent<Image>(), "Secondary", true);
            Place(toggle.graphic.rectTransform, .075f, .3f, .255f, .7f);
            skin.Surface((Image)toggle.graphic, "Action");
            var muteLabel = toggle.GetComponentInChildren<Text>(true);
            Place(muteLabel.rectTransform, .30f, .05f, .96f, .95f);
            LevelOverlaySkin.Text(muteLabel, 32, TextAnchor.MiddleCenter);
        }

        private static void RefineSlider(Slider slider)
        {
            if (slider.fillRect.parent == slider.transform)
            {
                var area = Rect("SkinFillArea", slider.transform, 0, .38f, 1, .62f);
                area.SetSiblingIndex(1);
                slider.fillRect.SetParent(area, false);
                Place(slider.fillRect, 0, 0, 1, 1);
            }
            slider.handleRect.anchorMin = new Vector2(0, 0);
            slider.handleRect.anchorMax = new Vector2(0, 1);
            slider.handleRect.sizeDelta = new Vector2(64, 0);
        }

        private static void Approach(ApproachPanelView view, LevelOverlaySkin skin)
        {
            foreach (string field in new[] { "_move", "_bridge", "_litter", "_weeds", "_mud", "_undo",
                "_continue", "_pause", "_return" })
                skin.Button(Ref<ApproachCommandView>(view, field).GetComponent<Button>(), field == "_continue",
                    field == "_pause" ? "Pause" : null, 36);
            skin.Button(Ref<Button>(view, "_objectives"), false, "Objectives", 36);
        }

        private static void Siege(SiegeFieldView view, LevelOverlaySkin skin)
        {
            foreach (string field in new[] { "_submit", "_pause", "_merge", "_reroute", "_clear", "_discard" })
                skin.Button(Ref<Button>(view, field), field == "_submit",
                    field == "_pause" ? "Pause" : null, 36);
            skin.Button(Ref<Button>(view, "_objectives"), false, "Objectives", 36);
        }
    }
}
