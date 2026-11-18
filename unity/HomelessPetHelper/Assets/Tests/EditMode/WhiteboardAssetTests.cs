using System.Linq;
using HomelessPetHelper.Modules.Rescue.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class WhiteboardAssetTests
    {
        [TestCase("Assets/Prefabs/Panels/Panel_OptionWhiteboard.prefab")]
        [TestCase("Assets/Prefabs/Widgets/Widget_OptionCard.prefab")]
        public void SavedPrefabsHaveNoMissingScriptsOrBindings(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero);
            }

            foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!(component is OptionPanelView) && !(component is OptionCardView))
                {
                    continue;
                }

                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (component is OptionCardView && iterator.propertyPath == "_hold")
                        {
                            Assert.That(iterator.objectReferenceValue, Is.Null,
                                "Legacy selection cards must not opt in to hold confirmation.");
                            continue;
                        }
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null,
                            path + ": " + iterator.propertyPath);
                    }
                }
            }
        }

        [Test]
        public void PreviewIsExcludedFromPlayerAndUsesReplaceableImages()
        {
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path.Contains("EditorScenes")), Is.False);
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_OptionWhiteboard.prefab");
            Assert.That(panel.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1080, 1920)));
            Assert.That(panel.GetComponentsInChildren<SafeAreaView>(true).Length, Is.EqualTo(1));
            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Widgets/Widget_OptionCard.prefab");
            Assert.That(card.GetComponentsInChildren<Image>(true).Any(image => image.name == "Artwork"), Is.True);
            Assert.That(card.GetComponent<Image>().type, Is.EqualTo(Image.Type.Sliced));
        }
    }
}
