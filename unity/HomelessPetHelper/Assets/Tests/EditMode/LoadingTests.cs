using System;
using System.Linq;
using HomelessPetHelper.Game.Loading;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class LoadingTests
    {
        [Test]
        public void DemoTaskUsesExplicitTimeClampsAndReleasesIdempotently()
        {
            var task = new DemoLoadingTask(2f);
            task.Tick(0f);
            Assert.That(task.Progress, Is.Zero);
            Assert.That(task.IsComplete, Is.False);
            task.Tick(.5f);
            Assert.That(task.Progress, Is.EqualTo(.25f));
            task.Tick(.5f);
            Assert.That(task.Progress, Is.EqualTo(.5f));
            task.Tick(float.MaxValue);
            Assert.That(task.Progress, Is.EqualTo(1f));
            Assert.That(task.IsComplete, Is.True);
            task.Dispose();
            task.Dispose();
            Assert.Throws<ObjectDisposedException>(() => task.Tick(0f));
            using (var skipped = new DemoLoadingTask(0f))
            {
                Assert.That(skipped.IsComplete, Is.True);
                Assert.That(skipped.Progress, Is.EqualTo(1f));
            }
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void DemoTaskRejectsInvalidDurationAndElapsed(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DemoLoadingTask(value));
            using (var task = new DemoLoadingTask(2f))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => task.Tick(value));
                Assert.That(task.Progress, Is.Zero);
                Assert.That(task.IsComplete, Is.False);
            }
        }

        [Test]
        public void StartupSceneIsLightweightAndDoesNotStaticallyReferenceMain()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).ToArray();
            Assert.That(scenes, Is.EqualTo(new[] { LoadingSceneRoot.ScenePath, LoadingSceneRoot.MainScenePath }));
            string[] dependencies = AssetDatabase.GetDependencies(LoadingSceneRoot.ScenePath, true);
            Assert.That(dependencies, Does.Contain("Assets/Prefabs/Panels/Panel_Loading.prefab"));
            Assert.That(dependencies.Any(path => path.Contains("EditorScenes") || path.Contains("GameContent.asset")),
                Is.False);
            Assert.That(dependencies.Where(path => path.EndsWith(".png", StringComparison.Ordinal)), Is.EquivalentTo(
                new[] { "Assets/Art/Loading/T_Loading_Elements.png", "Assets/Art/Loading/T_Loading_Background.png",
                    "Assets/Art/UI/T_UI_LevelControls.png" }));
            Assert.That(dependencies, Does.Contain("Assets/Art/Loading/T_WeChat_Cover.jpg"));
        }

        [Test]
        public void StaticArtworkHasOriginalAnimalsCompleteBindingsAndSafeArea()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_Loading.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null);
            var motion = prefab.GetComponentInChildren<LoadingArtworkMotion>(true);
            var entry = prefab.GetComponentInChildren<Button>(true);
            Assert.That(entry.gameObject.activeSelf, Is.False);
            Assert.That(entry.GetComponentInParent<SafeAreaView>(true), Is.Not.Null);
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(motion.OrbitAnimalCount, Is.EqualTo(13));
            Assert.That(motion.DoorAnimalCount, Is.EqualTo(2));
            Assert.That(prefab.GetComponentInChildren<SafeAreaView>(true), Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1080, 1920)));
            foreach (var component in new MonoBehaviour[] { motion, prefab.GetComponent<LoadingView>() })
            {
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(property.objectReferenceValue, Is.Not.Null, property.propertyPath);
            }
            foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
            {
                Assert.That(image.raycastTarget, Is.EqualTo(image == entry.targetGraphic));
                Assert.That(image.sprite, Is.Not.Null, image.name + " must retain its imported sprite identity.");
            }
            Assert.That(prefab.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(35));
            foreach (string name in new[] { "T_Loading_Elements.png", "T_Loading_Background.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/Loading/" + name);
                Assert.That(importer.mipmapEnabled || importer.isReadable, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }
    }
}
