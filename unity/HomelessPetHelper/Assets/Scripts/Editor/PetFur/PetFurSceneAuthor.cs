using System;
using System.Linq;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Preview.PetFur;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.PetFur
{
    public static class PetFurSceneAuthor
    {
        private const string Folder = "Assets/EditorScenes/PetFur/";
        private const string ScenePath = Folder + "SC_PetFurPreview.unity";
        private const string PortraitPath = Folder + "T_DoudouFurPreview.png";
        private const string Controls = "Assets/Art/UI/T_UI_LevelControls.png";

        [MenuItem("Tools/Homeless Pet Helper/Preview/Pet Fur")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        // Only run in a verification copy when reauthoring the saved scene.
        public static void Create()
        {
            ConfigurePortrait();
            var data = Load<TextAsset>("Assets/Art/Station/Doudou/DoudouFur.json");
            var profile = FurProfile.Read(data);
            var portrait = Load<Texture2D>(PortraitPath);
            var sprite = Load<Sprite>(PortraitPath);
            var material = LoadMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = MakeCamera();
            var baseLayer = new GameObject("Doudou", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            baseLayer.sprite = sprite;
            baseLayer.transform.position = new Vector3((profile.width * .5f - profile.crop.x - profile.crop.width * .5f) / 100,
                (profile.crop.y + profile.crop.height * .5f - profile.height * .5f) / 100, 0);
            var overlay = new GameObject("Fur Batch", typeof(MeshFilter), typeof(MeshRenderer)).GetComponent<MeshRenderer>();
            overlay.sortingOrder = 1;
            overlay.sharedMaterial = material;
            var preview = MakeUi(out var pet, out var brush, out var selected, out var unselected);
            var fields = new SerializedObject(preview);
            Bind(fields, "_camera", camera);
            Bind(fields, "_meshFilter", overlay.GetComponent<MeshFilter>());
            Bind(fields, "_overlay", overlay);
            Bind(fields, "_portrait", portrait);
            Bind(fields, "_data", data);
            Bind(fields, "_template", material);
            Bind(fields, "_petButton", pet);
            Bind(fields, "_brushButton", brush);
            Bind(fields, "_selected", selected);
            Bind(fields, "_unselected", unselected);
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save fur preview.");
            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePortrait()
        {
            var importer = AssetImporter.GetAtPath(PortraitPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Run node tools/unity-fur-preview.mjs first.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Material LoadMaterial()
        {
            var path = Folder + "M_PetFurPreview.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var material = new Material(Load<Shader>(Folder + "Sh_PetFurPreview.shader"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Camera MakeCamera()
        {
            var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.position = new Vector3(0, -.20f, -10);
            camera.orthographic = true;
            camera.orthographicSize = 3.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(214, 232, 218, 255);
            camera.allowHDR = false;
            return camera;
        }

        private static PetFurPreview MakeUi(out Image pet, out Image brush, out Sprite selected, out Sprite unselected)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("Preview Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            var input = Child("Fur input", canvas.transform);
            Place(input, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            input.gameObject.AddComponent<Image>().color = Color.clear;
            var preview = input.gameObject.AddComponent<PetFurPreview>();
            var toolbar = Child("Fur controls", canvas.transform);
            Place(toolbar, new Vector2(.12f, .03f), new Vector2(.88f, .03f), Vector2.zero, new Vector2(0, 138));
            var font = Load<Font>("Assets/Art/Fonts/PetRound-Game.ttf");
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Controls).OfType<Sprite>().ToArray();
            selected = sprites.Single(sprite => sprite.name == "Action");
            unselected = sprites.Single(sprite => sprite.name == "Secondary");
            pet = Button(toolbar, "Pet", "抚摸", 0, .32f, font, unselected, preview.Pet);
            brush = Button(toolbar, "Brush", "梳理", .34f, .66f, font, selected, preview.Brush);
            Button(toolbar, "Reset", "复位", .68f, 1, font, unselected, preview.ResetFur);
            return preview;
        }

        private static Image Button(RectTransform parent, string id, string label, float left, float right,
            Font font, Sprite skin, UnityEngine.Events.UnityAction action)
        {
            var rect = Child(id, parent);
            Place(rect, new Vector2(left, 0), new Vector2(right, 1), Vector2.zero, Vector2.zero);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = skin;
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var title = Child("Label", rect);
            Place(title, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var text = title.gameObject.AddComponent<Text>();
            text.text = label;
            text.font = font;
            text.fontSize = 43;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 21;
            text.resizeTextMaxSize = 43;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color32(40, 72, 63, 255);
            text.raycastTarget = false;
            return image;
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

        private static void Bind(SerializedObject obj, string field, UnityEngine.Object value) =>
            obj.FindProperty(field).objectReferenceValue = value;
    }
}
