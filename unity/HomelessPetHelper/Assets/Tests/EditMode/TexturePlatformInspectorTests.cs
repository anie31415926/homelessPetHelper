using System.Collections;
using System.Reflection;
using HomelessPetHelper.Editor.ResourceAudit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class TexturePlatformInspectorTests
    {
        [Test]
        public void AuditThenOpeningTextureInspectorsKeepsInstalledPlatformPropertiesAvailable()
        {
            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(report.Textures.Count, Is.GreaterThanOrEqualTo(9));
            foreach (ImageAuditReport.TextureEntry texture in report.Textures)
            {
                InstalledPlatformPropertiesCanBeCachedByBuiltInInspector(texture.Path);
            }
        }

        [Test]
        public void AuditWithTextureInspectorOpenKeepsInstalledPlatformPropertiesAvailable()
        {
            const string path = "Assets/Art/CityMap/Animals/T_Animal_DogWhite.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            UnityEditor.Editor inspector = UnityEditor.Editor.CreateEditor(importer);
            try
            {
                AssertPlatformProperties(inspector, path);
                ImageResourceAudit.Scan();
                AssertPlatformProperties(inspector, path);
            }
            finally
            {
                Object.DestroyImmediate(inspector);
            }
        }

        [TestCase("Assets/Art/CityMap/Animals/T_Animal_Rabbit.png")]
        [TestCase("Assets/Art/CityMap/Animals/T_Animal_CatCalico.png")]
        [TestCase("Assets/Art/CityMap/Animals/T_Animal_CatOrange.png")]
        [TestCase("Assets/Art/CityMap/Animals/T_Animal_DogWhite.png")]
        [TestCase("Assets/Art/CityMap/T_CityMap_Neighborhood.jpg")]
        [TestCase("Assets/Art/Outing/T_Outing_Parking.jpg")]
        [TestCase("Assets/Art/Outing/T_Outing_Treehouse.jpg")]
        [TestCase("Assets/Art/PetMotion/T_Pet_original.png")]
        [TestCase("Assets/Art/Outing/T_Outing_DogNatural.png")]
        public void InstalledPlatformPropertiesCanBeCachedByBuiltInInspector(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            UnityEditor.Editor inspector = UnityEditor.Editor.CreateEditor(importer);
            try
            {
                AssertPlatformProperties(inspector, path);
            }
            finally
            {
                Object.DestroyImmediate(inspector);
            }
        }

        private static void AssertPlatformProperties(UnityEditor.Editor inspector, string path)
        {
            // Exercise the exact Unity 2022.3 Inspector step from the reported stack trace.
            // Reflection stays in this version-specific Editor regression test, never runtime code.
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = inspector.GetType().GetField("m_PlatformSettings", flags);
            Assert.That(field, Is.Not.Null, "Unity Inspector API changed; review this regression probe.");
            var platforms = (IEnumerable)field.GetValue(inspector);
            inspector.serializedObject.Update();
            SerializedProperty array = inspector.serializedObject.FindProperty("m_PlatformSettings");
            foreach (object platform in platforms)
            {
                object model = platform.GetType().GetProperty("model", flags).GetValue(platform);
                var settings = (TextureImporterPlatformSettings)model.GetType()
                    .GetProperty("platformTextureSettings", flags).GetValue(model);
                MethodInfo cache = platform.GetType().GetMethod("CacheSerializedProperties", flags);
                Assert.That(cache, Is.Not.Null);
                Assert.DoesNotThrow(() => cache.Invoke(platform, new object[] { array }),
                    path + " missing Inspector platform: " + settings.name);
            }
        }
    }
}
