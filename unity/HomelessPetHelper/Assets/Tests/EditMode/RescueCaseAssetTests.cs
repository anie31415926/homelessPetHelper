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
    }
}
