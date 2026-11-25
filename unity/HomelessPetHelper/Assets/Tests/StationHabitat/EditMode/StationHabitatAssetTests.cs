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
        private const string CatAtlasPath = "Assets/Art/PetSit/T_Cat_SitAtlas.png";

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
            RequireBindings(actor.GetComponent<StationCatPlayer>());
            Assert.That(actor.GetComponentInChildren<Camera>(true).cullingMask, Is.EqualTo(1 << 29));
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            Assert.That(dependencies.Any(x => x.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)),
                Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(x => x.path.Contains("StationHabitat")), Is.False);
            Assert.That(AssetDatabase.GetDependencies(
                "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab", true)
                .Count(x => x.EndsWith(".png", StringComparison.Ordinal)), Is.EqualTo(1));
            var skin = actor.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(AssetDatabase.GetAssetPath(skin.sharedMaterial.mainTexture),
                Is.EqualTo(CatAtlasPath));
            var importer = (TextureImporter)AssetImporter.GetAtPath(CatAtlasPath);
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
        }

        [Test]
        public void CatAtlasWebGlOverrideKeepsUncompressedAlphaAndSourceSize()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(CatAtlasPath);
            var webGl = importer.GetPlatformTextureSettings("WebGL");
            Assert.That(webGl.overridden, Is.True);
            Assert.That(webGl.format, Is.EqualTo(TextureImporterFormat.RGBA32));
            Assert.That(webGl.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(webGl.crunchedCompression || webGl.allowsAlphaSplitting, Is.False);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Assert.That(webGl.maxTextureSize, Is.GreaterThanOrEqualTo(Math.Max(width, height)));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
        }

        [TestCase("farForeleg", "farFrontToe")]
        [TestCase("nearForeleg", "nearFrontToe")]
        public void CatForelegPreservesToeInfluenceWithTwoBoneSkinning(string meshName, string toeName)
        {
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Widgets/Widget_StationHabitatActor.prefab");
            var renderer = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(skin => skin.name == meshName);
            int toe = Array.FindIndex(renderer.bones, bone => bone.name == toeName);
            Assert.That(toe, Is.GreaterThanOrEqualTo(0));
            var weights = renderer.sharedMesh.boneWeights;
            Assert.That(weights, Has.Length.EqualTo(renderer.sharedMesh.vertexCount));
            Assert.That(weights.All(weight => weight.weight0 >= weight.weight1 &&
                Mathf.Approximately(weight.weight0 + weight.weight1, 1) &&
                weight.weight2 == 0 && weight.weight3 == 0), Is.True);
            Assert.That(weights.Any(weight => weight.boneIndex0 == toe && weight.weight0 == 1), Is.True);
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
