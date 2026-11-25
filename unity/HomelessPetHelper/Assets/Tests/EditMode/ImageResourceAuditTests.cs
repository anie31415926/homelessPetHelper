using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HomelessPetHelper.Editor.ResourceAudit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class ImageResourceAuditTests
    {
        private const string Root = "Assets/ImageResourceAuditTests";
        private const string Art = Root + "/Art";
        private bool _ownsFixture;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.IsValidFolder(Root), Is.False, "Do not replace an existing fixture folder.");
            AssetDatabase.CreateFolder("Assets", "ImageResourceAuditTests");
            _ownsFixture = true;
            AssetDatabase.CreateFolder(Root, "Art");
        }

        [TearDown]
        public void TearDown()
        {
            if (_ownsFixture)
            {
                AssetDatabase.DeleteAsset(Root);
                _ownsFixture = false;
            }
        }

        [Test]
        public void ImportSettingsAreReportedWithoutChangingIntentionalSettings()
        {
            string path = CreateTexture(Art, "Readable", 16);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true;
            importer.mipmapEnabled = true;
            importer.spriteBorder = new Vector4(2, 3, 4, 5);
            var webGl = importer.GetPlatformTextureSettings("WebGL");
            webGl.overridden = true;
            webGl.maxTextureSize = 512;
            webGl.format = TextureImporterFormat.Automatic;
            webGl.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(webGl);
            importer.SaveAndReimport();
            byte[] before = File.ReadAllBytes(path + ".meta");

            ImageAuditReport report = ImageResourceAudit.Scan();
            ImageAuditReport.TextureEntry texture = report.Textures.Single(item => item.Path == path);
            Assert.That(texture.SourceWidth, Is.EqualTo(16));
            Assert.That(texture.WebGlOverride, Is.True);
            Assert.That(texture.WebGlMaxSize, Is.EqualTo(512));
            Assert.That(texture.WebGlCompression, Is.EqualTo("Compressed"));
            Assert.That(HasFinding(report, path, "TextureReadWrite"), Is.True);
            Assert.That(HasFinding(report, path, "SpriteMipmaps"), Is.True);
            Assert.That(HasFinding(report, path, "UncompressedDefault"), Is.True);
            Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(before));
            Assert.That(importer.spriteBorder, Is.EqualTo(new Vector4(2, 3, 4, 5)));
        }

        [TestCase(2048, false)]
        [TestCase(4096, true)]
        public void LargeTextureReviewUsesImportedDimensionsAndStrictThreshold(int width, bool expected)
        {
            string path = CreateTexture(Art, "Wide", width);
            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(report.Textures.Single(item => item.Path == path).ImportedWidth, Is.EqualTo(width));
            Assert.That(HasFinding(report, path, "LargeImportedTexture"), Is.EqualTo(expected));
        }

        [Test]
        public void SourceFileBytesAndRgbaReferenceAreSeparateFromPackageSize()
        {
            string path = CreateTexture(Art, "ByteUnits", 256, 256);
            ImageAuditReport report = ImageResourceAudit.Scan();
            ImageAuditReport.TextureEntry texture = report.Textures.Single(item => item.Path == path);
            Assert.That(report.SchemaVersion, Is.EqualTo(2));
            Assert.That(texture.SourceFileBytes, Is.EqualTo(new FileInfo(path).Length));
            Assert.That(texture.ImportedRgba32ReferenceBytes, Is.EqualTo(262144));
            Assert.That(texture.SourceFileBytes, Is.LessThan(texture.ImportedRgba32ReferenceBytes));
            Assert.That(report.ToMarkdown(), Does.Contain("not exported/download size")
                .And.Contain("not measured GPU memory").And.Contain("not a package size or a resident set"));
        }

        [Test]
        public void NonSpriteMipmapsAreNotTreatedAsScreenSpaceUi()
        {
            string path = CreateTexture(Art, "WorldTexture", 16);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            Assert.That(HasFinding(ImageResourceAudit.Scan(), path, "SpriteMipmaps"), Is.False);
        }

        [Test]
        public void PrefabReferencesAreIndexedAndUnreferencedDoesNotMeanUnused()
        {
            string used = CreateTexture(Art, "Used", 16);
            string other = CreateTexture(Art, "DynamicCandidate", 16);
            var root = new GameObject("AuditFixture", typeof(SpriteRenderer));
            try
            {
                root.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(used);
                PrefabUtility.SaveAsPrefabAsset(root, Root + "/Widget_Audit.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(report.Textures.Single(item => item.Path == used).ReferencedBy,
                Does.Contain(Root + "/Widget_Audit.prefab"));
            Assert.That(HasFinding(report, used, "NoIndexedReference"), Is.False);
            ImageAuditReport.Finding finding = report.Findings.Single(item =>
                item.Path == other && item.Code == "NoIndexedReference");
            Assert.That(finding.Level, Is.EqualTo("Info"));
            Assert.That(finding.Detail, Does.Contain("not proof"));
        }

        [Test]
        public void OverlappingFolderAndSpriteInputsAreDeduplicatedWithinAnAtlas()
        {
            string path = CreateTexture(Art, "Shared", 16);
            AssetDatabase.CreateFolder(Root, "ArtSibling");
            string sibling = CreateTexture(Root + "/ArtSibling", "Outside", 16);
            string atlasPath = CreateAtlas("Folder", new Object[]
            {
                AssetDatabase.LoadAssetAtPath<DefaultAsset>(Art),
                AssetDatabase.LoadAssetAtPath<Sprite>(path)
            });

            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(report.Atlases.Single(item => item.Path == atlasPath).TexturePaths,
                Is.EqualTo(new[] { path }));
            Assert.That(report.Textures.Single(item => item.Path == sibling).AtlasPaths, Is.Empty);
            Assert.That(HasFinding(report, path, "MultipleMasterAtlases"), Is.False);
            Assert.That(HasFinding(report, path, "NoIndexedReference"), Is.False);
        }

        [Test]
        public void MultipleMastersAreFlaggedButMasterAndVariantAreNot()
        {
            string path = CreateTexture(Art, "Shared", 16);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            string masterPath = CreateAtlas("Master", new Object[] { sprite });
            var variant = new SpriteAtlas();
            variant.SetIsVariant(true);
            variant.SetMasterAtlas(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(masterPath));
            string variantPath = Root + "/Atlas_Variant.spriteatlas";
            AssetDatabase.CreateAsset(variant, variantPath);
            AssetDatabase.SaveAssets();

            ImageAuditReport first = ImageResourceAudit.Scan();
            Assert.That(first.Textures.Single(item => item.Path == path).AtlasPaths,
                Is.EquivalentTo(new[] { masterPath, variantPath }));
            Assert.That(HasFinding(first, path, "MultipleMasterAtlases"), Is.False);
            string second = CreateAtlas("OtherMaster", new Object[] { sprite });
            ImageAuditReport report = ImageResourceAudit.Scan();
            ImageAuditReport.Finding duplicate = report.Findings.Single(item =>
                item.Path == path && item.Code == "MultipleMasterAtlases");
            Assert.That(duplicate.Detail, Does.Contain(masterPath).And.Contain(second));
            Assert.That(duplicate.Detail, Does.Not.Contain(variantPath));
        }

        [Test]
        public void V2AtlasInputsCanBeReadWithoutPackingOrChangingPackerMode()
        {
            string texture = CreateTexture(Art, "V2", 16);
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new Object[] { AssetDatabase.LoadAssetAtPath<Sprite>(texture) });
            string path = Root + "/Atlas_V2.spriteatlasv2";
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            SpritePackerMode before = EditorSettings.spritePackerMode;

            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(report.Atlases.Single(item => item.Path == path).TexturePaths,
                Is.EqualTo(new[] { texture }));
            Assert.That(EditorSettings.spritePackerMode, Is.EqualTo(before));
        }

        [Test]
        public void VariantWithoutMasterIsReportedRatherThanSilentlyTreatedAsEmpty()
        {
            var atlas = new SpriteAtlas();
            atlas.SetIsVariant(true);
            string path = Root + "/Atlas_MissingMaster.spriteatlas";
            AssetDatabase.CreateAsset(atlas, path);
            AssetDatabase.SaveAssets();
            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(HasFinding(report, path, "AtlasMissingMaster"), Is.True);
            Assert.That(report.Atlases.Single(item => item.Path == path).TexturePaths, Is.Empty);
        }

        [Test]
        public void EnabledSceneDependenciesIncludeTransitivePrefabTextures()
        {
            EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = new[]
                {
                    new EditorBuildSettingsScene("Assets/EditorScenes/SC_StationWhiteboard.unity", true)
                };
                ImageAuditReport report = ImageResourceAudit.Scan();
                Assert.That(report.Textures.Single(item => item.Path.EndsWith("T_Pet_original.png"))
                    .InEnabledBuildSceneDependencies, Is.True);
            }
            finally
            {
                EditorBuildSettings.scenes = before;
            }
        }

        [Test]
        public void SavedProjectScanAndExportAreDeterministicAndDoNotWriteProjectAssets()
        {
            Dictionary<string, string> before = HashProjectFiles();
            string scenePath = SceneManager.GetActiveScene().path;
            bool dirty = SceneManager.GetActiveScene().isDirty;
            ImageAuditReport report = ImageResourceAudit.Scan();
            Assert.That(JsonUtility.ToJson(ImageResourceAudit.Scan()), Is.EqualTo(JsonUtility.ToJson(report)));
            ImageResourceAudit.WriteReport();
            CollectionAssert.AreEquivalent(before, HashProjectFiles());
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePath));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.EqualTo(dirty));
            Assert.That(report.Textures.Any(item => item.Path.EndsWith("T_Pet_original.png")), Is.True);
            Assert.That(report.Textures.Where(item => item.Path.StartsWith("Assets/EditorScenes/"))
                .All(item => !item.InEnabledBuildSceneDependencies), Is.True);
            Assert.That(File.ReadAllText(ImageResourceAudit.ReportDirectory + "/image-resources.md"),
                Does.Contain("not a WeChat device").And.Contain("References:"));
            Assert.That(JsonUtility.FromJson<ImageAuditReport>(File.ReadAllText(
                ImageResourceAudit.ReportDirectory + "/image-resources.json")).Textures.Count,
                Is.EqualTo(report.Textures.Count));
        }

        private static string CreateTexture(string folder, string name, int width, int height = 16)
        {
            string path = folder + "/T_Audit_" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return path;
        }

        private static string CreateAtlas(string name, Object[] inputs)
        {
            var atlas = new SpriteAtlas();
            atlas.Add(inputs);
            string path = Root + "/Atlas_" + name + ".spriteatlas";
            AssetDatabase.CreateAsset(atlas, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static bool HasFinding(ImageAuditReport report, string path, string code)
        {
            return report.Findings.Any(item => item.Path == path && item.Code == code);
        }

        private static Dictionary<string, string> HashProjectFiles()
        {
            using (SHA256 hash = SHA256.Create())
            {
                return new[] { "Assets", "Packages", "ProjectSettings" }
                    .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    .ToDictionary(path => path,
                        path => BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))));
            }
        }
    }
}
