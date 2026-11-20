using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Editor.CityMap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class CityMapLayerAssetTests
    {
        private const string ArtFolder = "Assets/Art/CityMap/";
        private const string PanelPath = "Assets/Prefabs/Panels/Panel_CityMap.prefab";

        [Serializable] private sealed class Layout
        {
            public int width = 0;
            public int height = 0;
            public Layer[] layers = Array.Empty<Layer>();
        }

        [Serializable] private sealed class Layer
        {
            public string name = string.Empty;
            public string path = string.Empty;
            public int x = 0;
            public int y = 0;
            public int width = 0;
            public int height = 0;
        }

        [Test]
        public void SavedPrefabBindsIndependentLayersAndIdBasedNameplateHighlights()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Image[] images = root.GetComponentsInChildren<Image>(true);
            Image terrain = images.Single(image => image.name == "MapArt");
            Assert.That(AssetDatabase.GetAssetPath(terrain.sprite),
                Is.EqualTo(ArtFolder + "T_CityMap_Neighborhood.jpg"));
            Layout layout = JsonUtility.FromJson<Layout>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                ArtFolder + "CityMapLayers.json").text);
            Assert.That(layout.layers.Length, Is.EqualTo(4));
            foreach (Layer layer in layout.layers)
            {
                string nodeName = layer.name == "Shelter" ? "ShelterArtwork" : layer.name;
                Image image = images.Single(item => item.name == nodeName);
                Assert.That(image.transform.parent, Is.SameAs(terrain.transform));
                Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(layer.path));
                Assert.That(image.rectTransform.anchorMin, Is.EqualTo(new Vector2((float)layer.x / layout.width,
                    1f - (float)(layer.y + layer.height) / layout.height)));
                Assert.That(image.rectTransform.anchorMax,
                    Is.EqualTo(new Vector2((float)(layer.x + layer.width) / layout.width,
                    1f - (float)layer.y / layout.height)));
                Assert.That(image.preserveAspect, Is.True);
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(image.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(1));
                Assert.That(image.GetComponent<Button>() != null, Is.EqualTo(layer.name != "Shelter"));
            }
            MonoBehaviour view = root.GetComponents<MonoBehaviour>()
                .Single(item => item.GetType().Name == "CityMapView");
            var serialized = new SerializedObject(view);
            var config = new CityMapPreviewConfig(AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.ConfigPath));
            SerializedProperty bindings = serialized.FindProperty("_pointBindings");
            Assert.That(bindings.arraySize, Is.EqualTo(config.Definition.Points.Count));
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                string id = binding.FindPropertyRelative("_pointId").stringValue;
                Assert.That(ids.Add(id), Is.True);
                Assert.That(config.Definition.TryGetPoint(id, out _), Is.True);
                var button = (Button)binding.FindPropertyRelative("_button").objectReferenceValue;
                var plate = (Image)binding.FindPropertyRelative("_highlight").objectReferenceValue;
                var label = (Text)binding.FindPropertyRelative("_label").objectReferenceValue;
                Assert.That(plate.transform.parent, Is.SameAs(button.transform));
                Assert.That(plate, Is.Not.SameAs(button.image));
                Assert.That(plate.raycastTarget, Is.True);
                Assert.That(label.transform.IsChildOf(plate.transform), Is.True);
            }
        }

        [TestCase("Parking", 208, 190)]
        [TestCase("Yard", 354, 211)]
        [TestCase("Porch", 250, 234)]
        [TestCase("Shelter", 294, 218)]
        public void PlaceSpritesKeepNativeSizeTransparentSilhouettesAndBoundedImports(
            string name, int width, int height)
        {
            string path = ArtFolder + "T_CityPlace_" + name + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(width, height)));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.maxTextureSize, Is.EqualTo(512));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(new FileInfo(path).Length, Is.LessThanOrEqualTo(61440));
            var source = new Texture2D(2, 2);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(source.width, Is.EqualTo(width));
                Assert.That(source.height, Is.EqualTo(height));
                Color32[] pixels = source.GetPixels32();
                Assert.That(pixels.Count(pixel => pixel.a == 0), Is.GreaterThan(pixels.Length / 5));
                Assert.That(pixels.Count(pixel => pixel.a == 255), Is.GreaterThan(pixels.Length / 5));
                Assert.That(pixels.Any(pixel => pixel.a > 0 && pixel.a < 255), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void TerrainHasNoBakedLandmarksAndMapLayersStayOutsideMainDependencies()
        {
            var source = new Texture2D(2, 2);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(ArtFolder + "T_CityMap_Neighborhood.jpg")), Is.True);
                // Sample the former roof/interior centers in source-image coordinates.
                foreach (Vector2Int point in new[] { new Vector2Int(110, 810), new Vector2Int(600, 480),
                    new Vector2Int(640, 980), new Vector2Int(160, 1040) })
                {
                    Color color = source.GetPixel(point.x, source.height - 1 - point.y);
                    Assert.That(color.g, Is.GreaterThan(color.r * 1.15f));
                    Assert.That(color.g, Is.GreaterThan(color.b * 1.15f));
                    Assert.That(color.g, Is.GreaterThan(0.8f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(dependencies.Any(path => path.StartsWith(ArtFolder, StringComparison.Ordinal)), Is.True);
            Assert.That(dependencies.Any(path => path.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)),
                Is.False);
        }
    }
}
