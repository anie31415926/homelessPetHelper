using System;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Author once in isolation; subsequent edits use the saved Prefab contents.</summary>
    public static class CityMapAssets
    {
        public const string ScenePath = "Assets/EditorScenes/SC_CityMap.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_CityMap.prefab";
        public const string TextPath = "Assets/EditorScenes/CityMapText.json";
        public const string ImagePath = "Assets/EditorScenes/CityMapArt/T_CityMap_Neighborhood.jpg";
        private static readonly Color Ink = new Color32(47, 57, 50, 255);
        private static readonly Color Green = new Color32(46, 119, 102, 255);

        [MenuItem("Homeless Pet Helper/Preview/Open City Map")]
        public static void OpenScene()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void CreateAssets()
        {
            if (!Application.isBatchMode || AssetDatabase.LoadMainAssetAtPath(PanelPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
                throw new InvalidOperationException("Author once in isolation; do not overwrite saved map assets.");
            ConfigureTexture();
            var copy = new OptionPreviewText(AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
            GameObject root = Rect("Panel_CityMap", null);
            root.SetActive(false);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Image>().color = new Color32(168, 226, 158, 255);
            GameObject safe = Rect("SafeArea", root.transform);
            Stretch((RectTransform)safe.transform);
            safe.AddComponent<SafeAreaView>();
            var column = safe.AddComponent<VerticalLayoutGroup>();
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            GameObject header = Column("Header", safe.transform, 12, new RectOffset(32, 32, 20, 12));
            header.AddComponent<Image>().color = Color.white;
            GameObject titleRow = Row("TitleRow", header.transform, 88);
            Label("Title", titleRow.transform, copy.Get("title"), 44, 88);
            Toggle pause = Pause(titleRow.transform, copy.Get("pause"));
            Text status = Label("Status", header.transform, string.Empty, 30, 48);
            GameObject viewport = Rect("MapViewport", safe.transform);
            viewport.AddComponent<LayoutElement>().flexibleHeight = 1;
            viewport.AddComponent<RectMask2D>();
            GameObject art = Rect("MapArt", viewport.transform);
            Stretch((RectTransform)art.transform);
            var aspect = art.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 768f / 1344;
            Image image = art.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ImagePath);
            image.raycastTarget = false;
            Button parking = Marker("Parking", art.transform, copy.Get("parking"), new Vector2(0.16f, 0.405f));
            Button yard = Marker("Yard", art.transform, copy.Get("yard"), new Vector2(0.78f, 0.64f));
            Button porch = Marker("Porch", art.transform, copy.Get("porch"), new Vector2(0.81f, 0.245f));
            Text shelter = Label("Shelter", art.transform, copy.Get("shelter"), 28, 48);
            Position(shelter.rectTransform, new Vector2(0.21f, 0.15f), new Vector2(180, 48));
            GameObject footer = Column("Destination", safe.transform, 12, new RectOffset(32, 32, 20, 28));
            footer.AddComponent<Image>().color = Color.white;
            Text heading = Label("Heading", footer.transform, string.Empty, 40, 56);
            Text details = Label("Details", footer.transform, string.Empty, 32, 100);
            Text feedback = Label("Feedback", footer.transform, string.Empty, 28, 76);
            feedback.color = Green;
            GameObject actions = Row("Actions", footer.transform, 96);
            Button cancel = Command("Cancel", actions.transform, copy.Get("return"), Color.white, Ink);
            Button primary = Command("Primary", actions.transform, copy.Get("depart"), Green, Color.white);
            var view = root.AddComponent<CityMapView>();
            Bind(view, "_status", status);
            Bind(view, "_heading", heading);
            Bind(view, "_details", details);
            Bind(view, "_feedback", feedback);
            Bind(view, "_primary", primary);
            Bind(view, "_primaryLabel", primary.GetComponentInChildren<Text>());
            Bind(view, "_cancel", cancel);
            Bind(view, "_pause", pause);
            var serialized = new SerializedObject(view);
            var places = serialized.FindProperty("_places");
            places.arraySize = 3;
            places.GetArrayElementAtIndex(0).objectReferenceValue = parking;
            places.GetArrayElementAtIndex(1).objectReferenceValue = yard;
            places.GetArrayElementAtIndex(2).objectReferenceValue = porch;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            UnityEngine.Object.DestroyImmediate(root);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var camera = new GameObject("Main Camera", typeof(Camera));
            camera.tag = "MainCamera";
            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        public static void NormalizeLayout()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Normalize saved map Prefab in an isolated batch project.");
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                foreach (LayoutElement element in root.GetComponentsInChildren<LayoutElement>(true))
                    if (element.name != "MapViewport") element.flexibleHeight = 0;
                foreach (VerticalLayoutGroup group in root.GetComponentsInChildren<VerticalLayoutGroup>(true))
                {
                    if (group.GetComponent<SafeAreaView>() != null) continue;
                    var element = group.GetComponent<LayoutElement>() ?? group.gameObject.AddComponent<LayoutElement>();
                    element.flexibleHeight = 0;
                }
                foreach (HorizontalLayoutGroup group in root.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                    group.childForceExpandWidth = false;
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureTexture()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;
            importer.SaveAndReimport();
        }

        private static GameObject Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject Column(string name, Transform parent, int spacing, RectOffset padding)
        {
            GameObject go = Rect(name, parent);
            go.AddComponent<LayoutElement>().flexibleHeight = 0;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        private static GameObject Row(string name, Transform parent, int height)
        {
            GameObject go = Rect(name, parent);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleHeight = 0;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return go;
        }

        private static Text Label(string name, Transform parent, string value, int size, int height)
        {
            GameObject go = Rect(name, parent);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleHeight = 0;
            element.flexibleWidth = 1;
            Text label = go.AddComponent<Text>();
            label.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.color = Ink;
            label.text = value;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        private static Button Command(string name, Transform parent, string value, Color surface, Color ink)
        {
            GameObject go = Rect(name, parent);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 96;
            element.flexibleHeight = 0;
            element.flexibleWidth = 1;
            Image image = go.AddComponent<Image>();
            image.color = surface;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.91f, 0.96f, 0.91f);
            colors.pressedColor = new Color(0.73f, 0.85f, 0.73f);
            colors.disabledColor = new Color(0.65f, 0.68f, 0.65f, 0.65f);
            button.colors = colors;
            Text label = Label(name + "Label", go.transform, value, 32, 96);
            label.color = ink;
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(12, 4);
            label.rectTransform.offsetMax = new Vector2(-12, -4);
            return button;
        }

        private static Button Marker(string name, Transform parent, string value, Vector2 point)
        {
            Button button = Command(name, parent, value, Color.white, Ink);
            Position((RectTransform)button.transform, point, new Vector2(216, 88));
            return button;
        }

        private static Toggle Pause(Transform parent, string value)
        {
            GameObject go = Rect("Pause", parent);
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = 180;
            element.flexibleWidth = 0;
            element.flexibleHeight = 0;
            Image image = go.AddComponent<Image>();
            image.color = new Color32(233, 241, 237, 255);
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = image;
            Image tick = Rect("Check", go.transform).AddComponent<Image>();
            tick.color = Green;
            Position(tick.rectTransform, new Vector2(0.17f, 0.5f), new Vector2(22, 22));
            toggle.graphic = tick;
            Text label = Label("PauseLabel", go.transform, value, 30, 88);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(58, 0);
            return toggle;
        }

        private static void Position(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
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
    }
}
