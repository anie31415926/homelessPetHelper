using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Incremental isolated Prefab contents edit; never recreates the saved panels.</summary>
    public static class ForestEdgeArtImport
    {
        private const string Source = "ForestEdgeSource";
        private const string Art = "Assets/Art/CityMap/";
        private const string Content = "Assets/Settings/Game/GameContent.asset";
        [Serializable] private sealed class Manifest { public Import[] assets = Array.Empty<Import>(); }
        [Serializable] private sealed class Import { public string target = string.Empty; }
        [Serializable] private sealed class Layout
        {
            public int width = 0, height = 0;
            public Layer[] layers = Array.Empty<Layer>();
        }
        [Serializable] private sealed class Layer
        {
            public string name = string.Empty, pointId = string.Empty, path = string.Empty;
            public int x = 0, y = 0, width = 0, height = 0;
        }

        public static void Apply()
        {
            if (!Application.isBatchMode || !Directory.Exists(Source))
                throw new InvalidOperationException("Use isolated ForestEdgeAssets authoring.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Source, "manifest.json")));
            foreach (Import item in manifest.assets) ImportTexture(item.target);
            AssetDatabase.Refresh();
            EditMap();
            BindContent();
            EditPlayfield("Assets/Prefabs/Panels/Panel_Approach.prefab", true);
            EditPlayfield("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab", false);
            AssetDatabase.SaveAssets();
        }

        private static void ImportTexture(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(Path.Combine(Source, Path.GetFileName(path)), path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool landmark = path.EndsWith(".png", StringComparison.Ordinal);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = landmark ? 512 : 1024;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaSource = landmark ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = landmark;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void EditMap()
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Art + "CityMapLayers.json"));
            GameObject root = PrefabUtility.LoadPrefabContents(CityMapAssets.PanelPath);
            try
            {
                var content = AssetDatabase.LoadAssetAtPath<GameContent>(Content);
                var copy = new HomelessPetHelper.Game.Text.OptionPreviewText(content.MapText);
                root.GetComponentsInChildren<Text>(true).Single(label => label.name == "Title").text =
                    copy.Get("title");
                Image terrain = root.GetComponentsInChildren<Image>(true).Single(image => image.name == "MapArt");
                terrain.GetComponent<AspectRatioFitter>().aspectRatio = (float)layout.width / layout.height;
                terrain.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "T_CityMap_Neighborhood.jpg");
                var serialized = new SerializedObject(root.GetComponent<CityMapView>());
                var bindings = serialized.FindProperty("_pointBindings");
                Button template = terrain.GetComponentsInChildren<Button>().First();
                bindings.arraySize = layout.layers.Length - 1;
                int index = 0;
                foreach (Layer layer in layout.layers)
                {
                    Image image = terrain.GetComponentsInChildren<Image>(true)
                        .SingleOrDefault(item => item.name ==
                            (layer.name == "Shelter" ? "ShelterArtwork" : layer.name));
                    if (image == null)
                    {
                        var node = UnityEngine.Object.Instantiate(template.gameObject, terrain.transform);
                        node.name = layer.name;
                        image = node.GetComponent<Image>();
                    }
                    Place(image, layer, layout);
                    if (layer.pointId != "shelter") BindPoint(bindings.GetArrayElementAtIndex(index++), image, layer);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CityMapAssets.PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Place(Image image, Layer layer, Layout layout)
        {
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2((float)layer.x / layout.width,
                1f - (float)(layer.y + layer.height) / layout.height);
            rect.anchorMax = new Vector2((float)(layer.x + layer.width) / layout.width,
                1f - (float)layer.y / layout.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(layer.path);
            image.preserveAspect = true;
            image.color = Color.white;
            if (layer.pointId == "shelter")
            {
                var label = image.GetComponentInChildren<Text>(true);
                var shelterPlate = (RectTransform)label.transform.parent;
                shelterPlate.anchorMin = new Vector2(.5f - 104f / layer.width, 0);
                shelterPlate.anchorMax = new Vector2(.5f + 104f / layer.width, 70f / layer.height);
                shelterPlate.offsetMin = shelterPlate.offsetMax = Vector2.zero;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                return;
            }
            var plate = image.GetComponentsInChildren<Image>(true).Single(value => value.name == "Nameplate");
            plate.rectTransform.anchorMin = new Vector2(.5f - 104f / layer.width, 0);
            plate.rectTransform.anchorMax = new Vector2(.5f + 104f / layer.width, 70f / layer.height);
            plate.rectTransform.offsetMin = plate.rectTransform.offsetMax = Vector2.zero;
            foreach (Text label in plate.GetComponentsInChildren<Text>(true))
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = label.name == "State" ? 22 : 28;
                label.fontSize = label.resizeTextMaxSize;
            }
        }

        private static void BindPoint(SerializedProperty binding, Image image, Layer layer)
        {
            var plate = image.GetComponentsInChildren<Image>(true).Single(value => value.name == "Nameplate");
            binding.FindPropertyRelative("_pointId").stringValue = layer.pointId;
            binding.FindPropertyRelative("_button").objectReferenceValue = image.GetComponent<Button>();
            binding.FindPropertyRelative("_highlight").objectReferenceValue = plate;
            binding.FindPropertyRelative("_label").objectReferenceValue = plate.GetComponentsInChildren<Text>()
                .Single(label => label.name != "State");
            binding.FindPropertyRelative("_stateLabel").objectReferenceValue = plate.GetComponentsInChildren<Text>()
                .Single(label => label.name == "State");
        }

        private static void BindContent()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(Content);
            var config = content.CreateMapConfig();
            var serialized = new SerializedObject(content);
            var entries = serialized.FindProperty("_outingArtwork");
            entries.arraySize = config.Definition.Points.Count;
            for (int i = 0; i < entries.arraySize; i++)
            {
                string id = config.Definition.Points[i].PointId;
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_pointId").stringValue = id;
                entry.FindPropertyRelative("_background").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/Outing/T_Outing_" + char.ToUpperInvariant(id[0]) + id.Substring(1) + ".jpg");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            content.Validate();
        }

        private static void EditPlayfield(string path, bool approach)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Component owner = approach ? (Component)root.GetComponent<ApproachPanelView>() :
                    root.GetComponent<SiegeTrialView>();
                Transform field = approach ? root.GetComponentInChildren<ApproachBoardView>(true).transform :
                    root.GetComponentInChildren<SiegeBattlefield>(true).transform;
                Transform parent = approach ? field : field.parent;
                Image background = parent.GetComponentsInChildren<Image>(true)
                    .SingleOrDefault(image => image.name == "LocationBackground");
                if (background == null) background = CreateBackground(parent, field, approach);
                MainSceneAuthor.Bind(owner, "_locationBackground", background);
                if (approach)
                {
                    field.GetComponent<Image>().color = Color.clear;
                    var serialized = new SerializedObject(owner);
                    foreach (string key in new[] { "_floorColor", "_reachableColor" })
                    {
                        Color color = serialized.FindProperty(key).colorValue;
                        color.a = .18f;
                        serialized.FindProperty(key).colorValue = color;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Image CreateBackground(Transform parent, Transform field, bool approach)
        {
            if (!approach) parent = WrapField(field);
            var node = new GameObject("LocationBackground", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var image = node.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Outing/T_Outing_Parking.jpg");
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static Transform WrapField(Transform field)
        {
            var node = new GameObject("LocationField", typeof(RectTransform), typeof(LayoutElement));
            node.transform.SetParent(field.parent, false);
            node.transform.SetSiblingIndex(field.GetSiblingIndex());
            EditorUtility.CopySerialized(field.GetComponent<LayoutElement>(), node.GetComponent<LayoutElement>());
            var source = (RectTransform)field;
            var rect = (RectTransform)node.transform;
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.offsetMin = source.offsetMin;
            rect.offsetMax = source.offsetMax;
            source.SetParent(rect, false);
            source.anchorMin = Vector2.zero;
            source.anchorMax = Vector2.one;
            source.offsetMin = source.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
