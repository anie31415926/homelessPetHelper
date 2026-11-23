using System;
using System.Linq;
using HomelessPetHelper.Modules.Station.Habitat;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Framework.UI.Motion;

namespace HomelessPetHelper.Tests.StationHabitat.EditMode
{
    public sealed class StationRoomTests
    {
        private static StationHabitatArea Area() => new StationHabitatArea(new HabitatPoint(0, 0),
            new HabitatPoint(1, 1), new HabitatPoint(.1, .1), new HabitatPoint(.8, .1),
            new[] { new HabitatPoint(.5, .5) }, new[] { new HabitatPoint(.2, .1) });

        [Test]
        public void GroundFootprintRejectsCenterCrossingTangencyAndInvalidCoordinates()
        {
            var area = Area();
            Assert.That(area.Contains(new HabitatPoint(.5, .5)), Is.False);
            Assert.That(area.CanTraverse(new HabitatPoint(.1, .5), new HabitatPoint(.9, .5)), Is.False);
            Assert.That(area.CanTraverse(new HabitatPoint(.1, .6), new HabitatPoint(.9, .6)), Is.False);
            Assert.That(area.CanTraverse(new HabitatPoint(.1, .7), new HabitatPoint(.9, .7)), Is.True);
            Assert.That(area.Contains(new HabitatPoint(double.NaN, .1)), Is.False);
            Assert.That(area.CanTraverse(new HabitatPoint(-.1, .1), new HabitatPoint(.1, .1)), Is.False);
            Assert.Throws<ArgumentException>(() => new StationHabitatArea(new HabitatPoint(0, 0),
                new HabitatPoint(1, 1), new HabitatPoint(.5, .5), new HabitatPoint(.1, .1),
                new[] { new HabitatPoint(.5, .5) }, new[] { new HabitatPoint(.2, .1) }));
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(-12)]
        public void EverySegmentAndDestinationInLongSessionRemainClear(int seed)
        {
            var area = Room().CreateArea();
            var motion = new StationHabitatMotion(seed, area);
            int walking = 0;
            for (int i = 0; i < 30000; i++)
            {
                var previous = motion.Position;
                motion.Tick(.05);
                Assert.That(area.CanTraverse(previous, motion.Position), Is.True);
                if (motion.Action != HabitatAction.Walk) continue;
                walking++;
                Assert.That(area.CanTraverse(motion.Position, motion.Destination), Is.True);
            }
            Assert.That(walking, Is.GreaterThan(1000), "Blocked destinations must not freeze all wandering.");
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) motion.Tick(.05);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void StationUsesSharedSkinAndIndependentVisualFeedbackWithoutAdditionalAudioOwners()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_StationLoop.prefab");
            var scope = root.GetComponent<MotionScope>();
            Assert.That(scope, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(scope.Library), Is.EqualTo("Assets/Settings/UI/LevelMotion.asset"));
            Assert.That(root.GetComponentsInChildren<AudioScope>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            foreach (var button in root.GetComponentsInChildren<Button>(true).Where(button =>
                new[] { "Back", "Clean", "Groom", "Check", "Pet", "Finish", "History", "StationMenu",
                    "CloseStationMenu", "Restart", "Enter" }.Contains(button.name)))
            {
                Assert.That(button.GetComponent<ButtonAudio>(), Is.Not.Null, button.name);
                var motion = button.GetComponent<UiMotionFeedback>();
                Assert.That(motion, Is.Not.Null, button.name);
                var visual = (RectTransform)new SerializedObject(motion).FindProperty("_visual").objectReferenceValue;
                Assert.That(visual.parent, Is.SameAs(button.transform));
                Assert.That(button.targetGraphic.transform, Is.SameAs(visual));
                Assert.That(button.targetGraphic.raycastTarget, Is.False);
                var image = (Image)button.targetGraphic;
                Assert.That(AssetDatabase.GetAssetPath(image.sprite),
                    Is.EqualTo("Assets/Art/UI/T_UI_LevelControls.png"));
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(button.GetComponent<Image>().color.a, Is.Zero);
            }
            Assert.That(root.GetComponentsInChildren<StationPopupPaper>(true).Length, Is.EqualTo(3));
            var backdrops = root.GetComponentsInChildren<Image>(true).Where(image => image.color ==
                (Color)new Color32(0, 0, 0, 88)).ToArray();
            Assert.That(backdrops.Length, Is.EqualTo(1));
            Assert.That(backdrops[0].transform.parent.parent, Is.SameAs(root.transform));
        }

        [Test]
        public void SavedRoomUsesIndependentArtLayersOneCoordinateSpaceAndNoRaycastBlockers()
        {
            var room = Room();
            Assert.That(room, Is.Not.Null);
            var fit = room.GetComponent<AspectRatioFitter>();
            Assert.That(fit.aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Assert.That(fit.aspectRatio, Is.EqualTo(336f / 600).Within(.0001f));
            Assert.That(room.GetComponentsInChildren<Image>(true).Count(image =>
                image.name.StartsWith("Furniture_", StringComparison.Ordinal)), Is.EqualTo(5));
            Assert.That(room.GetComponentsInChildren<StationRoomObstacle>(true).Length, Is.EqualTo(5));
            foreach (var image in room.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("Furniture_", StringComparison.Ordinal) ||
                    image.name == "RoomBackground"))
            {
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.raycastTarget, Is.False);
            }
            var scene = room.GetComponentInParent<StationSceneView>(true);
            foreach (var cell in scene.Cells) Assert.That(cell.transform.parent, Is.SameAs(room.Content));
            var area = room.CreateArea();
            Assert.That(area.Contains(area.Start), Is.True);
            Assert.That(area.Contains(area.Rest), Is.True);
            Assert.That(area.Contains(new HabitatPoint(.5, .35)), Is.False);
        }

        private static StationRoomView Room() => AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Panels/Panel_StationLoop.prefab").GetComponentInChildren<StationRoomView>(true);
    }
}
