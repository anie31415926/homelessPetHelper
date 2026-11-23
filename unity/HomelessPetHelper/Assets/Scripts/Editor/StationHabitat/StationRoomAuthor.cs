using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.StationHabitat
{
    public static class StationRoomAuthor
    {
        private const string Folder = "Assets/Art/StationRoom/";
        private const string Panel = "Assets/Prefabs/Panels/Panel_StationLoop.prefab";
        [Serializable] private sealed class Manifest { public int width, height; public Slice[] slices; }
        [Serializable] private sealed class Slice { public string id; public int x, y, width, height, left, top; }

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated station-room.ps1.");
            var data = JsonUtility.FromJson<Manifest>(File.ReadAllText("StationRoomSource/unity-import.json"));
            Directory.CreateDirectory(Folder);
            Import("T_StationRoom_Background.jpg", null);
            Import("T_StationRoom_Furniture.png", data.slices);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Folder + "T_StationRoom_Furniture.png")
                .OfType<Sprite>().ToDictionary(sprite => sprite.name);
            var root = PrefabUtility.LoadPrefabContents(Panel);
            try
            {
                var scene = root.GetComponentInChildren<StationSceneView>(true);
                if (scene.Room != null)
                    throw new InvalidOperationException("Room already exists. Edit in Prefab Mode.");
                Assemble(scene, data, sprites);
                PrefabUtility.SaveAsPrefabAsset(root, Panel);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void Assemble(StationSceneView scene, Manifest data,
            System.Collections.Generic.Dictionary<string, Sprite> sprites)
        {
            foreach (Transform child in scene.transform)
                if (child.GetComponent<StationResidentView>() == null && child.name != "EmptyStation")
                    child.gameObject.SetActive(false);
            scene.GetComponent<Image>().color = new Color32(249, 229, 220, 255);
            scene.GetComponent<Image>().raycastTarget = false;
            var content = Rect("StationRoomArt", scene.transform, Vector2.zero, Vector2.one);
            content.SetAsFirstSibling();
            var fit = content.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)data.width / data.height;
            var room = content.gameObject.AddComponent<StationRoomView>();
            Bind(room, "_content", content);
            var background = Picture("RoomBackground", content, Vector2.zero, Vector2.one,
                AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "T_StationRoom_Background.jpg"));
            background.preserveAspect = true;
            RectTransform bed = null;
            foreach (var slice in data.slices.OrderBy(slice => slice.id == "bed" ? 1 : 0))
            {
                var min = new Vector2((float)slice.left / data.width,
                    1f - (float)(slice.top + slice.height) / data.height);
                var max = new Vector2((float)(slice.left + slice.width) / data.width,
                    1f - (float)slice.top / data.height);
                var image = Picture("Furniture_" + slice.id, content, min, max, sprites[slice.id]);
                if (slice.id == "bed") bed = image.rectTransform;
            }
            Bind(room, "_bed", bed);
            var obstacles = new[] {
                Obstacle("BedFootprint", content, new Vector2(.50f, .35f), new Vector2(.43f, .125f)),
                Obstacle("TreeFootprint", content, new Vector2(.12f, .48f), new Vector2(.105f, .045f)),
                Obstacle("PlantFootprint", content, new Vector2(.25f, .51f), new Vector2(.04f, .025f)),
                Obstacle("PillowFootprint", content, new Vector2(.77f, .50f), new Vector2(.12f, .025f)),
                Obstacle("CabinetFootprint", content, new Vector2(.96f, .48f), new Vector2(.07f, .035f))
            };
            BindArray(room, "_obstacles", obstacles);
            foreach (var resident in scene.Cells)
            {
                resident.transform.SetParent(content, false);
                Stretch((RectTransform)resident.transform, Vector2.zero, Vector2.one);
                var canvas = resident.GetComponent<Canvas>();
                if (canvas != null) canvas.overrideSorting = false;
            }
            BindArray(room, "_residents", scene.Cells.Cast<Object>().ToArray());
            Bind(scene, "_room", room);
            var empty = Read<Text>(scene, "_empty");
            empty.transform.SetAsLastSibling();
            room.CreateArea();
        }

        private static StationRoomObstacle Obstacle(string name, Transform parent, Vector2 center, Vector2 radius)
        {
            var node = Rect(name, parent, Vector2.zero, Vector2.one);
            var obstacle = node.gameObject.AddComponent<StationRoomObstacle>();
            var fields = new SerializedObject(obstacle);
            fields.FindProperty("_center").vector2Value = center;
            fields.FindProperty("_radius").vector2Value = radius;
            fields.ApplyModifiedPropertiesWithoutUndo();
            return obstacle;
        }

        private static void Import(string file, Slice[] slices)
        {
            string target = Folder + file;
            File.Copy("StationRoomSource/" + file, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = slices == null ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.isReadable = importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            if (slices != null)
            {
                var factories = new SpriteDataProviderFactories();
                factories.Init();
                var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name);
                var rects = slices.Select(slice => new SpriteRect { name = slice.id,
                    rect = new Rect(slice.x, slice.y, slice.width, slice.height),
                    pivot = new Vector2(.5f, .5f), alignment = SpriteAlignment.Center,
                    spriteID = previous.TryGetValue(slice.id, out var old) ? old.spriteID : GUID.Generate()
                }).ToArray();
                provider.SetSpriteRects(rects);
                provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                    rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
                provider.Apply();
            }
            importer.SaveAndReimport();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect, min, max);
            return rect;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Image Picture(string name, Transform parent, Vector2 min, Vector2 max, Sprite sprite)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }
        private static void Bind(Object owner, string field, Object value) => MainSceneAuthor.Bind(owner, field, value);
        private static T Read<T>(Object owner, string field) where T : Object =>
            (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void BindArray(Object owner, string field, Object[] values)
        {
            var serialized = new SerializedObject(owner);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
