using System;
using System.Linq;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.StationHabitat.EditMode
{
    public sealed class StationHabitatAssetTests
    {
        [Test]
        public void NewBindingsAreExplicitAndMainHasNoPreviewDependency()
        {
            var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/Panel_StationLoop.prefab");
            var view = panel.GetComponentInChildren<StationHabitatView>(true);
            Assert.That(view, Is.Not.Null);
            RequireBindings(view);
            var scene = panel.GetComponentInChildren<StationSceneView>(true);
            RequireBindings(scene);
            RequireBindings(panel.GetComponent<StationSceneChrome>());
            Assert.That(scene.Cells.Count, Is.EqualTo(4));
            foreach (var resident in scene.Cells) RequireBindings(resident);
            Assert.That(panel.GetComponents<MonoBehaviour>().Any(component =>
                component.GetType().Name == "AudioScope"), Is.True, "Keep the existing local audio owner.");
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab");
            RequireBindings(actor.GetComponent<StationHabitatRenderer>());
            Assert.That(actor.GetComponentInChildren<Camera>(true).cullingMask, Is.EqualTo(1 << 29));
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(dependencies.Any(x => x.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)),
                Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(x => x.path.Contains("StationHabitat")), Is.False);
            Assert.That(AssetDatabase.GetDependencies(
                "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab", true)
                .Count(x => x.EndsWith(".png", StringComparison.Ordinal)), Is.EqualTo(1));
        }

        private static void RequireBindings(MonoBehaviour view)
        {
            var field = new SerializedObject(view).GetIterator();
            while (field.NextVisible(true))
                if (field.propertyType == SerializedPropertyType.ObjectReference)
                    Assert.That(field.objectReferenceValue, Is.Not.Null, view.name + ": " + field.propertyPath);
        }
    }
}
