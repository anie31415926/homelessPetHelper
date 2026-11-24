#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationCatExpressionTests
    {
        private const string Actor = "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab";

        [Test]
        public void ProjectionReadsSnapshotWithoutChangingFactsAndDistressTakesPriority()
        {
            var animal = Animal(AnimalEmotion.Happy);
            var before = animal.Current;
            Assert.That(AnimalExpression.From(before).TailRaise, Is.EqualTo(1));
            Assert.That(animal.Current, Is.SameAs(before));
            Assert.That(AnimalExpression.From(Animal(AnimalEmotion.Irritated).Current).TailSwish, Is.EqualTo(1));
            Assert.That(AnimalExpression.From(Animal(AnimalEmotion.Happy, AnimalStress.Acute).Current).TailRaise,
                Is.Zero);
            Assert.That(AnimalExpression.From(null).TailRaise, Is.Zero);
            Assert.That(AnimalExpression.From(Animal(AnimalEmotion.Relaxed).Current).TailSwish, Is.Zero);
            var blend = new AnimalExpression(0, 1).MoveTowards(new AnimalExpression(1, 0), .25f);
            Assert.That(blend.TailRaise, Is.EqualTo(.25f));
            Assert.That(blend.TailSwish, Is.EqualTo(.75f));
            Assert.That((int)AnimalEmotion.Startled, Is.EqualTo(3), "Retain saved enum values.");
        }

        [UnityTest]
        public IEnumerator TailKeepsRootAndBodyAcrossExpressionsAndRendersBothStates()
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Actor));
            var surface = new GameObject("ExpressionSurface", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage));
            var renderer = actor.GetComponent<StationHabitatRenderer>();
            var tail = actor.GetComponent<StationCatTail>();
            var image = surface.GetComponent<RawImage>();
            renderer.Bind(image);
            try
            {
                renderer.Render(HabitatAction.Sit, 1, true);
                Vector3 join = tail.Tail.localPosition + tail.Tail.localRotation * tail.JoinLocal;
                var mesh = actor.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
                Quaternion first = Quaternion.identity;
                string folder = Path.GetFullPath("StationCatExpressionEvidence");
                Directory.CreateDirectory(folder);
                for (int state = 0; state < 2; state++)
                {
                    var expression = AnimalExpression.From(Animal(state == 0 ? AnimalEmotion.Irritated :
                        AnimalEmotion.Happy).Current);
                    for (int frame = 0; frame <= 24; frame++)
                    {
                        renderer.Render(HabitatAction.Sit, 1, true, false, expression, frame / 30f);
                        Assert.That(Vector3.Distance(tail.Tail.localPosition + tail.Tail.localRotation *
                            tail.JoinLocal, join), Is.LessThan(.00001f), "Tail detached.");
                        Assert.That(actor.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh, Is.SameAs(mesh));
                        if (frame == 6) first = tail.Tail.localRotation;
                        if (frame == 18 && state == 0)
                            Assert.That(Quaternion.Angle(first, tail.Tail.localRotation), Is.GreaterThan(30));
                        yield return null;
                        renderer.GetComponentInChildren<Camera>().Render();
                        Capture((RenderTexture)image.texture, Path.Combine(folder,
                            (state == 0 ? "irritated-" : "happy-") + frame.ToString("D3") + ".png"));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(actor);
                UnityEngine.Object.Destroy(surface);
            }
        }

        [Test]
        public void ExpressionSamplingAllocationAndCpuCostAreRecorded()
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Actor));
            try
            {
                var player = actor.GetComponent<StationCatPlayer>();
                var expression = AnimalExpression.From(Animal(AnimalEmotion.Irritated).Current);
                for (int i = 0; i < 10; i++) player.Sample(HabitatAction.Sit, 1, false, expression, i * .01f);
                var timer = new System.Diagnostics.Stopwatch();
                long before = GC.GetAllocatedBytesForCurrentThread();
                timer.Start();
                for (int i = 0; i < 1000; i++) player.Sample(HabitatAction.Sit, 1, false, expression, i * .01f);
                timer.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Debug.Log("Cat 1000 native samples + tail expressions: managedBytes=" + allocated +
                    ", milliseconds=" + timer.Elapsed.TotalMilliseconds + "; Windows Editor CPU, no camera.");
                Assert.That(allocated, Is.Zero);
            }
            finally { UnityEngine.Object.Destroy(actor); }
        }

        [UnityTest]
        public IEnumerator SceneRebindUsesSameAnimalAndPauseFreezesTailAndBlend()
        {
            var panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_StationLoop.prefab"));
            var scene = panel.GetComponentInChildren<StationSceneView>(true);
            var cell = scene.Cells[0];
            var animal = Animal(AnimalEmotion.Irritated);
            var art = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Station/T_Station_CatTemporary.png");
            try
            {
                Assert.That(art, Is.Not.Null);
                Show(scene, animal.Current, art);
                yield return null;
                yield return null;
                var tail = UnityEngine.Object.FindObjectOfType<StationCatTail>();
                Quaternion first = tail.Tail.localRotation;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Quaternion.Angle(first, tail.Tail.localRotation), Is.GreaterThan(1));
                scene.SetSuspended(true);
                // Finish the current frame's LateUpdate before comparing frozen transforms.
                yield return null;
                first = tail.Tail.localRotation;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(tail.Tail.localRotation, Is.EqualTo(first));
                var previous = animal.Current;
                animal.TryApplyDisposition(previous, new AnimalDisposition(false, AnimalEmotion.Happy,
                    AnimalStress.None, AnimalBehavior.Settled));
                Show(scene, animal.Current, art);
                yield return null;
                Assert.That(tail.Tail.localRotation, Is.EqualTo(first), "Rebind must not animate while paused.");
                scene.SetSuspended(false);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(cell.AnimalId, Is.EqualTo(animal.Current.AnimalId));
                Assert.That(Quaternion.Angle(first, tail.Tail.localRotation), Is.GreaterThan(10));
                scene.SetPresentationVisible(false);
                yield return null;
                Assert.That(cell.MotionImage.texture, Is.Null);
            }
            finally { UnityEngine.Object.Destroy(panel); }
            yield return null;
        }

        private static void Show(StationSceneView scene, AnimalSnapshot animal, Sprite sprite) => scene.Show(
            new[] { new StationSceneResident(animal.AnimalId, 0, "Cat", sprite, true, true, animal) }, -1);

        private static AnimalIndividual Animal(AnimalEmotion emotion, AnimalStress stress = AnimalStress.None) =>
            new AnimalIndividual(Guid.NewGuid(), "cat", new AnimalDisposition(false, emotion, stress,
                AnimalBehavior.Settled), new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()),
                coat: new AnimalCoatCondition(20, 30));

        private static void Capture(RenderTexture target, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
        }
    }
}
#endif
