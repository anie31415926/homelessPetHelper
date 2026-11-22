using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.Field.FieldAuthorUi;

namespace HomelessPetHelper.Editor.LevelUi
{
    /// <summary>Isolated, incremental Prefab Mode edit. Existing gameplay bindings and IDs are retained.</summary>
    public static class LevelPresentationAuthor
    {
        public const string LibraryPath = "Assets/Settings/UI/LevelMotion.asset";
        private const string Panels = "Assets/Prefabs/Panels/";
        private const string Widgets = "Assets/Prefabs/Widgets/";
        private static Dictionary<string, Sprite> _sprites;

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LevelPresentation.");
            _sprites = AssetDatabase.LoadAllAssetsAtPath(LevelOverlaySkin.TexturePath).OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);
            var library = CreateLibrary();
            Edit(Widgets + "Widget_SiegeChoice.prefab", Choice);
            Edit(Widgets + "Widget_LevelOverlays.prefab", root => Feedback(root, Scope(root, library)));
            Edit(Panels + "Panel_Approach.prefab", root => Panel(root, library));
            Edit(Panels + "Panel_SiegeTrial.prefab", root =>
            {
                Panel(root, library);
                var view = root.GetComponentInChildren<SiegeFieldView>(true);
                Bind(view, "_motion", root.GetComponent<MotionScope>());
                SiegeUi(view);
                CanvasLayer(view.gameObject, true);
                CanvasLayer(Ref<RectTransform>(view, "_hand").gameObject, true);
            });
            AssetDatabase.SaveAssets();
        }

        private static MotionLibrary CreateLibrary()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings/UI"))
                AssetDatabase.CreateFolder("Assets/Settings", "UI");
            var library = AssetDatabase.LoadAssetAtPath<MotionLibrary>(LibraryPath);
            if (library != null) throw new InvalidOperationException("Already authored; edit the saved asset.");
            library = ScriptableObject.CreateInstance<MotionLibrary>();
            var serialized = new SerializedObject(library);
            var definitions = serialized.FindProperty("_definitions");
            definitions.arraySize = 7;
            Define(definitions, 0, MotionCue.Hover, .12f, .025f, 0, Vector2.zero, 1, false, Rise());
            Define(definitions, 1, MotionCue.Press, .08f, -.045f, 0, Vector2.zero, 1, false, Rise());
            Define(definitions, 2, MotionCue.Selected, .14f, .02f, 0, Vector2.zero, 1, false, Rise());
            Define(definitions, 3, MotionCue.Foliage, 3.2f, 0, 1.5f, Vector2.zero, 1, true,
                Curve(0, 0, .25f, 1, .5f, 0, .75f, -1, 1, 0));
            Define(definitions, 4, MotionCue.Water, 2.6f, .018f, 0, Vector2.zero, 1, true,
                Curve(0, 0, .5f, 1, 1, 0));
            Define(definitions, 5, MotionCue.Clear, .35f, .08f, -4, new Vector2(0, 12), 0, false, Rise());
            Define(definitions, 6, MotionCue.Place, .22f, .15f, 0, Vector2.zero, 1, false,
                Curve(0, -1, .65f, .2f, 1, 0));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            library.Validate();
            AssetDatabase.CreateAsset(library, LibraryPath);
            return library;
        }

        private static AnimationCurve Rise() => AnimationCurve.EaseInOut(0, 0, 1, 1);
        private static AnimationCurve Curve(params float[] pairs)
        {
            var keys = new Keyframe[pairs.Length / 2];
            for (int index = 0; index < keys.Length; index++)
                keys[index] = new Keyframe(pairs[index * 2], pairs[index * 2 + 1], 0, 0);
            return new AnimationCurve(keys);
        }
        private static void Define(SerializedProperty definitions, int index, MotionCue cue, float duration,
            float scale, float angle, Vector2 offset, float opacity, bool loop, AnimationCurve curve)
        {
            var item = definitions.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("cue").enumValueIndex = (int)cue;
            item.FindPropertyRelative("durationSeconds").floatValue = duration;
            item.FindPropertyRelative("scaleDelta").vector2Value = Vector2.one * scale;
            item.FindPropertyRelative("angleDegrees").floatValue = angle;
            item.FindPropertyRelative("offset").vector2Value = offset;
            item.FindPropertyRelative("opacity").floatValue = opacity;
            item.FindPropertyRelative("loop").boolValue = loop;
            item.FindPropertyRelative("weight").animationCurveValue = curve;
        }

        private static void Panel(GameObject root, MotionLibrary library)
        {
            var scope = Scope(root, library);
            var scene = root.GetComponent<OutingFieldView>();
            Bind(scene, "_motion", scope);
            var terrain = Ref<OutingFieldGraphic>(scene, "_terrain");
            Bind(terrain, "_motion", scope);
            CanvasLayer(terrain.gameObject, false);
            CanvasLayer(Ref<Image>(scene, "_background").gameObject, false);
            Feedback(root, scope);
        }

        private static MotionScope Scope(GameObject root, MotionLibrary library)
        {
            var scope = root.AddComponent<MotionScope>();
            Bind(scope, "_library", library);
            return scope;
        }

        private static void Feedback(GameObject root, MotionScope scope)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                // Nested widget is authored once at its own source.
                if (root.GetComponent<LevelOverlayView>() == null &&
                    button.GetComponentInParent<LevelOverlayView>(true) != null) continue;
                Wrap(button, scope);
            }
        }

        private static UiMotionFeedback Wrap(Button button, MotionScope scope)
        {
            if (button.GetComponent<UiMotionFeedback>() != null)
                throw new InvalidOperationException("Already wrapped: " + button.name);
            var hit = button.GetComponent<Image>();
            if (hit == null) throw new InvalidOperationException("Expected authored button Image.");
            var children = Enumerable.Range(0, button.transform.childCount)
                .Select(index => button.transform.GetChild(index)).ToArray();
            var visual = Rect("MotionVisual", button.transform);
            var surface = visual.gameObject.AddComponent<Image>();
            EditorUtility.CopySerialized(hit, surface);
            surface.raycastTarget = false;
            hit.sprite = null; hit.type = Image.Type.Simple; hit.color = Color.clear;
            hit.raycastTarget = true;
            foreach (var child in children) child.SetParent(visual, false);
            var layout = button.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                EditorUtility.CopySerialized(layout, visual.gameObject.AddComponent<HorizontalLayoutGroup>());
                UnityEngine.Object.DestroyImmediate(layout);
            }
            button.targetGraphic = surface;
            var motion = button.gameObject.AddComponent<UiMotionFeedback>();
            if (scope != null) Bind(motion, "_scope", scope);
            Bind(motion, "_control", button); Bind(motion, "_visual", visual);
            return motion;
        }

        private static void Choice(GameObject root)
        {
            var button = root.GetComponent<Button>();
            Surface(button.GetComponent<Image>(), "Paper");
            var cell = root.GetComponent<SiegeChoiceCell>();
            var label = Ref<Text>(cell, "_label");
            CardLayout(cell);
            label.fontSize = label.resizeTextMaxSize = 26; label.resizeTextMinSize = 22;
            label.resizeTextForBestFit = true; label.color = LevelOverlaySkin.Ink;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(246, 247, 223, 255);
            colors.pressedColor = new Color32(215, 230, 199, 255);
            colors.disabledColor = new Color32(166, 164, 154, 145);
            button.colors = colors;
            var serialized = new SerializedObject(cell);
            serialized.FindProperty("_normal").colorValue = Color.white;
            serialized.FindProperty("_selectedColor").colorValue = new Color32(197, 228, 166, 255);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Bind(cell, "_motion", Wrap(button, null));
        }

        private static void CardLayout(SiegeChoiceCell cell)
        {
            var label = Ref<Text>(cell, "_label");
            Place(label.rectTransform, .31f, .08f, .94f, .92f);
            label.fontSize = label.resizeTextMaxSize = 26;
            Place(Ref<SiegePropIcon>(cell, "_art").rectTransform, .04f, .10f, .29f, .90f);
        }

        private static void IntentLayout(SiegeFieldView view, RectTransform target)
        {
            target.SetParent(view.transform, false);
            Place(target, .56f, .76f, .98f, .81f);
        }

        public static void RefineSavedUi()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated snapshot.");
            Edit(Widgets + "Widget_SiegeChoice.prefab", root => CardLayout(root.GetComponent<SiegeChoiceCell>()));
            Edit(Panels + "Panel_SiegeTrial.prefab", root =>
            {
                var view = root.GetComponentInChildren<SiegeFieldView>(true);
                var intent = Ref<Text>(view, "_intent").rectTransform;
                var backing = (RectTransform)intent.parent.GetChild(intent.GetSiblingIndex() - 1);
                if (backing.name != "PaperBacking") throw new InvalidOperationException("Missing intent paper.");
                IntentLayout(view, backing); IntentLayout(view, intent);
            });
            AssetDatabase.SaveAssets();
        }

        private static void SiegeUi(SiegeFieldView view)
        {
            IntentLayout(view, Ref<Text>(view, "_intent").rectTransform);
            foreach (string field in new[] { "_status", "_intent" })
            {
                var text = Ref<Text>(view, field);
                PaperLabel(text);
            }
            // Optional hints retain their existing outline and lifetime; no orphaned empty paper.
            Ref<Text>(view, "_hint").color = LevelOverlaySkin.Ink;
            Ref<Text>(view, "_detail").color = LevelOverlaySkin.Ink;
            Charge(Ref<Slider>(view, "_playerBar"), .04f, .48f, "Play", Color.white);
            Charge(Ref<Slider>(view, "_animalBar"), .52f, .96f, "Paw", new Color32(224, 181, 145, 255));
            foreach (string field in new[] { "_top", "_mid", "_bottom" })
            {
                var button = Ref<Button>(view, field);
                Surface((Image)button.targetGraphic, "Secondary");
                button.GetComponentInChildren<Text>(true).color = LevelOverlaySkin.Ink;
            }
        }

        private static void PaperLabel(Text label)
        {
            var outline = label.GetComponent<Outline>();
            if (outline != null) UnityEngine.Object.DestroyImmediate(outline);
            label.color = LevelOverlaySkin.Ink;
            var paper = Rect("PaperBacking", label.transform, -.04f, -.06f, 1.04f, 1.06f);
            paper.SetAsFirstSibling();
            Surface(paper.gameObject.AddComponent<Image>(), "Paper");
            // A child Image draws after Text. Put the backing beside it with matching anchors instead.
            var target = label.rectTransform;
            paper.SetParent(target.parent, false);
            paper.anchorMin = target.anchorMin; paper.anchorMax = target.anchorMax;
            paper.pivot = target.pivot; paper.sizeDelta = target.sizeDelta;
            paper.anchoredPosition = target.anchoredPosition;
            paper.SetSiblingIndex(target.GetSiblingIndex());
        }

        private static void Charge(Slider slider, float left, float right, string symbol, Color tint)
        {
            var rect = (RectTransform)slider.transform;
            Place(rect, left, .102f, right, .121f);
            Surface(slider.GetComponent<Image>(), "VolumeTrack");
            var fill = slider.fillRect.GetComponent<Image>();
            Surface(fill, "VolumeFill"); fill.color = tint;
            // Filled geometry clips to the real progress, including an exactly empty bar.
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = slider.normalizedValue;
            var icon = Rect("ChargeIcon", rect, 0, -.18f, .12f, 1.18f).gameObject.AddComponent<Image>();
            icon.sprite = _sprites[symbol]; icon.preserveAspect = true;
            icon.color = LevelOverlaySkin.Ink; icon.raycastTarget = false;
        }

        private static void Surface(Image image, string sprite)
        {
            image.sprite = _sprites[sprite]; image.type = Image.Type.Sliced;
            image.color = Color.white; image.raycastTarget = false;
        }
        private static void CanvasLayer(GameObject target, bool interactive)
        {
            if (target.GetComponent<Canvas>() == null) target.AddComponent<Canvas>();
            if (interactive && target.GetComponent<GraphicRaycaster>() == null)
                target.AddComponent<GraphicRaycaster>();
        }
        private static void Edit(string path, Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
