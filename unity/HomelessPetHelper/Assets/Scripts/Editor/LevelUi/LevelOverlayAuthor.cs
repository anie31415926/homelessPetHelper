using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>One-time additive Prefab Mode edit in an isolated snapshot; never recreates existing panels.</summary>
    public static class LevelOverlayAuthor
    {
        private const string WidgetPath = "Assets/Prefabs/Widgets/Widget_LevelOverlays.prefab";
        private static readonly List<Text> Labels = new List<Text>();
        private static readonly List<string> Keys = new List<string>();
        private static LevelOverlayView _view;

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LevelUiAssets.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WidgetPath) != null)
                throw new InvalidOperationException("Overlay widget already exists. Edit its saved prefab.");
            CreateWidget();
            Upgrade("Assets/Prefabs/Panels/Panel_Approach.prefab", true);
            Upgrade("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", false);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            Bind(content, "_levelUiText", AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/LevelUiText.json"));
            Bind(content, "_levelGuidance", AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Settings/Game/LevelGuidance.json"));
            content.Validate();
            AssetDatabase.SaveAssets();
        }

        private static void CreateWidget()
        {
            Labels.Clear(); Keys.Clear();
            var root = Rect("LevelOverlays", null);
            try
            {
                _view = root.gameObject.AddComponent<LevelOverlayView>();
                var mask = Rect("GuideMask", root).gameObject.AddComponent<GuideMaskGraphic>();
                mask.color = new Color32(20, 31, 25, 165);
                Bind(_view, "_guideMask", mask);
                var caption = Rect("GuideCaption", root, .08f, .22f, .92f, .33f);
                Surface(caption, new Color32(252, 248, 231, 255));
                Bind(_view, "_guideCaption", caption.gameObject);
                Bind(_view, "_guideText", Text("GuideText", caption, .04f, .06f, .96f, .94f, 36));
                var modal = Rect("Modal", root);
                Surface(modal, new Color32(22, 34, 27, 180), true);
                Bind(_view, "_modalRoot", modal.gameObject);
                Bind(_view, "_modalInput", modal.gameObject.AddComponent<CanvasGroup>());
                Pause(modal); Settings(modal); Objectives(modal); Confirmation(modal);
                var notice = Text("OverlayNotice", root, .08f, .34f, .92f, .44f, 32);
                var outline = notice.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(253, 249, 230, 255);
                outline.effectDistance = new Vector2(2, -2);
                Bind(_view, "_notice", notice);
                BindLabels();
                _view.Close();
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, WidgetPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); _view = null; }
        }

        private static RectTransform Page(string name, Transform parent, string field, string title)
        {
            var page = Rect(name, parent, .06f, .12f, .94f, .86f);
            Surface(page, new Color32(247, 243, 225, 255));
            Bind(_view, field, page.gameObject);
            LocalText("Title", page, .07f, .88f, .93f, .97f, title, 44);
            return page;
        }

        private static void Pause(Transform modal)
        {
            var page = Page("PausePage", modal, "_pausePage", "pause.title");
            ButtonField("OverlayResume", page, .12f, .64f, .88f, .76f, "_resume", "pause.resume");
            ButtonField("OverlaySettings", page, .12f, .46f, .88f, .58f, "_settings", "pause.settings");
            ButtonField("OverlayReturn", page, .12f, .22f, .88f, .34f, "_return", "pause.return");
        }

        private static void Objectives(Transform modal)
        {
            var page = Page("ObjectivesPage", modal, "_objectivesPage", "objectives.title");
            var description = Text("ObjectiveList", page, .08f, .33f, .92f, .85f, 36);
            description.alignment = TextAnchor.UpperLeft;
            description.lineSpacing = 1.12f;
            Bind(_view, "_objectivesText", description);
            ButtonField("OverlayReplay", page, .08f, .18f, .92f, .29f, "_replay", "objectives.replay");
            ButtonField("OverlayCloseObjectives", page, .08f, .04f, .92f, .15f,
                "_closeObjectives", "objectives.close");
        }

        private static void Confirmation(Transform modal)
        {
            var page = Page("ConfirmationPage", modal, "_confirmationPage", "confirm.title");
            Bind(_view, "_confirmationText", Text("ConfirmationText", page, .08f, .31f, .92f, .83f, 38));
            ButtonField("OverlayCancel", page, .08f, .09f, .47f, .23f, "_cancel", "confirm.no");
            ButtonField("OverlayConfirm", page, .53f, .09f, .92f, .23f, "_confirm", "confirm.yes");
        }

        private static void Settings(Transform modal)
        {
            var page = Page("SettingsPage", modal, "_settingsPage", "settings.title");
            var audio = page.gameObject.AddComponent<AudioSettingsView>();
            AudioRow(audio, page, "Master", "_master", "_masterMute", "settings.master", .76f);
            AudioRow(audio, page, "Music", "_music", "_musicMute", "settings.music", .61f);
            AudioRow(audio, page, "Effects", "_effects", "_effectsMute", "settings.effects", .46f);
            LocalText("AudioNote", page, .08f, .32f, .92f, .39f, "settings.note", 28);
            Bind(_view, "_audio", audio);
            Bind(_view, "_settingsStatus", Text("SettingsStatus", page, .08f, .24f, .92f, .32f, 28));
            ButtonField("OverlayResetAudio", page, .08f, .15f, .92f, .23f,
                "_resetSettings", "settings.reset");
            ButtonField("OverlayCancelSettings", page, .08f, .03f, .47f, .12f,
                "_cancelSettings", "settings.cancel");
            ButtonField("OverlaySaveSettings", page, .53f, .03f, .92f, .12f,
                "_saveSettings", "settings.save");
        }

        private static void AudioRow(AudioSettingsView owner, Transform page, string name, string volumeField,
            string muteField, string labelKey, float y)
        {
            LocalText(name + "Label", page, .08f, y, .65f, y + .065f, labelKey, 32);
            var rect = Rect(name + "Volume", page, .08f, y - .065f, .65f, y - .005f);
            var background = Surface(rect, new Color32(202, 211, 192, 255), true);
            var fill = Rect("Fill", rect);
            Surface(fill, new Color32(143, 171, 109, 255));
            var handleArea = Rect("HandleArea", rect, .02f, .1f, .98f, .9f);
            var handle = Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(28, 0);
            var handleImage = Surface(handle, new Color32(91, 121, 87, 255));
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = background;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            Bind(owner, volumeField, slider);
            var toggleRect = Rect(name + "Mute", page, .71f, y - .07f, .93f, y + .06f);
            var toggle = toggleRect.gameObject.AddComponent<Toggle>();
            var baseImage = Surface(toggleRect, new Color32(224, 230, 209, 255), true);
            var mark = Rect("Mark", toggleRect, .03f, .15f, .22f, .85f);
            toggle.graphic = Surface(mark, new Color32(83, 124, 80, 255));
            toggle.targetGraphic = baseImage;
            LocalText("MuteLabel", toggleRect, .24f, .05f, .98f, .95f, "settings.mute", 28);
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            toggle.isOn = false;
            Bind(owner, muteField, toggle);
        }

        private static void ButtonField(string name, Transform parent, float x0, float y0, float x1, float y1,
            string field, string key)
        {
            var button = Button(name, parent, x0, y0, x1, y1);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Bind(_view, field, button);
            Labels.Add(button.GetComponentInChildren<Text>()); Keys.Add(key);
        }

        private static void LocalText(string name, Transform parent, float x0, float y0, float x1, float y1,
            string key, int size)
        { Labels.Add(Text(name, parent, x0, y0, x1, y1, size)); Keys.Add(key); }

        private static void BindLabels()
        {
            var serialized = new SerializedObject(_view);
            var labels = serialized.FindProperty("_localizedLabels");
            var keys = serialized.FindProperty("_labelKeys");
            labels.arraySize = keys.arraySize = Labels.Count;
            for (int index = 0; index < Labels.Count; index++)
            {
                labels.GetArrayElementAtIndex(index).objectReferenceValue = Labels[index];
                keys.GetArrayElementAtIndex(index).stringValue = Keys[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Upgrade(string path, bool approach)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var safe = root.GetComponentInChildren<SafeAreaView>(true).transform;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(WidgetPath), safe);
                Place((RectTransform)instance.transform, 0, 0, 1, 1);
                var overlay = instance.GetComponent<LevelOverlayView>();
                var owner = approach ? (UnityEngine.Object)root.GetComponent<ApproachPanelView>() :
                    root.GetComponent<SiegeTrialView>().GetComponentInChildren<SiegeFieldView>(true);
                var target = approach ? safe : ((SiegeFieldView)owner).transform;
                var objectives = Button("FieldObjectives", target, .72f, .82f, .97f, .875f);
                objectives.GetComponentInChildren<Text>().text = "";
                Bind(owner, "_objectives", objectives);
                if (approach) Bind(owner, "_overlays", overlay);
                else Bind(root.AddComponent<LevelOverlayAnchor>(), "_view", overlay);
                instance.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
