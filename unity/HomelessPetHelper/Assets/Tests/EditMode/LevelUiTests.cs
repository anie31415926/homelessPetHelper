using System;
using System.Linq;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.Persistence;
using System.Collections.Generic;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Guidance;
using HomelessPetHelper.Game.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using HomelessPetHelper.Game.Approach;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class LevelUiTests
    {
        private static GuideSession NewGuide() => new GuideSession("mechanic.v1", new[]
        {
            new GuideStep("select", "select", new[] { "select" }),
            new GuideStep("commit", "commit", new[] { "select", "commit" })
        });

        [Test]
        public void NestedPopupsCloseOnlyTheTopAndRejectDuplicateAndUnownedChildren()
        {
            var stack = new PopupStack<LevelPopupKind>(3, 2);
            Assert.That(stack.TryOpen(LevelPopupKind.Pause), Is.True);
            Assert.That(stack.TryOpen(LevelPopupKind.Pause), Is.False);
            Assert.That(stack.TryPush(LevelPopupKind.Objectives, LevelPopupKind.Settings), Is.False);
            Assert.That(stack.TryPush(LevelPopupKind.Pause, LevelPopupKind.Settings), Is.True);
            Assert.That(stack.TryPush(LevelPopupKind.Settings, LevelPopupKind.Confirmation), Is.True);
            Assert.That(stack.TryPush(LevelPopupKind.Confirmation, LevelPopupKind.Objectives), Is.False);
            Assert.That(stack.TryClose(LevelPopupKind.Pause), Is.False);
            Assert.That(stack.Depth, Is.EqualTo(3));
            Assert.That(stack.TryClose(LevelPopupKind.Confirmation), Is.True);
            Assert.That(stack.Top, Is.EqualTo(LevelPopupKind.Settings));
            Assert.That(stack.TryClose(LevelPopupKind.Settings), Is.True);
            Assert.That(stack.Top, Is.EqualTo(LevelPopupKind.Pause));
        }

        [Test]
        public void IndependentRequestsAreBoundedPriorityOrderedAndStableForEqualPriority()
        {
            var stack = new PopupStack<LevelPopupKind>(4, 2);
            stack.TryOpen(LevelPopupKind.Pause);
            Assert.That(stack.TryOpen(LevelPopupKind.Objectives, 1), Is.True);
            Assert.That(stack.TryOpen(LevelPopupKind.Settings, 2), Is.True);
            Assert.That(stack.TryOpen(LevelPopupKind.Confirmation, 3), Is.False);
            stack.TryClose(LevelPopupKind.Pause);
            Assert.That(stack.Top, Is.EqualTo(LevelPopupKind.Settings));
            stack.TryClose(LevelPopupKind.Settings);
            Assert.That(stack.Top, Is.EqualTo(LevelPopupKind.Objectives));
            stack.Clear();
            Assert.That(stack.Top, Is.Null);
            Assert.That(stack.PendingCount, Is.Zero);
            stack.TryOpen(LevelPopupKind.Pause);
            stack.TryOpen(LevelPopupKind.Settings, 1);
            stack.TryOpen(LevelPopupKind.Objectives, 1);
            stack.TryClose(LevelPopupKind.Pause);
            Assert.That(stack.Top, Is.EqualTo(LevelPopupKind.Settings));
            Assert.Throws<ArgumentOutOfRangeException>(() => stack.TryOpen((LevelPopupKind)99));
        }

        [Test]
        public void GuideAllowsOnlyCurrentActionsAndOnlySuccessfulCompletionAdvancesOneStep()
        {
            var guide = NewGuide();
            Assert.That(guide.Allows("commit"), Is.False);
            Assert.That(guide.ReportSucceeded("commit"), Is.False);
            Assert.That(guide.StepIndex, Is.Zero);
            Assert.That(guide.ReportSucceeded("select"), Is.True);
            Assert.That(guide.ReportSucceeded("select"), Is.False);
            Assert.That(guide.IsComplete, Is.False);
            Assert.That(guide.ReportSucceeded("commit"), Is.True);
            Assert.That(guide.IsComplete, Is.True);
            Assert.That(guide.ReportSucceeded("commit"), Is.False);
            Assert.That(guide.Allows("other"), Is.True);
        }

        [Test]
        public void InterruptedGuideCannotBeRecordedAsCompleted()
        {
            var storage = new MemorySaveStorage();
            var progress = new GuidanceProgressSession(storage);
            var guide = NewGuide();
            Assert.That(progress.TryRecordCompleted(guide), Is.False);
            guide.ReportSucceeded("select");
            guide.Cancel();
            Assert.That(guide.ReportSucceeded("commit"), Is.False);
            Assert.That(progress.TryRecordCompleted(guide), Is.False);
            Assert.That(storage.Writes, Is.Zero);
        }

        [Test]
        public void CompletedMechanicSurvivesReloadWithoutPersistingAnyGameplayAndVersionIsIndependent()
        {
            var storage = new MemorySaveStorage();
            var progress = new GuidanceProgressSession(storage);
            var guide = NewGuide();
            guide.ReportSucceeded("select"); guide.ReportSucceeded("commit");
            Assert.That(progress.TryRecordCompleted(guide), Is.True);
            int writes = storage.Writes;
            Assert.That(progress.TryRecordCompleted(guide), Is.True);
            Assert.That(storage.Writes, Is.EqualTo(writes));
            var restored = new GuidanceProgressSession(storage);
            Assert.That(restored.HasCompleted("mechanic.v1"), Is.True);
            Assert.That(restored.HasCompleted("mechanic.v2"), Is.False);
            Assert.That(string.Join("", storage.Slots), Does.Not.Contain("main.station"));
        }

        [Test]
        public void FailedSaveCanRetryButFailedLoadPreservesOriginalData()
        {
            var storage = new MemorySaveStorage();
            var progress = new GuidanceProgressSession(storage);
            var guide = NewGuide();
            guide.ReportSucceeded("select"); guide.ReportSucceeded("commit");
            storage.FailWrites = true;
            Assert.That(progress.TryRecordCompleted(guide), Is.False);
            Assert.That(progress.HasCompleted("mechanic.v1"), Is.False);
            storage.FailWrites = false;
            Assert.That(progress.TryRecordCompleted(guide), Is.True);
            var slots = (string[])storage.Slots.Clone();
            storage.FailReads = true;
            var failedLoad = new GuidanceProgressSession(storage);
            Assert.That(failedLoad.TryRecordCompleted(guide), Is.False);
            Assert.That(storage.Slots, Is.EqualTo(slots));
        }

        [Test]
        public void MalformedLearningSectionIsPreservedInsteadOfBecomingEmptyProgress()
        {
            var storage = new MemorySaveStorage();
            var saves = new SaveService(storage);
            var malformed = new SaveSection<Dictionary<string, int>>("guidance.completed", 1,
                () => new Dictionary<string, int>(), data => null);
            saves.Register(malformed);
            Assert.That(saves.TryOpen(out _, out _), Is.True);
            Assert.That(saves.TryCommit(writer => writer.Set(malformed, new Dictionary<string, int>()), out _),
                Is.True);
            var original = (string[])storage.Slots.Clone();
            var progress = new GuidanceProgressSession(storage);
            Assert.That(progress.PersistenceError, Is.Not.Null);
            var guide = NewGuide();
            guide.ReportSucceeded("select"); guide.ReportSucceeded("commit");
            Assert.That(progress.TryRecordCompleted(guide), Is.False);
            Assert.That(storage.Slots, Is.EqualTo(original));
        }

        [Test]
        public void AuthoredGuideCatalogCreatesIndependentStepsWithLocalizedCopy()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var catalog = content.CreateGuides();
            var first = catalog.Create("field.yard.cleanup");
            var second = catalog.Create("field.yard.cleanup");
            first.ReportSucceeded(first.Current.CompletionAction);
            Assert.That(second.StepIndex, Is.Zero);
            Assert.That(catalog.Create("unknown"), Is.Null);
            Assert.Throws<ArgumentException>(() => new GuideStep("key", "done", new[] { "other" }));
        }

        [Test]
        public void EmptyVolumeRailStaysVisibleAgainstTheOffWhiteCard()
        {
            const string path = "Assets/Art/UI/T_UI_LevelControls.png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            var source = new Texture2D(2, 2);
            try
            {
                Assert.That(source.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                Color Sample(string name)
                {
                    var center = sprites.Single(sprite => sprite.name == name).rect.center;
                    return source.GetPixel(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
                }
                float paper = Luminance(Sample("Paper"));
                float track = Luminance(Sample("VolumeTrack"));
                float fill = Luminance(Sample("VolumeFill"));
                Assert.That((paper + .05f) / (track + .05f), Is.GreaterThan(1.35f),
                    "The empty rail must remain visible at zero volume.");
                Assert.That(fill, Is.LessThan(track), "The filled section must be darker than the empty rail.");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static float Luminance(Color color) =>
            .2126f * Mathf.GammaToLinearSpace(color.r) + .7152f * Mathf.GammaToLinearSpace(color.g) +
            .0722f * Mathf.GammaToLinearSpace(color.b);

        [Test]
        public void SharedUiArtUsesSmallNineSlicesAndNonReadableTexture()
        {
            const string path = "Assets/Art/UI/T_UI_LevelControls.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.That(sprites, Has.Length.EqualTo(14));
            foreach (string name in new[] { "Paper", "Action", "Secondary" })
            {
                var sprite = sprites.Single(item => item.name == name);
                Assert.That(sprite.border.x, Is.GreaterThan(0));
                Assert.That(sprite.border.y, Is.GreaterThan(0));
                Assert.That(sprite.rect.width, Is.LessThanOrEqualTo(256));
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture.width * texture.height * 4, Is.LessThan(1024 * 1024));
        }

        [Test]
        public void SavedPanelsShareTheOverlayWidgetAndEveryBoundLabelHasLocalizedText()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var text = content.CreateLevelUiText();
            foreach (string path in new[] { "Assets/Prefabs/Panels/Panel_Approach.prefab",
                "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab" })
            {
                var panel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var view = panel.GetComponentInChildren<LevelOverlayView>(true);
                Assert.That(view, Is.Not.Null, path);
                var serialized = new SerializedObject(view);
                Assert.That(view.transform.parent, Is.SameAs(panel.transform),
                    "The shared overlay must span the panel Canvas, outside the scene safe area.");
                var backdrop = (GameObject)serialized.FindProperty("_modalBackdrop").objectReferenceValue;
                Assert.That(backdrop.GetComponentInParent<SafeAreaView>(true), Is.Null);
                Assert.That(backdrop.GetComponentInParent<Mask>(true), Is.Null);
                Assert.That(backdrop.GetComponentInParent<RectMask2D>(true), Is.Null);
                Assert.That(backdrop.GetComponent<Image>().color, Is.EqualTo((Color)new Color32(0, 0, 0, 88)));
                var modal = (GameObject)serialized.FindProperty("_modalRoot").objectReferenceValue;
                Assert.That(modal.GetComponent<Image>(), Is.Null, "Pages must not add a second dim layer.");
                Assert.That(modal.GetComponentInParent<SafeAreaView>(true), Is.Not.Null,
                    "Foreground content still belongs to the safe area.");
                var property = serialized.GetIterator();
                while (property.NextVisible(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.name.StartsWith("_", StringComparison.Ordinal))
                        Assert.That(property.objectReferenceValue, Is.Not.Null, property.name);
                var labels = serialized.FindProperty("_localizedLabels");
                var keys = serialized.FindProperty("_labelKeys");
                Assert.That(labels.arraySize, Is.EqualTo(keys.arraySize));
                for (int index = 0; index < labels.arraySize; index++)
                {
                    Assert.That(labels.GetArrayElementAtIndex(index).objectReferenceValue, Is.InstanceOf<Text>());
                    Assert.That(text.Get(keys.GetArrayElementAtIndex(index).stringValue), Is.Not.Empty);
                }
                Assert.That(panel.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null);
                var approach = panel.GetComponent<ApproachPanelView>();
                if (approach != null) Assert.That(approach.Overlays, Is.SameAs(view));
                else Assert.That(panel.GetComponent<LevelOverlayAnchor>().View, Is.SameAs(view));
            }
        }
    }
}
