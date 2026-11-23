using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Game.Loading;
using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.Loading
{
    public static class LoadingSceneAuthor
    {
        public const string PrefabPath = "Assets/Prefabs/Panels/Panel_Loading.prefab";
        private const string ArtPath = "Assets/Art/Loading/";

        [MenuItem("Homeless Pet Helper/Loading/Open Loading Scene")]
        public static void Open()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(LoadingSceneRoot.ScenePath);
        }

        [MenuItem("Homeless Pet Helper/Loading/Preview Animation")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(LoadingSceneRoot.ScenePath);
            var host = UnityEngine.Object.FindObjectOfType<LoadingSceneRoot>();
            if (host != null) host.ConfigurePreview();
            // Preview is an unsaved scene override, not a change to the startup scene.
            EditorApplication.isPlaying = true;
        }

        public static void Create()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated LoadingAssets authoring.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                throw new InvalidOperationException("Loading already exists. Edit it in Prefab Mode; do not recreate.");
            var manifest = JsonUtility.FromJson<Slices>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                ArtPath + "LoadingSlices.json").text);
            ImportSprites(manifest);
            GameObject root = CreatePanel(manifest);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Loading Camera", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(255, 231, 196, 255);
            camera.cullingMask = 0;
            camera.orthographic = true;
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var host = new GameObject("Loading Root", typeof(LoadingSceneRoot)).GetComponent<LoadingSceneRoot>();
            MainSceneAuthor.Bind(host, "_view", instance.GetComponent<LoadingView>());
            EditorSceneManager.SaveScene(scene, LoadingSceneRoot.ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LoadingSceneRoot.ScenePath, true),
                new EditorBuildSettingsScene(LoadingSceneRoot.MainScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Created static Loading prefab and startup scene. Main was not modified.");
        }

        private static void ImportSprites(Slices manifest)
        {
            string path = ArtPath + "T_Loading_Elements.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer);
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = manifest.sprites.Select(s => new SpriteMetaData
            {
                name = s.name,
                rect = new Rect(s.rect[0], 512 - s.rect[1] - s.rect[3], s.rect[2], s.rect[3]),
                pivot = new Vector2(.5f, .5f),
                alignment = (int)SpriteAlignment.Center
            }).ToArray();
            importer.SaveAndReimport();
            importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath + "T_Loading_Background.png");
            Configure(importer);
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        private static GameObject CreatePanel(Slices manifest)
        {
            var root = new GameObject("Panel_Loading", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(LoadingView));
            root.SetActive(false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            var sprites = AssetDatabase.LoadAllAssetsAtPath(ArtPath + "T_Loading_Elements.png")
                .OfType<Sprite>().ToDictionary(x => x.name);
            RectTransform bg = Rect("Paper", root.transform);
            Stretch(bg);
            AddImage(bg, AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "T_Loading_Background.png"), false);
            RectTransform safe = Rect("SafeArea", root.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaView>();
            RectTransform bounds = Rect("ArtworkBounds", safe);
            Stretch(bounds);
            bounds.offsetMin = new Vector2(48f, 330f);
            bounds.offsetMax = new Vector2(-48f, -80f);
            RectTransform art = Rect("Illustration", bounds);
            art.gameObject.AddComponent<Canvas>();
            var fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            RectTransform orbit = Rect("AnimalOrbit", art);
            Stretch(orbit);
            var animals = new List<RectTransform>();
            var doors = new List<RectTransform>();
            var decorations = new List<RectTransform>();
            foreach (Slice slice in manifest.sprites.Where(s => s.kind != "progress"))
            {
                RectTransform parent = slice.kind == "orbit" ? orbit : art;
                RectTransform item = Rect(slice.name, parent);
                Place(item, slice.source);
                AddImage(item, sprites[slice.name]);
                if (slice.kind == "orbit") animals.Add(item);
                if (slice.kind == "door") doors.Add(item);
                if (slice.kind == "heart" || slice.kind == "star") decorations.Add(item);
            }
            var motion = art.gameObject.AddComponent<LoadingArtworkMotion>();
            MainSceneAuthor.Bind(motion, "_orbit", orbit);
            BindArray(motion, "_animals", animals);
            BindArray(motion, "_doorAnimals", doors);
            BindArray(motion, "_decorations", decorations);
            CreateProgress(root.GetComponent<LoadingView>(), safe, sprites);
            root.SetActive(true);
            return root;
        }

        private static void Place(RectTransform item, int[] box)
        {
            float x = .5f + ((box[0] + box[2]) * .5f - 130f) / 300f;
            float y = .5f - ((box[1] + box[3]) * .5f - 193f) / 300f;
            Vector2 half = new Vector2(box[2] - box[0], box[3] - box[1]) / 600f;
            item.anchorMin = new Vector2(x, y) - half;
            item.anchorMax = new Vector2(x, y) + half;
            item.offsetMin = item.offsetMax = Vector2.zero;
        }

        private static void CreateProgress(LoadingView view, RectTransform parent, Dictionary<string, Sprite> sprites)
        {
            RectTransform group = Rect("Progress", parent);
            group.anchorMin = new Vector2(.5f, 0f);
            group.anchorMax = group.anchorMin;
            group.pivot = new Vector2(.5f, 0f);
            group.sizeDelta = new Vector2(880f, 250f);
            group.anchoredPosition = new Vector2(0f, 72f);
            RectTransform track = Rect("ProgressTrack", group);
            track.sizeDelta = new Vector2(650f, 116f);
            track.anchoredPosition = new Vector2(80f, -32f);
            AddImage(track, sprites["ProgressTrack"]);
            RectTransform fill = Rect("ProgressFill", track);
            Stretch(fill);
            fill.offsetMin = new Vector2(18f, 34f);
            fill.offsetMax = new Vector2(-24f, -34f);
            Image fillImage = AddImage(fill, sprites["ProgressFill"], false);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0f;
            RectTransform cat = Rect("ProgressCat", group);
            cat.sizeDelta = new Vector2(230f, 230f);
            cat.anchoredPosition = new Vector2(-300f, 0f);
            AddImage(cat, sprites["ProgressCat"]);
            RectTransform labelRect = Rect("LoadingLabel", group);
            labelRect.sizeDelta = new Vector2(440f, 90f);
            labelRect.anchoredPosition = new Vector2(0f, 100f);
            AddImage(labelRect, sprites["LoadingSign"], false);
            Text label = AddText(Rect("Text", labelRect), 48, "Loading...");
            Stretch((RectTransform)label.transform);
            Text percent = AddText(Rect("Percent", group), 42, "0%");
            ((RectTransform)percent.transform).sizeDelta = new Vector2(180f, 64f);
            ((RectTransform)percent.transform).anchoredPosition = new Vector2(100f, -118f);
            MainSceneAuthor.Bind(view, "_fill", fillImage);
            MainSceneAuthor.Bind(view, "_label", label);
            MainSceneAuthor.Bind(view, "_percent", percent);
            MainSceneAuthor.Bind(view, "_text", AssetDatabase.LoadAssetAtPath<TextAsset>(ArtPath + "LoadingText.json"));
        }

        private static Text AddText(RectTransform rect, int size, string value)
        {
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = AssetDatabase.LoadAssetAtPath<Font>(MainSceneAuthor.FontPath);
            text.fontSize = size;
            text.text = value;
            text.color = new Color32(92, 65, 40, 255);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, bool preserve = true)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserve;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            item.SetParent(parent, false);
            item.anchorMin = item.anchorMax = new Vector2(.5f, .5f);
            return item;
        }

        private static void Stretch(RectTransform item)
        {
            item.anchorMin = Vector2.zero;
            item.anchorMax = Vector2.one;
            item.offsetMin = item.offsetMax = Vector2.zero;
        }

        private static void BindArray(UnityEngine.Object owner, string field, List<RectTransform> items)
        {
            var data = new SerializedObject(owner);
            SerializedProperty array = data.FindProperty(field);
            array.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        [Serializable] private sealed class Slices { public Slice[] sprites; }
        [Serializable] private sealed class Slice { public string name, kind; public int[] source, rect; }
    }
}
