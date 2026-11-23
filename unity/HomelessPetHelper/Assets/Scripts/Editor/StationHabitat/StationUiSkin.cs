using System.Linq;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.StationHabitat
{
    internal sealed class StationUiSkin
    {
        internal readonly UiLayoutMetrics Metrics = AssetDatabase.LoadAssetAtPath<UiLayoutMetrics>(
            "Assets/Settings/UI/LayoutMetrics.asset");
        private readonly System.Collections.Generic.Dictionary<string, Sprite> _sprites =
            AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/T_UI_LevelControls.png")
                .OfType<Sprite>().ToDictionary(sprite => sprite.name);
        internal MotionScope Scope;

        internal void Surface(Image image, string sprite, bool hit = false)
        {
            image.sprite = _sprites[sprite]; image.type = Image.Type.Sliced;
            image.color = Color.white; image.raycastTarget = hit;
        }
        internal void Text(Text text, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            text.fontSize = size; text.resizeTextForBestFit = false; text.color = Metrics.ink;
            text.alignment = alignment; text.lineSpacing = 1.25f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false;
        }
        internal void Button(Button button, bool primary = false, string icon = null, string cue = "ui.tap")
        {
            var rect = (RectTransform)button.transform;
            var hit = button.GetComponent<Image>();
            if (hit == null) hit = button.gameObject.AddComponent<Image>();
            var visual = Rect("MotionVisual", rect);
            visual.offsetMin = Vector2.one * Metrics.innerGap;
            visual.offsetMax = -Vector2.one * Metrics.innerGap;
            var image = visual.gameObject.AddComponent<Image>();
            Surface(image, primary ? "Action" : "Secondary");
            foreach (var label in button.GetComponentsInChildren<Text>(true))
            {
                label.transform.SetParent(visual, false);
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(Metrics.innerGap * 2, Metrics.innerGap);
                label.rectTransform.offsetMax = -label.rectTransform.offsetMin;
                Text(label, Metrics.bodySize, TextAnchor.MiddleCenter);
                if (icon != null) label.gameObject.SetActive(false);
            }
            if (icon != null)
            {
                var graphic = Rect("SharedIcon", visual).gameObject.AddComponent<Image>();
                graphic.sprite = _sprites[icon]; graphic.preserveAspect = true; graphic.raycastTarget = false;
                graphic.color = Metrics.ink;
                graphic.rectTransform.anchorMin = graphic.rectTransform.anchorMax = Vector2.one * .5f;
                graphic.rectTransform.sizeDelta = Vector2.one * 72;
            }
            hit.sprite = null; hit.type = Image.Type.Simple; hit.color = Color.clear; hit.raycastTarget = true;
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(249, 249, 242, 255);
            colors.pressedColor = new Color32(211, 228, 214, 255);
            colors.disabledColor = new Color32(188, 196, 190, 255);
            button.colors = colors;
            var audio = button.GetComponent<ButtonAudio>();
            if (audio == null) audio = button.gameObject.AddComponent<ButtonAudio>();
            audio.CueId = cue;
            var motion = button.gameObject.AddComponent<UiMotionFeedback>();
            Bind(motion, "_scope", Scope); Bind(motion, "_control", button); Bind(motion, "_visual", visual);
            var element = Element(rect, Metrics.touchSize);
            element.minWidth = Metrics.touchSize;
        }
        internal void Paper(RectTransform target)
        {
            var backing = Rect("PopupPaper", target);
            backing.SetAsFirstSibling();
            var element = backing.gameObject.AddComponent<LayoutElement>(); element.ignoreLayout = true;
            element.minHeight = element.preferredHeight = 0;
            Surface(backing.gameObject.AddComponent<Image>(), "Paper");
            var image = target.GetComponent<Image>();
            if (image == null) image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear; image.raycastTarget = true;
            var motion = target.gameObject.AddComponent<
                HomelessPetHelper.Modules.Station.UI.Habitat.StationPopupPaper>();
            Bind(motion, "_scope", Scope); Bind(motion, "_paper", backing);
        }
        internal VerticalLayoutGroup Column(RectTransform target, int padding)
        {
            var layout = target.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding); layout.spacing = Metrics.gap;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            return layout;
        }
        internal static LayoutElement Element(Transform target, float height)
        {
            var element = target.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height; element.preferredHeight = height; element.flexibleHeight = 0;
            return element;
        }
        internal static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Stretch(rect); return rect;
        }
        internal static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero; target.anchorMax = Vector2.one;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }
        internal static T Read<T>(Object owner, string field) where T : Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        internal static void Bind(Object owner, string field, Object value) =>
            MainSceneAuthor.Bind(owner, field, value);
        internal static void Remove<T>(Component target) where T : Component
        { var component = target.GetComponent<T>(); if (component != null) Object.DestroyImmediate(component); }
    }
}
