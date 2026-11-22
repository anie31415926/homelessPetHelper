using HomelessPetHelper.Game.Field;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Field
{
    /// <summary>Editor-only helpers for this incremental, saved-prefab upgrade.</summary>
    internal static class FieldAuthorUi
    {
        internal static readonly Color Ink = new Color32(56, 77, 65, 255);
        internal static Font Font => AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
        internal static RectTransform Rect(string name, Transform parent, float x0 = 0, float y0 = 0, float x1 = 1,
            float y1 = 1)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Place(rect, x0, y0, x1, y1); return rect;
        }
        internal static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        internal static Image Surface(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast;
            return image;
        }
        internal static Text Text(string name, Transform parent, float x0, float y0, float x1, float y1, int size = 32)
        {
            var label = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Text>();
            label.font = Font; label.fontSize = label.resizeTextMaxSize = size; label.resizeTextMinSize = 24;
            label.resizeTextForBestFit = true; label.color = Ink; label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false; label.text = string.Empty;
            return label;
        }
        internal static Button Button(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = Surface(rect, new Color32(225, 233, 211, 250), true);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.disabledColor = new Color(.72f, .75f, .69f, .65f);
                button.colors = colors;
            Text("Label", rect, .03f, .06f, .97f, .94f, 32); return button;
        }
        internal static Slider Bar(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1); Surface(rect, new Color32(205, 211, 190, 255));
            var fill = Rect("Fill", rect); var image = Surface(fill, color);
            var slider = rect.gameObject.AddComponent<Slider>(); slider.interactable = false;
            slider.transition = Selectable.Transition.None; slider.fillRect = fill; slider.targetGraphic = image;
            return slider;
        }
        internal static T Ref<T>(Object owner, string field) where T : Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        internal static void Bind(Object owner, string field, Object value) => MainSceneAuthor.Bind(owner,
            field, value);

        internal static OutingFieldView Scene(GameObject owner, Transform parent, out RectTransform frame,
            out RectTransform grid, out Image animal)
        {
            var area = Rect("FieldScene", parent, 0, .17f, 1, .86f);
            frame = Rect("SharedYardFrame", area);
            var ratio = frame.gameObject.AddComponent<AspectRatioFitter>();
            ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent; ratio.aspectRatio = .75f;
            var background = Surface(Rect("Backdrop", frame), Color.white);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Outing/T_Outing_Yard.jpg");
            var terrain = Rect("PhysicalProps", frame).gameObject.AddComponent<OutingFieldGraphic>();
            terrain.raycastTarget = false;
            var palette = new SerializedObject(terrain);
            foreach (var item in new[] { ("_stone",new Color32(208,201,167,150)), ("_soil",new Color32(128,132,94,80)),
                ("_leaves",new Color32(143,171,109,235)), ("_leafLight",new Color32(175,194,133,235)),
                ("_box",new Color32(195,155,106,255)), ("_water",new Color32(149,193,199,230)),
                ("_wood",new Color32(154,119,78,255)), ("_mud",new Color32(172,145,105,180)) })
                palette.FindProperty(item.Item1).colorValue = item.Item2;
            palette.ApplyModifiedPropertiesWithoutUndo();
            grid = Rect("OpeningGrid", frame);
            animal = Surface(Rect("Animal", frame), Color.white);
            animal.preserveAspect = true;
            var scene = owner.AddComponent<OutingFieldView>();
            Bind(scene, "_frame", frame); Bind(scene, "_grid", grid); Bind(scene, "_background", background);
            Bind(scene, "_animal", animal); Bind(scene, "_terrain", terrain); Bind(scene, "_sceneRoot",
                area.gameObject);
            Bind(scene, "_cat", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Outing/T_Outing_CatNatural.png"));
            Bind(scene, "_dog", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Outing/T_Outing_DogNatural.png"));
            return scene;
        }
    }
}
