#if UNITY_EDITOR
using System;
using System.Collections;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HomelessPetHelper.Tests.StationHabitat.PlayMode
{
    public sealed class StationAppearanceTests
    {
        [UnityTest]
        public IEnumerator ExplicitAppearanceIsSelectedAndReleasedWithoutReplacingAnimalFacts()
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab");
            var alternative = UnityEngine.Object.Instantiate(original);
            alternative.SetActive(false);
            var alternativePlayer = alternative.GetComponent<StationCatPlayer>();
            var fields = new SerializedObject(alternativePlayer);
            fields.FindProperty("_appearanceId").stringValue = "test.alternative";
            fields.ApplyModifiedPropertiesWithoutUndo();
            var panel = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Panels/Panel_StationLoop.prefab"));
            var scene = panel.GetComponentInChildren<StationSceneView>(true);
            var art = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PetMotion/T_Pet_original.png");
            var animal = new AnimalIndividual(Guid.NewGuid(), "cat", new AnimalDisposition(false,
                AnimalEmotion.Happy, AnimalStress.None, AnimalBehavior.Settled),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()), coat: new AnimalCoatCondition(20, 30));
            var snapshot = animal.Current;
            var cell = scene.Cells[0];
            try
            {
                scene.Show(new[] { new StationSceneResident(snapshot.AnimalId, 0, "Cat", art, false, true,
                    snapshot, alternative.GetComponent<StationHabitatRenderer>()) }, -1);
                yield return null;
                yield return null;
                var renderer = UnityEngine.Object.FindObjectOfType<StationHabitatRenderer>();
                // Display activation belongs to the view; the inactive template remains untouched.
                Assert.That(cell.MotionImage.texture, Is.Not.Null);
                Assert.That(renderer.Player.AppearanceId, Is.EqualTo("test.alternative"));
                var motion = cell.Motion;
                scene.Show(new[] { new StationSceneResident(snapshot.AnimalId, 0, "Cat", art, true, true,
                    snapshot) }, -1);
                yield return null;
                yield return null;
                renderer = UnityEngine.Object.FindObjectOfType<StationHabitatRenderer>();
                Assert.That(renderer.Player.AppearanceId, Is.EqualTo("cat.orange.side"));
                Assert.That(cell.Motion, Is.SameAs(motion));
                Assert.That(animal.Current, Is.SameAs(snapshot));
                scene.SetPresentationVisible(false);
                yield return null;
                Assert.That(cell.MotionImage.texture, Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(panel);
                UnityEngine.Object.Destroy(alternative);
            }
        }
    }
}
#endif
