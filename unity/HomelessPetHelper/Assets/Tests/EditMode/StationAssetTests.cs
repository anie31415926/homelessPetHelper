using System.Linq;
using HomelessPetHelper.Modules.Station.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class StationAssetTests
    {
        [TestCase("Cat")]
        [TestCase("Dog")]
        public void TemporaryCareArtRespectsPreviewImageBudgetAndAlignedMask(string kind)
        {
            string path = "Assets/EditorScenes/StationCareArt/T_Station_" + kind + "Temporary";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.texture.width, Is.EqualTo(512));
            Assert.That(sprite.texture.height, Is.EqualTo(512));
            Assert.That(new System.IO.FileInfo(path + ".png").Length, Is.LessThanOrEqualTo(150 * 1024));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.GetDefaultPlatformTextureSettings().maxTextureSize, Is.EqualTo(512));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            byte[] mask = AssetDatabase.LoadAssetAtPath<TextAsset>(path + "Mask.bytes").bytes;
            Assert.That(mask.Length, Is.EqualTo(128 * 128));
            Assert.That(mask.Any(value => value == 0) && mask.Any(value => value == 255), Is.True);
            Assert.That(mask[0], Is.Zero);
        }

        [Test]
        public void AllStationBindingsAreSavedAndSceneIsExcludedFromBuild()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_StationWhiteboard.prefab");
            Assert.That(root, Is.Not.Null);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero);
            }

            foreach (MonoBehaviour view in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(view is StationPanelView) && !(view is StationAnimalView) && !(view is DirtSurfaceView))
                {
                    continue;
                }

                SerializedProperty field = new SerializedObject(view).GetIterator();
                while (field.NextVisible(true))
                {
                    if (field.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        Assert.That(field.objectReferenceValue, Is.Not.Null, view.name + ": " + field.propertyPath);
                    }
                }
            }

            Assert.That(root.GetComponentsInChildren<StationAnimalView>(true).Length, Is.EqualTo(3));
            foreach (CareBubbleView bubbles in root.GetComponentsInChildren<CareBubbleView>(true))
            {
                Assert.That(bubbles.GetComponent<CanvasRenderer>(), Is.Not.Null,
                    "Bubble state can be live without rendering when the saved CanvasRenderer is missing.");
            }
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path.Contains("EditorScenes")), Is.False);
        }

        [TestCase("cat")]
        [TestCase("dog")]
        public void SampleArtIsStaticNonReadableAndMaskIsBounded(string kind)
        {
            string path = "Assets/EditorScenes/StationArt/T_Station_" + kind;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path + ".png");
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png"), Is.Not.Null,
                string.Join(",", AssetDatabase.LoadAllAssetsAtPath(path + ".png")
                    .Select(asset => asset.GetType().Name)));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            byte[] mask = AssetDatabase.LoadAssetAtPath<TextAsset>(path + "Mask.bytes").bytes;
            Assert.That(mask.Length, Is.EqualTo(128 * 128));
            Assert.That(mask.Any(value => value == 0) && mask.Any(value => value == 255), Is.True);
        }
    }
}
