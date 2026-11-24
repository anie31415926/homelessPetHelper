using HomelessPetHelper.Game.CityMap;
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

        [Test]
        public void StorybookTerrainAndLandmarksKeepIndependentIdBoundHitAreas()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Image[] images = root.GetComponentsInChildren<Image>(true);
            Image terrain = images.Single(image => image.name == "MapArt");
            Assert.That(AssetDatabase.GetAssetPath(terrain.sprite),
                Is.EqualTo(ArtFolder + "T_CityMap_Neighborhood.jpg"));
            Assert.That(terrain.sprite.rect.size, Is.EqualTo(new Vector2(673, 1024)));
            Assert.That(terrain.rectTransform.childCount, Is.EqualTo(8));
            var landmarks = terrain.transform.Find("Landmarks");
            Assert.That(landmarks, Is.Not.Null);
            Assert.That(landmarks.GetSiblingIndex(), Is.Zero, "Artwork must render below the point labels.");
            Assert.That(landmarks.childCount, Is.EqualTo(12));
            foreach (RectTransform landmark in landmarks)
            {
                var visual = landmark.GetComponent<Image>();
                Assert.That(visual, Is.Not.Null, landmark.name);
                Assert.That(visual.raycastTarget, Is.False, landmark.name);
                Assert.That(AssetDatabase.GetAssetPath(visual.sprite),
                    Is.EqualTo(ArtFolder + "StorybookLandmarks/T_Storybook_" + landmark.name + ".png"));
                Vector2 expectedSize = Vector2.Scale(landmark.anchorMax - landmark.anchorMin, terrain.sprite.rect.size);
                Assert.That(Vector2.Distance(visual.sprite.rect.size, expectedSize), Is.LessThan(.01f));
            }
            var areas = new System.Collections.Generic.List<Rect>();
            foreach (RectTransform hotspot in terrain.rectTransform)
            {
                if (hotspot == landmarks) continue;
                bool shelter = hotspot.name == "ShelterArtwork";
                Image hit = hotspot.GetComponent<Image>();
                Assert.That(hit.sprite, Is.Null, hotspot.name);
                Assert.That(hit.color.a, Is.Zero);
                Assert.That(hit.raycastTarget, Is.EqualTo(!shelter));
                Assert.That(hotspot.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(shelter ? 1 : 2));
                Assert.That(hotspot.GetComponent<Button>() != null, Is.EqualTo(!shelter));
                var area = Rect.MinMaxRect(hotspot.anchorMin.x, hotspot.anchorMin.y,
                    hotspot.anchorMax.x, hotspot.anchorMax.y);
                Assert.That(area.xMin, Is.GreaterThanOrEqualTo(0), hotspot.name);
                Assert.That(area.yMin, Is.GreaterThanOrEqualTo(0), hotspot.name);
                Assert.That(area.xMax, Is.LessThanOrEqualTo(1), hotspot.name);
                Assert.That(area.yMax, Is.LessThanOrEqualTo(1), hotspot.name);
                Assert.That(areas.Any(previous => previous.Overlaps(area)), Is.False, hotspot.name);
                areas.Add(area);
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
                var stateLabel = (Text)binding.FindPropertyRelative("_stateLabel").objectReferenceValue;
                Assert.That(button.transform.parent, Is.SameAs(terrain.transform));
                Assert.That(plate.transform.parent, Is.SameAs(button.transform));
                Assert.That(plate, Is.Not.SameAs(button.image));
                Assert.That(plate.raycastTarget, Is.True);
                Assert.That(label.transform.IsChildOf(plate.transform), Is.True);
                Assert.That(stateLabel, Is.Not.Null);
                Assert.That(stateLabel, Is.Not.SameAs(label));
                Assert.That(stateLabel.transform.IsChildOf(plate.transform), Is.True);
            }
            Assert.That(serialized.FindProperty("_departure").objectReferenceValue, Is.Not.Null);
        }

        [TestCase("Parking", 206, 165)]
        [TestCase("Yard", 249, 187)]
        [TestCase("Porch", 235, 220)]
        [TestCase("Shelter", 204, 136)]
        [TestCase("Park", 265, 105)]
        [TestCase("Market", 198, 155)]
        [TestCase("Riverside", 140, 105)]
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
                Assert.That(new[] { pixels[0].a, pixels[source.width - 1].a,
                    pixels[pixels.Length - source.width].a, pixels[pixels.Length - 1].a },
                    Is.All.EqualTo((byte)0), "Landmark corners must not retain a rectangular background.");
                Assert.That(pixels.Count(pixel => pixel.a == 255), Is.GreaterThan(pixels.Length / 5));
                Assert.That(pixels.Any(pixel => pixel.a > 0 && pixel.a < 255), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        [Test]
        public void MainUsesStorybookTerrainAndOnlyTheCurrentLandmarkTextures()
        {
            var source = new Texture2D(2, 2);
            try
            {
                Assert.That(source.LoadImage(File.ReadAllBytes(ArtFolder + "T_CityMap_Neighborhood.jpg")), Is.True);
                Assert.That(source.width, Is.EqualTo(673));
                Assert.That(source.height, Is.EqualTo(1024));
                Assert.That(new FileInfo(ArtFolder + "T_CityMap_Neighborhood.jpg").Length,
                    Is.LessThanOrEqualTo(204800));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
            string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(dependencies, Does.Contain(ArtFolder + "T_CityMap_Neighborhood.jpg"));
            Assert.That(dependencies.Count(path => path.StartsWith(ArtFolder + "StorybookLandmarks/",
                StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.Ordinal)), Is.EqualTo(12));
            Assert.That(dependencies.Any(path => path.StartsWith(ArtFolder + "ValleyIcons/",
                StringComparison.Ordinal)), Is.False);
            Assert.That(dependencies.Any(path => path.StartsWith(ArtFolder + "T_CityPlace_",
                StringComparison.Ordinal)), Is.False);
            Assert.That(dependencies.Any(path => path.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)),
                Is.False);
        }
    }
}
