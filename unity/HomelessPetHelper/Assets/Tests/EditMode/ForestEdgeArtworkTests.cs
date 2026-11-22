using System;
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
        public void EveryConfiguredPointHasDistinctArtworkAndForestIsOutsideMainDependencies()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(ContentPath);
            Assert.DoesNotThrow(content.Validate);
            var points = content.CreateMapConfig().Definition.Points;
            Assert.That(points.Count, Is.EqualTo(6));
            Sprite[] pictures = points.Select(point => content.GetOutingBackground(point.PointId)).ToArray();
            Assert.That(pictures.Distinct().Count(), Is.EqualTo(points.Count));
            string[] dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            foreach (Sprite picture in pictures)
            {
                string path = AssetDatabase.GetAssetPath(picture);
                Assert.That(dependencies, Does.Contain(path));
                Assert.That(picture.rect.size, Is.EqualTo(new Vector2(768, 1024)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.isReadable || importer.mipmapEnabled, Is.False);
            }
            const string forest = "Assets/Art/CityMap/T_CityMap_Forest.jpg";
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(forest), Is.Not.Null);
            Assert.That(dependencies, Does.Not.Contain(forest));
            Assert.That(dependencies.Any(path => path.Contains("assembled")), Is.False);
            Assert.Throws<ArgumentException>(() => content.GetOutingBackground("unknown-point"));
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("missing-sprite")]
        [TestCase("missing-point")]
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
                else entries.arraySize--;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(() => content.Validate(), Throws.Exception);
            }
            finally { UnityEngine.Object.DestroyImmediate(content); }
        }
    }
}
