using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.PetMotion
{
    internal static class PetMotionPanelAuthor
    {
        private static readonly Color Ink = new Color32(40, 56, 50, 255);
        private static readonly Color Mint = new Color32(77, 150, 121, 255);
        private static Dictionary<string, string> _text;

        [Serializable] private sealed class Copy { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string key, value; }

        internal static void CreatePanel()
        {
            _text = new Dictionary<string, string>();
            var copy = AssetDatabase.LoadAssetAtPath<TextAsset>(PetMotionAssets.Folder + "/PetMotionText.json");
            foreach (var entry in JsonUtility.FromJson<Copy>(copy.text).entries)
                _text.Add(entry.key, entry.value);
            var root = new GameObject("Panel_PetMotion");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PetMotionAssets.RigPath), root.transform);
            var player = rig.GetComponent<PetMotionPlayer>();
            var canvasNode = Rect("Canvas", root.transform);
            canvasNode.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasNode.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            canvasNode.AddComponent<GraphicRaycaster>();
            canvasNode.AddComponent<Image>().color = Color.white;
            var safe = Rect("SafeArea", canvasNode.transform);
            Stretch((RectTransform)safe.transform);
            safe.AddComponent<SafeAreaView>();
            var layout = safe.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 32, 40);
            layout.spacing = 18;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Label("Title", safe.transform, _text["title"], 44, 80);
            var stage = Rect("Stage", safe.transform);
            var le = stage.AddComponent<LayoutElement>();
            le.minHeight = 200;
            le.flexibleHeight = 1;
            var image = stage.AddComponent<RawImage>();
            image.raycastTarget = false;
            var controls = canvasNode.AddComponent<PetMotionControls>();
            PetMotionAssets.Bind(controls, "_player", player);
            var surface = stage.AddComponent<PetMotionSurface>();
            PetMotionAssets.Bind(surface, "_surface", image);
            PetMotionAssets.Bind(surface, "_player", player);
            PetMotionAssets.Bind(surface, "_camera", rig.GetComponentInChildren<Camera>(true));
            var row = Row("ActionRow", safe.transform);
            Label("ActionLabel", row.transform, _text["action"], 32, 88, 170);
            var dropdownNode = DefaultControls.CreateDropdown(ControlResources());
            dropdownNode.name = "Action";
            dropdownNode.transform.SetParent(row.transform, false);
            Flexible(dropdownNode, 96);
            var dropdown = dropdownNode.GetComponent<Dropdown>();
            dropdown.ClearOptions();
            var options = new List<string>();
            foreach (string id in new[] { "idle", "look", "tread", "walk", "sit", "side" })
                options.Add(_text[id]);
            dropdown.AddOptions(options);
            StyleDropdown(dropdown);
            PetMotionAssets.Bind(controls, "_action", dropdown);
            PetMotionAssets.Bind(controls, "_speed", SliderRow("Speed", safe.transform, "speed", .5f, 1.5f, 1));
            PetMotionAssets.Bind(controls, "_progress", SliderRow("Progress", safe.transform, "progress", 0, 1, 0));
            var tools = Row("Tools", safe.transform);
            PetMotionAssets.Bind(controls, "_playing", Toggle("Playing", tools.transform, "playing", true));
            PetMotionAssets.Bind(controls, "_source", Toggle("Source", tools.transform, "source", false));
            var resetNode = DefaultControls.CreateButton(ControlResources());
            resetNode.name = "Reset";
            resetNode.transform.SetParent(tools.transform, false);
            Flexible(resetNode, 96);
            resetNode.GetComponentInChildren<Text>().text = _text["reset"];
            resetNode.GetComponent<Image>().color = new Color32(242, 216, 210, 255);
            PetMotionAssets.Bind(controls, "_reset", resetNode.GetComponent<Button>());
            foreach (var label in root.GetComponentsInChildren<Text>(true))
            {
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.color = Ink;
                label.fontSize = label.name == "Title" ? 44 : 32;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 22;
                label.resizeTextMaxSize = label.fontSize;
            }
            PrefabUtility.SaveAsPrefabAsset(root, PetMotionAssets.PanelPath);
            UnityEngine.Object.DestroyImmediate(root);
            _text = null;
        }

        private static Slider SliderRow(string name, Transform parent, string key, float min, float max, float value)
        {
            var row = Row(name + "Row", parent);
            Label(name + "Label", row.transform, _text[key], 32, 88, 170);
            var node = DefaultControls.CreateSlider(ControlResources());
            node.name = name;
            node.transform.SetParent(row.transform, false);
            Flexible(node, 96);
            var slider = node.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);
            slider.fillRect.GetComponent<Image>().color = Mint;
            slider.handleRect.GetComponent<Image>().color = Mint;
            StyleSlider(slider);
            return slider;
        }

        private static Toggle Toggle(string name, Transform parent, string key, bool value)
        {
            var node = DefaultControls.CreateToggle(ControlResources());
            node.name = name;
            node.transform.SetParent(parent, false);
            Flexible(node, 96);
            var toggle = node.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(value);
            toggle.GetComponentInChildren<Text>().text = _text[key];
            var background = (RectTransform)toggle.targetGraphic.transform;
            background.sizeDelta = new Vector2(40, 40);
            background.anchorMin = background.anchorMax = new Vector2(0, .5f);
            background.anchoredPosition = new Vector2(24, 0);
            toggle.graphic.color = Mint;
            var label = toggle.GetComponentInChildren<Text>().rectTransform;
            label.offsetMin = new Vector2(56, 0);
            label.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;
            return toggle;
        }

        private static GameObject Row(string name, Transform parent)
        {
            var row = Rect(name, parent);
            var group = row.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 20;
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            var layout = row.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 96;
            layout.flexibleHeight = 0;
            return row;
        }

        private static Text Label(string name, Transform parent, string value, int size, float height, float width = 0)
        {
            var node = Rect(name, parent);
            var text = node.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            var layout = node.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            if (width > 0) layout.minWidth = layout.preferredWidth = width;
            return text;
        }

        private static void Flexible(GameObject node, float height)
        {
            var layout = node.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleWidth = 1;
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return node;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        internal static void RefineSavedPanel()
        {
            var root = PrefabUtility.LoadPrefabContents(PetMotionAssets.PanelPath);
            try
            {
                foreach (var row in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                    row.GetComponent<LayoutElement>().flexibleHeight = 0;
                foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                    StyleSlider(slider);
                var resources = ControlResources();
                var dropdown = root.GetComponentInChildren<Dropdown>(true);
                StyleDropdown(dropdown);
                dropdown.targetGraphic.GetComponent<Image>().sprite = resources.standard;
                foreach (var graphic in dropdown.GetComponentsInChildren<Image>(true))
                    if (graphic.name == "Arrow") graphic.sprite = resources.dropdown;
                foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
                {
                    if (toggle.transform.IsChildOf(dropdown.template))
                        continue;
                    toggle.targetGraphic.GetComponent<Image>().sprite = resources.standard;
                    toggle.graphic.GetComponent<Image>().sprite = resources.checkmark;
                    toggle.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PetMotionAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void StyleSlider(Slider slider)
        {
            foreach (var image in slider.GetComponentsInChildren<Image>(true))
                if (image.name == "Background")
                {
                    image.rectTransform.anchorMin = new Vector2(0, .5f);
                    image.rectTransform.anchorMax = new Vector2(1, .5f);
                    image.rectTransform.sizeDelta = new Vector2(-20, 8);
                }
            var fillArea = (RectTransform)slider.fillRect.parent;
            fillArea.anchorMin = new Vector2(0, .5f);
            fillArea.anchorMax = new Vector2(1, .5f);
            fillArea.sizeDelta = new Vector2(-20, 8);
            slider.handleRect.anchorMin = slider.handleRect.anchorMax = new Vector2(0, .5f);
            var handleArea = (RectTransform)slider.handleRect.parent;
            handleArea.anchorMin = new Vector2(0, .5f);
            handleArea.anchorMax = new Vector2(1, .5f);
            handleArea.sizeDelta = new Vector2(-20, 0);
            slider.handleRect.sizeDelta = new Vector2(32, 32);
            slider.handleRect.GetComponent<Image>().sprite = ControlResources().knob;
        }

        private static void StyleDropdown(Dropdown dropdown)
        {
            var item = dropdown.template.GetComponentInChildren<Toggle>(true);
            var background = item.targetGraphic.GetComponent<Image>();
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = Color.white;
            var rect = (RectTransform)item.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 88);
            var content = (RectTransform)rect.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 88);
            dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 560);
        }

        private static DefaultControls.Resources ControlResources()
        {
            return new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
        }
    }
}
