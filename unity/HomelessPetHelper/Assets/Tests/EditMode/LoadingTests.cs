using System;
using System.Linq;
using HomelessPetHelper.Game.Loading;
using HomelessPetHelper.Modules.Animals.UI;
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
        public void LoadingIllustrationRetainsEveryReviewedAnimalSlice()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_Loading.prefab");
            var data = new SerializedObject(prefab.GetComponent<LoadingView>());
            var artwork = (GameObject)data.FindProperty("_previewArtwork").objectReferenceValue;
            var progress = (GameObject)data.FindProperty("_progress").objectReferenceValue;
            Assert.That(artwork.GetComponentsInChildren<PetPortraitView>(true), Is.Empty,
                "Loading is a multi-species illustration, not a main-story pet portrait surface.");
            Assert.That(progress.GetComponentsInChildren<PetPortraitView>(true), Is.Empty);
            string[] expected = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Loading/T_Loading_Elements.png")
                .OfType<Sprite>().Select(sprite => sprite.name)
                .Where(name => name.StartsWith("Orbit", StringComparison.Ordinal) ||
                    name.StartsWith("Door", StringComparison.Ordinal) || name == "ProgressCat").ToArray();
            Assert.That(expected.Length, Is.EqualTo(16));
            var images = artwork.GetComponentsInChildren<Image>(true)
                .Concat(progress.GetComponentsInChildren<Image>(true)).ToArray();
            Assert.That(images.Where(image => image.sprite != null && expected.Contains(image.sprite.name))
                .Select(image => image.sprite.name), Is.EquivalentTo(expected));
            Assert.That(prefab.GetComponentsInChildren<PetPortraitView>(true).Length, Is.EqualTo(2),
                "Only the two title-cover pets keep the current animated portraits.");
        }

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
            Assert.That(dependencies, Does.Contain("Assets/Art/PetMotion/T_CatPortraitMotion.png"));
            Assert.That(dependencies, Does.Contain("Assets/Art/PetSit/T_Station_ShibaMotion.png"));
            Assert.That(dependencies.Any(path => path.EndsWith(".anim", StringComparison.Ordinal) ||
                path.EndsWith("/Widget_PetMotion.prefab", StringComparison.Ordinal)), Is.False);
            Assert.That(dependencies, Does.Not.Contain("Assets/Art/Loading/T_WeChat_Cover.jpg"));
        }

        [Test]
        public void ArtworkKeepsMultiSpeciesLoadingAndAnimatedCoverWithSafeArea()
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
            Assert.That(prefab.GetComponentsInChildren<PetPortraitView>(true).Length, Is.EqualTo(2));
            Assert.That(prefab.GetComponentInChildren<SafeAreaView>(true), Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1080, 1920)));
            foreach (var component in new MonoBehaviour[] { motion, prefab.GetComponent<LoadingView>() })
            {
                var property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(property.objectReferenceValue, Is.Not.Null, property.propertyPath);
            }
            var buttons = prefab.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length, Is.EqualTo(11));
            foreach (var name in new[] { "DeleteSlot1", "DeleteSlot2", "DeleteSlot3", "ConfirmDelete" })
            {
                var button = buttons.Single(candidate => candidate.name == name);
                Assert.That(button.gameObject.activeSelf, Is.False);
                Assert.That(button.GetComponentInParent<SafeAreaView>(true), Is.Not.Null);
            }
            foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
            {
                bool backdrop = image.name == "SaveBackdrop";
                Assert.That(image.raycastTarget, Is.EqualTo(backdrop || buttons.Any(button => button.targetGraphic == image)));
                if (backdrop) continue;
                if (image.GetComponent<PetPortraitView>() == null)
                    Assert.That(image.sprite, Is.Not.Null, image.name + " must retain its imported sprite identity.");
                else Assert.That(image.sprite, Is.Null, image.name + " must not retain a replaced animal sprite.");
            }
            foreach (string name in new[] { "T_Loading_Elements.png", "T_Loading_Background.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/Loading/" + name);
                Assert.That(importer.mipmapEnabled || importer.isReadable, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }
    }
}
