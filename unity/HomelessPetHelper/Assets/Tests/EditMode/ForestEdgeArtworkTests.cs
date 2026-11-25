using System.Linq;
using HomelessPetHelper.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class ForestEdgeArtworkTests
    {
        private const string ContentPath = "Assets/Settings/Game/GameContent.asset";

        [Test]
        public void OnlyAuthoredScenesAreBoundAndEveryPointFallsBackToTheFirstScene()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            Assert.DoesNotThrow(content.Validate);
            var points = content.CreateMapConfig().Definition.Points;
            Assert.That(points.Count, Is.EqualTo(7));
            var bindings = new SerializedObject(content).FindProperty("_outingArtwork");
            Assert.That(bindings.arraySize, Is.EqualTo(2), "Only parking and treehouse keep an authored scene.");
            Sprite parking = content.GetOutingBackground("parking");
            Sprite treehouse = content.GetOutingBackground("treehouse");
            Assert.That(parking, Is.Not.Null);
            Assert.That(treehouse, Is.Not.Null);
            Assert.That(treehouse, Is.Not.SameAs(parking));
            string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            foreach (var point in points)
            {
                Sprite picture = content.GetOutingBackground(point.PointId);
                Assert.That(picture, Is.SameAs(point.PointId == "treehouse" ? treehouse : parking), point.PointId);
                string path = AssetDatabase.GetAssetPath(picture);
                Assert.That(dependencies, Does.Contain(path));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
            }
            Assert.That(content.GetOutingBackground("unknown-point"), Is.SameAs(parking));
            Assert.That(dependencies.Any(path => path.Contains("assembled")), Is.False);
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("missing-sprite")]
        [TestCase("no-fallback")]
        public void InvalidArtworkMappingIsRejectedBeforeStartingGame(string fault)
        {
            var content = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath));
            try
            {
                var serialized = new SerializedObject(content);
                var entries = serialized.FindProperty("_outingArtwork");
                var first = entries.GetArrayElementAtIndex(0);
                if (fault == "duplicate") entries.GetArrayElementAtIndex(1).FindPropertyRelative("_pointId")
                    .stringValue = first.FindPropertyRelative("_pointId").stringValue;
                else if (fault == "unknown") first.FindPropertyRelative("_pointId").stringValue = "unknown-point";
                else if (fault == "missing-sprite")
                    first.FindPropertyRelative("_background").objectReferenceValue = null;
                else entries.arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(() => content.Validate(), Throws.Exception);
            }
            finally { UnityEngine.Object.DestroyImmediate(content); }
        }
    }
}
