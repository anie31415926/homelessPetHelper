using System.Linq;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueCaseAssetTests
    {
        [Test]
        public void SavedTrialPrefabHasCompleteBindingsAndIsNotInPlayer()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_RescueCase.prefab");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent<RescueCaseView>(), Is.Not.Null);
            Assert.That(root.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1080, 1920)));
            Assert.That(root.GetComponentsInChildren<SafeAreaView>(true).Length, Is.EqualTo(1));
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path.Contains("EditorScenes")), Is.False);
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero);
            }

            foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(component is RescueCaseView) && !(component is OptionPanelView))
                {
                    continue;
                }

                var fields = new SerializedObject(component).GetIterator();
                while (fields.NextVisible(true))
                {
                    if (fields.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        Assert.That(fields.objectReferenceValue, Is.Not.Null, fields.propertyPath);
                    }
                }
            }
        }

        [Test]
        public void CompactCaseHasSeparateArtworkAndHoldControlsWithoutChangingLegacyWhiteboard()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_RescueCase.prefab");
            var fields = new SerializedObject(root.GetComponent<OptionPanelView>());
            Assert.That(fields.FindProperty("_holdToConfirm").boolValue, Is.True);
            var use = (Button)fields.FindProperty("_use").objectReferenceValue;
            Assert.That(use.gameObject.activeSelf, Is.False);
            var card = (OptionCardView)fields.FindProperty("_cardPrefab").objectReferenceValue;
            var hold = card.GetComponentInChildren<HoldActionButton>(true);
            Assert.That(hold, Is.Not.Null);
            Assert.That(new SerializedObject(hold).FindProperty("_progress").objectReferenceValue, Is.Not.Null);
            var progress = (Image)new SerializedObject(hold).FindProperty("_progress").objectReferenceValue;
            Assert.That(progress.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(progress.enabled, Is.False);
            Assert.That(progress.rectTransform.anchorMax.x, Is.Zero);
            Assert.That(new SerializedObject(hold).FindProperty("_holdSeconds").floatValue, Is.EqualTo(0.4f));
            foreach (string name in new[] { "Backdrop", "Cat" })
                Assert.That(root.GetComponentsInChildren<Image>(true).Single(item => item.name == name).sprite,
                    Is.Not.Null);
            var legacy = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_OptionWhiteboard.prefab");
            Assert.That(legacy.GetComponent<OptionPanelView>().HoldToConfirm, Is.False);
            Assert.That(AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true)
                .Any(path => path.Contains("RescueCaseArt") || path.Contains("Panel_RescueCase")), Is.False);
        }
    }
}
