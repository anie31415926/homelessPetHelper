using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.StationPreview
{
    internal static class StationPreviewLayout
    {
        internal static readonly Color Ink = new Color32(32, 36, 40, 255);
        internal static readonly Color Surface = new Color32(242, 243, 244, 255);

        internal static GameObject Rect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return root;
        }

        internal static GameObject Column(string name, Transform parent, bool flexible = false)
        {
            GameObject root = Rect(name, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.flexibleHeight = flexible ? 1 : 0;
            var group = root.AddComponent<VerticalLayoutGroup>();
            group.spacing = 20;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return root;
        }

        internal static GameObject Row(string name, Transform parent, float height)
        {
            GameObject root = Rect(name, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            var group = root.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 24;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            return root;
        }

        internal static Text Label(string name, Transform parent, string value, int size, float height)
        {
            GameObject root = Rect(name, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;
            var text = root.AddComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 26;
            text.resizeTextMaxSize = size;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        internal static Button Button(string name, Transform parent, string value, bool primary = false)
        {
            GameObject root = Rect(name, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = 104;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;
            var image = root.AddComponent<Image>();
            image.color = primary ? new Color32(220, 239, 228, 255) : Surface;
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.65f, 0.65f, 0.65f);
            button.colors = colors;
            Text label = Label("Label", root.transform, value, 36, 104);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch((RectTransform)label.transform, 12);
            return button;
        }

        internal static Toggle Pause(Transform parent, string value)
        {
            GameObject root = Row("Pause", parent, 88);
            root.AddComponent<Image>().color = Surface;
            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = root.GetComponent<Image>();
            GameObject box = Rect("Indicator", root.transform);
            var layout = box.AddComponent<LayoutElement>();
            layout.preferredWidth = 40;
            layout.flexibleWidth = 0;
            layout.preferredHeight = 40;
            toggle.graphic = box.AddComponent<Image>();
            toggle.graphic.color = Ink;
            Label("Label", root.transform, value, 34, 88);
            var group = root.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.padding = new RectOffset(20, 16, 0, 0);
            return toggle;
        }

        internal static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        internal static void Bind(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
