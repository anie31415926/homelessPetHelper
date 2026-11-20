using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.OptionPreview
{
    /// <summary>
    /// Explicit authoring tool, never invoked by a Player. Saved Prefabs are the editable source
    /// after creation; repeated calls refuse to overwrite them or an artist's Sprite assignments.
    /// </summary>
    public static class OptionPreviewAssets
    {
        public const string ScenePath = "Assets/EditorScenes/SC_OptionWhiteboard.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_OptionWhiteboard.prefab";
        public const string CardPath = "Assets/Prefabs/Widgets/Widget_OptionCard.prefab";
        private const string TextPath = "Assets/EditorScenes/OptionPreviewText.json";
        private static readonly Color Ink = new Color32(32, 36, 40, 255);
        private static readonly Color Surface = new Color32(242, 243, 244, 255);

        [MenuItem("Homeless Pet Helper/Preview/Open Option Whiteboard")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        public static void CreateAssets()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Author preview assets in an isolated batch project.");
            }

            if (AssetDatabase.LoadMainAssetAtPath(PanelPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(CardPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
            {
                throw new InvalidOperationException("Preview assets already exist; edit the saved Prefabs in place.");
            }

            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Panels");
            EnsureFolder("Assets/Prefabs/Widgets");
            var textSource = AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath);
            var text = new OptionPreviewText(textSource);
            OptionCardView card = CreateCard(text);
            CreatePanel(card, text);
            CreateScene();
            AssetDatabase.SaveAssets();
        }

        public static void NormalizeLayout()
        {
            foreach (string path in new[] { PanelPath, CardPath })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (LayoutElement element in root.GetComponentsInChildren<LayoutElement>(true))
                    {
                        if (element.flexibleHeight < 0)
                        {
                            element.flexibleHeight = 0;
                        }

                        if (element.TryGetComponent(out Selectable control))
                        {
                            element.flexibleWidth = 1;
                        }
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static OptionCardView CreateCard(OptionPreviewText text)
        {
            GameObject root = Rect("Widget_OptionCard", null);
            root.SetActive(false);
            root.AddComponent<LayoutElement>().preferredHeight = 256;
            Image surface = root.AddComponent<Image>();
            surface.color = Color.white;
            surface.type = Image.Type.Sliced;
            HorizontalLayoutGroup row = root.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 20, 20);
            row.spacing = 24;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            Image artwork = Rect("Artwork", root.transform).AddComponent<Image>();
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;
            artwork.gameObject.AddComponent<LayoutElement>().preferredWidth = 144;
            artwork.gameObject.SetActive(false);
            GameObject column = Vertical("Content", root.transform, 12);
            column.AddComponent<LayoutElement>().flexibleWidth = 1;
            Text title = Label("Title", column.transform, string.Empty, 40, 64);
            Text identity = Label("Identity", column.transform, string.Empty, 28, 40);
            GameObject controls = Horizontal("Controls", column.transform, 88);
            Toggle selection = Check("Selection", controls.transform, text.Get("select"));
            Toggle retention = Check("Retention", controls.transform, text.Get("retain"));
            var view = root.AddComponent<OptionCardView>();
            Bind(view, "_title", title);
            Bind(view, "_identity", identity);
            Bind(view, "_selection", selection);
            Bind(view, "_retention", retention);
            Bind(view, "_surface", surface);
            Bind(view, "_artwork", artwork);
            root.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<OptionCardView>();
        }

        private static OptionPanelView CreatePanel(OptionCardView card, OptionPreviewText text)
        {
            GameObject root = Rect("Panel_OptionWhiteboard", null);
            root.SetActive(false);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Image>().color = Color.white;
            GameObject safeArea = Vertical("SafeArea", root.transform, 24);
            Stretch((RectTransform)safeArea.transform);
            safeArea.AddComponent<SafeAreaView>();
            safeArea.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(48, 48, 40, 40);
            Label("Title", safeArea.transform, text.Get("title"), 48, 80);
            GameObject status = Horizontal("Status", safeArea.transform, 56);
            Text turn = Label("Turn", status.transform, string.Empty, 36, 56);
            Text retained = Label("Retained", status.transform, string.Empty, 36, 56);
            Label("Section", safeArea.transform, text.Get("section"), 36, 56);
            ScrollRect scroll = CreateList(safeArea.transform);
            Text feedback = Label("Feedback", safeArea.transform, text.Get("ready"), 34, 100);
            GameObject controls = Horizontal("SessionControls", safeArea.transform, 96);
            Toggle pause = Check("Pause", controls.transform, text.Get("pause"));
            Button restart = Command("Restart", controls.transform, text.Get("restart"));
            Button use = Command("Use", safeArea.transform, text.Get("use"));
            var view = root.AddComponent<OptionPanelView>();
            Bind(view, "_turn", turn);
            Bind(view, "_retained", retained);
            Bind(view, "_feedback", feedback);
            Bind(view, "_pause", pause);
            Bind(view, "_restart", restart);
            Bind(view, "_use", use);
            Bind(view, "_scroll", scroll);
            Bind(view, "_content", scroll.content);
            Bind(view, "_cardPrefab", card);
            root.SetActive(true);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab.GetComponent<OptionPanelView>();
        }

        private static ScrollRect CreateList(Transform parent)
        {
            GameObject root = Rect("OptionList", parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.flexibleHeight = 1;
            root.AddComponent<Image>().color = Surface;
            root.AddComponent<RectMask2D>();
            var scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = (RectTransform)root.transform;
            GameObject content = Vertical("Content", root.transform, 16);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = Vector2.zero;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(16, 16, 16, 16);
            scroll.content = rect;
            return scroll;
        }

        private static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            PrefabUtility.InstantiatePrefab(prefab);
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static GameObject Vertical(string name, Transform parent, float spacing)
        {
            GameObject result = Rect(name, parent);
            var group = result.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            return result;
        }

        private static GameObject Horizontal(string name, Transform parent, float height)
        {
            GameObject result = Rect(name, parent);
            var layout = result.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            var group = result.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 24;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;
            return result;
        }

        private static Text Label(string name, Transform parent, string value, int size, float height)
        {
            GameObject root = Rect(name, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.flexibleWidth = 1;
            layout.flexibleHeight = 0;
            var text = root.AddComponent<Text>();
            text.text = value;
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 24;
            text.resizeTextMaxSize = size;
            text.color = Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        private static Toggle Check(string name, Transform parent, string label)
        {
            GameObject root = Horizontal(name, parent, 88);
            root.GetComponent<LayoutElement>().minWidth = 240;
            root.GetComponent<LayoutElement>().flexibleWidth = 1;
            var background = root.AddComponent<Image>();
            background.color = Surface;
            background.type = Image.Type.Sliced;
            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            GameObject box = Rect("Indicator", root.transform);
            var layout = box.AddComponent<LayoutElement>();
            layout.preferredWidth = 44;
            layout.preferredHeight = 44;
            layout.flexibleWidth = 0;
            layout.flexibleHeight = 0;
            toggle.graphic = box.AddComponent<Image>();
            toggle.graphic.color = Ink;
            Label("Label", root.transform, label, 34, 88);
            var group = root.GetComponent<HorizontalLayoutGroup>();
            group.padding = new RectOffset(16, 16, 0, 0);
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return toggle;
        }

        private static Button Command(string name, Transform parent, string label)
        {
            GameObject root = Rect(name, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = 104;
            layout.flexibleHeight = 0;
            layout.flexibleWidth = 1;
            var image = root.AddComponent<Image>();
            image.type = Image.Type.Sliced;
            image.color = new Color32(220, 239, 228, 255);
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = Label("Label", root.transform, label, 36, 104);
            text.alignment = TextAnchor.MiddleCenter;
            Stretch((RectTransform)text.transform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                int separator = path.LastIndexOf('/');
                AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
            }
        }
    }
}
