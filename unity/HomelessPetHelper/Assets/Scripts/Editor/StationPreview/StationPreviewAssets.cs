using System;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static HomelessPetHelper.Editor.StationPreview.StationPreviewLayout;

namespace HomelessPetHelper.Editor.StationPreview
{
    public static class StationPreviewAssets
    {
        public const string ScenePath = "Assets/EditorScenes/SC_StationWhiteboard.unity";
        public const string PanelPath = "Assets/Prefabs/Panels/Panel_StationWhiteboard.prefab";
        public const string TextPath = "Assets/EditorScenes/StationPreviewText.json";
        public const string ArtFolder = "Assets/EditorScenes/StationArt";

        [MenuItem("Homeless Pet Helper/Preview/Open Station Whiteboard")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        public static void CreateAssets()
        {
            if (!Application.isBatchMode || AssetDatabase.LoadMainAssetAtPath(PanelPath) != null ||
                AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
            {
                throw new InvalidOperationException("Create new station assets only in an isolated batch project.");
            }

            ImportArt();
            var text = new OptionPreviewText(AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
            CreatePanel(text);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath));
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void ImportArt()
        {
            foreach (string kind in new[] { "cat", "dog" })
            {
                string path = ArtFolder + "/T_Station_" + kind + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                {
                    throw new InvalidOperationException("Station art failed to import as a Sprite: " + path);
                }
            }
        }

        public static void NormalizeArt()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Normalize fixture art in an isolated batch project.");
            }

            ImportArt();
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<StationPanelView>());
                var image = (Image)serialized.FindProperty("_summaryAnimal").objectReferenceValue;
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/T_Station_cat.png");
                foreach (DirtSurfaceView surface in root.GetComponentsInChildren<DirtSurfaceView>(true))
                {
                    var surfaceFields = new SerializedObject(surface);
                    var animal = (Image)surfaceFields.FindProperty("_animal").objectReferenceValue;
                    animal.raycastTarget = false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreatePanel(OptionPreviewText text)
        {
            GameObject root = Rect("Panel_StationWhiteboard", null);
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Image>().color = Color.white;
            var view = root.AddComponent<StationPanelView>();
            GameObject safe = Column("SafeArea", root.transform);
            Stretch((RectTransform)safe.transform);
            safe.AddComponent<SafeAreaView>();
            safe.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(40, 40, 32, 32);
            Label("Title", safe.transform, text.Get("title"), 48, 72);
            GameObject status = Row("Status", safe.transform, 60);
            Bind(view, "_clock", Label("Clock", status.transform, string.Empty, 34, 60));
            Bind(view, "_capacity", Label("Capacity", status.transform, string.Empty, 32, 60));
            GameObject body = Rect("Body", safe.transform);
            body.AddComponent<LayoutElement>().flexibleHeight = 1;
            Bind(view, "_actions", body.AddComponent<CanvasGroup>());
            CreateSummary(body.transform, view, text);
            CreateOverview(body.transform, view, text);
            CreateDetail(body.transform, view, text);
            Bind(view, "_feedback", Label("Feedback", safe.transform, string.Empty, 34, 104));
            GameObject controls = Row("Session", safe.transform, 88);
            Bind(view, "_pause", Pause(controls.transform, text.Get("pause")));
            Bind(view, "_restart", Button("Restart", controls.transform, text.Get("restart")));
            root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static GameObject Page(string name, Transform parent)
        {
            GameObject page = Column(name, parent);
            Stretch((RectTransform)page.transform);
            return page;
        }

        private static void CreateSummary(Transform parent, StationPanelView view, OptionPreviewText text)
        {
            GameObject page = Page("Summary", parent);
            Bind(view, "_summary", page);
            Label("Heading", page.transform, text.Get("summary"), 44, 72);
            GameObject stage = Rect("ArrivalArt", page.transform);
            stage.AddComponent<LayoutElement>().flexibleHeight = 1;
            GameObject artwork = Rect("Animal", stage.transform);
            Stretch((RectTransform)artwork.transform);
            var image = artwork.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/T_Station_cat.png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            Bind(view, "_summaryAnimal", image);
            Bind(view, "_summaryBody", Label("Outcome", page.transform, string.Empty, 38, 272));
            Bind(view, "_enter", Button("Enter", page.transform, text.Get("enter"), true));
        }

        private static void CreateOverview(Transform parent, StationPanelView view, OptionPreviewText text)
        {
            GameObject page = Page("Overview", parent);
            Bind(view, "_overview", page);
            Label("Heading", page.transform, text.Get("overview"), 44, 72);
            var animals = new StationAnimalView[3];
            for (int index = 0; index < animals.Length; index++)
            {
                GameObject row = Row("Animal" + index, page.transform, 256);
                row.GetComponent<LayoutElement>().flexibleHeight = 1;
                var background = row.AddComponent<Image>();
                background.color = Surface;
                var select = row.AddComponent<Button>();
                select.targetGraphic = background;
                GameObject artSlot = Rect("ArtSlot", row.transform);
                artSlot.AddComponent<LayoutElement>().preferredWidth = 320;
                DirtSurfaceView surface = CreateSurface(artSlot.transform);
                GameObject info = Column("Info", row.transform);
                info.GetComponent<LayoutElement>().flexibleWidth = 1;
                info.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
                Text name = Label("Name", info.transform, text.Get("name" + index), 42, 64);
                Text condition = Label("Condition", info.transform, string.Empty, 34, 128);
                animals[index] = row.AddComponent<StationAnimalView>();
                Bind(animals[index], "_select", select);
                Bind(animals[index], "_name", name);
                Bind(animals[index], "_status", condition);
                Bind(animals[index], "_surface", surface);
            }

            var serialized = new SerializedObject(view);
            SerializedProperty array = serialized.FindProperty("_animals");
            array.arraySize = animals.Length;
            for (int index = 0; index < animals.Length; index++)
            {
                array.GetArrayElementAtIndex(index).objectReferenceValue = animals[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Bind(view, "_history", Button("History", page.transform, text.Get("history")));
            page.SetActive(false);
        }

        private static void CreateDetail(Transform parent, StationPanelView view, OptionPreviewText text)
        {
            GameObject page = Page("Detail", parent);
            Bind(view, "_detail", page);
            Bind(view, "_name", Label("Name", page.transform, string.Empty, 44, 64));
            Bind(view, "_condition", Label("Condition", page.transform, string.Empty, 34, 104));
            GameObject stage = Rect("CareStage", page.transform);
            stage.AddComponent<LayoutElement>().flexibleHeight = 1;
            Bind(view, "_surface", CreateSurface(stage.transform));
            Bind(view, "_progress", Label("Progress", page.transform, string.Empty, 34, 64));
            GameObject commands = Row("CareCommands", page.transform, 104);
            Bind(view, "_check", Button("Check", commands.transform, text.Get("check")));
            Bind(view, "_pet", Button("Pet", commands.transform, text.Get("pet")));
            Bind(view, "_clean", Button("Clean", commands.transform, text.Get("clean"), true));
            Button finish = Button("Finish", commands.transform, text.Get("finish"), true);
            Bind(view, "_finish", finish);
            finish.gameObject.SetActive(false);
            Bind(view, "_back", Button("Back", page.transform, text.Get("back")));
            page.SetActive(false);
        }

        private static DirtSurfaceView CreateSurface(Transform parent)
        {
            GameObject root = Rect("DirtSurface", parent);
            Stretch((RectTransform)root.transform);
            var fit = root.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 300f / 320;
            var surface = root.AddComponent<DirtSurfaceView>();
            GameObject animal = Rect("Animal", root.transform);
            Stretch((RectTransform)animal.transform);
            var image = animal.AddComponent<Image>();
            image.raycastTarget = false;
            GameObject dirt = Rect("Dirt", root.transform);
            Stretch((RectTransform)dirt.transform);
            var overlay = dirt.AddComponent<RawImage>();
            overlay.raycastTarget = false;
            Bind(surface, "_animal", image);
            Bind(surface, "_dirt", overlay);
            return surface;
        }
    }
}
