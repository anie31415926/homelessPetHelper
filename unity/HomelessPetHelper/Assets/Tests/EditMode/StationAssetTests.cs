using System.Linq;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Modules.Station.UI.Habitat;
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
            string path = "Assets/Art/Station/T_Station_" + kind + "Temporary";
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

        [TestCase("Panel_StationWhiteboard", false)]
        [TestCase("Panel_StationLoop", true)]
        public void AllStationBindingsAreSavedAndSceneIsExcludedFromBuild(string panel, bool expandable)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/" + panel + ".prefab");
            Assert.That(root, Is.Not.Null);
            var detailSurface = root.GetComponent<StationPanelView>().Surface;
            var detailBubbles = detailSurface.GetComponentInChildren<CareBubbleView>(true);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero);
            }

            foreach (MonoBehaviour view in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(view is StationPanelView) && !(view is StationAnimalView) && !(view is DirtSurfaceView) &&
                    !(view is StationSceneView) && !(view is StationSceneChrome) && !(view is StationResidentView))
                {
                    continue;
                }

                SerializedProperty field = new SerializedObject(view).GetIterator();
                while (field.NextVisible(true))
                {
                    if (view is DirtSurfaceView && IsProjectionBinding(field.propertyPath))
                    {
                        Assert.That(field.objectReferenceValue != null,
                            Is.EqualTo(expandable && view == detailSurface), view.name + ": " + field.propertyPath);
                        continue;
                    }
                    if (view is StationPanelView &&
                        (field.propertyPath == "_rosterContent" || field.propertyPath == "_habitat" ||
                            field.propertyPath == "_scene" || field.propertyPath == "_chrome" ||
                            field.propertyPath == "_summaryRename" || field.propertyPath == "_detailRename" ||
                            field.propertyPath == "_portraits"))
                    {
                        Assert.That(field.objectReferenceValue != null, Is.EqualTo(expandable),
                            "Only the expandable loop panel owns saved scroll content and the habitat.");
                        continue;
                    }
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
                var atlas = new SerializedObject(bubbles).FindProperty("_atlas").objectReferenceValue;
                Assert.That(atlas != null, Is.EqualTo(expandable && bubbles == detailBubbles), bubbles.name);
            }
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path.Contains("EditorScenes")), Is.False);
        }

        private static bool IsProjectionBinding(string path) => path == "_legacyCat" || path == "_legacyDog" ||
            path == "_rescueCat" || path == "_rescueDog" || path == "_catProjection" || path == "_dogProjection";

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
