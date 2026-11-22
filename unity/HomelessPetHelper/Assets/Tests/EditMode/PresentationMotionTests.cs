using System;
using System.Linq;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Game.Field;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class PresentationMotionTests
    {
        private const string LibraryPath = "Assets/Settings/UI/LevelMotion.asset";
        private static MotionLibrary Library => AssetDatabase.LoadAssetAtPath<MotionLibrary>(LibraryPath);

        [Test]
        public void SharedPresetsAreFiniteContinuousAndEndWithoutChangingGameplay()
        {
            var library = Library;
            Assert.That(library, Is.Not.Null);
            library.Validate();
            foreach (MotionCue cue in Enum.GetValues(typeof(MotionCue)))
                for (int index = 0; index <= 100; index++)
                {
                    var pose = library.Sample(cue, index * .071f);
                    Assert.That(float.IsNaN(pose.Scale.x) || float.IsInfinity(pose.Offset.y), Is.False);
                    Assert.That(pose.Scale.x, Is.GreaterThan(0));
                    Assert.That(pose.Opacity, Is.InRange(0, 1));
                }
            foreach (var cue in new[] { MotionCue.Water, MotionCue.Foliage })
                Assert.That(library.Sample(cue, library.Duration(cue) - .0001f).AngleDegrees,
                    Is.EqualTo(library.Sample(cue, 0).AngleDegrees).Within(.001f));
            Assert.That(library.Sample(MotionCue.Clear, 10).Opacity, Is.Zero);
            Assert.That(library.Sample(MotionCue.Place, 10).Scale, Is.EqualTo(Vector2.one));
            Assert.Throws<ArgumentOutOfRangeException>(() => library.Sample(MotionCue.Hover, float.NaN));
        }

        [Test]
        public void WarmPresetSamplingRecordsCpuCostWithoutManagedAllocation()
        {
            var library = Library;
            library.Validate();
            for (int index = 0; index < 100; index++) library.Sample(MotionCue.Foliage, index * .01f);
            var timer = new System.Diagnostics.Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread();
            timer.Start();
            for (int index = 0; index < 100000; index++) library.Sample(MotionCue.Foliage, index * .01f);
            timer.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine("Motion 100000 warm preset samples: managedBytes=" + bytes +
                ", milliseconds=" + timer.Elapsed.TotalMilliseconds + "; Editor CPU only, not device performance.");
            Assert.That(bytes, Is.Zero);
        }

        [TestCase("durationSeconds", 0)]
        [TestCase("opacity", 2)]
        [TestCase("angleDegrees", float.PositiveInfinity)]
        public void InvalidConfigurationIsRejectedBeforePlayback(string field, float value)
        {
            var copy = UnityEngine.Object.Instantiate(Library);
            try
            {
                var serialized = new SerializedObject(copy);
                serialized.FindProperty("_definitions").GetArrayElementAtIndex(0)
                    .FindPropertyRelative(field).floatValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => copy.Validate());
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void DuplicateAndMissingCuesAreRejected()
        {
            var copy = UnityEngine.Object.Instantiate(Library);
            try
            {
                var serialized = new SerializedObject(copy);
                var definitions = serialized.FindProperty("_definitions");
                definitions.GetArrayElementAtIndex(1).FindPropertyRelative("cue").enumValueIndex = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => copy.Validate());
                definitions.arraySize = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => copy.Validate());
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void SavedPanelsExplicitlySharePresetsButKeepHitAndVisualTransformsSeparate()
        {
            foreach (string name in new[] { "Panel_Approach", "Panel_SiegeTrial" })
            {
                var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/" + name + ".prefab");
                var scope = panel.GetComponent<MotionScope>();
                Assert.That(scope.Library, Is.SameAs(Library));
                var terrain = panel.GetComponentInChildren<OutingFieldGraphic>(true);
                Assert.That(terrain.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(terrain.raycastTarget, Is.False);
                foreach (var motion in panel.GetComponentsInChildren<UiMotionFeedback>(true))
                {
                    var serialized = new SerializedObject(motion);
                    var control = (Selectable)serialized.FindProperty("_control").objectReferenceValue;
                    var visual = (RectTransform)serialized.FindProperty("_visual").objectReferenceValue;
                    Assert.That(visual.parent, Is.SameAs(control.transform));
                    Assert.That(control.targetGraphic.transform, Is.SameAs(visual));
                    Assert.That(visual.GetComponent<Image>().raycastTarget, Is.False);
                    Assert.That(control.GetComponent<Image>().raycastTarget, Is.True);
                    Assert.That(serialized.FindProperty("_scope").objectReferenceValue, Is.Not.Null);
                }
                Assert.That(panel.GetComponentsInChildren<MonoBehaviour>(true).Any(item => item == null), Is.False);
            }
        }
    }
}
