using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Editor.Atlas;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class AtlasToolingTests
    {
        [Test]
        public void CurrentAtlasInputsMeshesAndBindingsPassBuildGuard()
        {
            Assert.DoesNotThrow(() => new AtlasBuildGuard().OnPreprocessBuild(null));
        }

        [Test]
        public void InvalidConfigRejectsDuplicateAndIneligibleTextureMembership()
        {
            var config = UiAtlasConfig.Read();
            var group = config.groups[0];
            group.textures = new[] { group.textures[0], group.textures[0] };
            Assert.Throws<InvalidDataException>(() => config.Validate());
            config = UiAtlasConfig.Read();
            config.groups[0].textures = new[] { "Assets/Art/CityMap" };
            Assert.Throws<InvalidDataException>(() => config.Validate());
            foreach (int padding in new[] { -1, 0, 2, 33 })
            {
                config = UiAtlasConfig.Read(); config.groups[0].padding = padding;
                Assert.Throws<InvalidDataException>(() => config.Validate());
            }
        }

        [Test]
        public void SyncPreservesSpriteIdentityPivotBorderAndAtlasGuid()
        {
            var source = UiAtlasConfig.Read().groups[0];
            var group = new UiAtlasConfig.Group
            {
                id = "AtlasToolingTest", lifetime = "test-only", maxSize = 1024, padding = 4,
                textures = source.textures, includeInBuild = true,
            };
            string[] metas = group.textures.Select(p => File.ReadAllText(p + ".meta")).ToArray();
            string[] sprites = group.textures.SelectMany(UiAtlasConfig.LoadSprites).Select(SpriteSnapshot).ToArray();
            try
            {
                UiAtlasTools.SyncGroup(group);
                AssetDatabase.SaveAssets();
                string guid = AssetDatabase.AssetPathToGUID(group.AtlasPath);
                string serialized = File.ReadAllText(group.AtlasPath);
                UiAtlasTools.SyncGroup(group);
                AssetDatabase.SaveAssets();
                Assert.That(AssetDatabase.AssetPathToGUID(group.AtlasPath), Is.EqualTo(guid));
                Assert.That(File.ReadAllText(group.AtlasPath), Is.EqualTo(serialized));
                Assert.That(group.textures.Select(p => File.ReadAllText(p + ".meta")), Is.EqualTo(metas));
                Assert.That(group.textures.SelectMany(UiAtlasConfig.LoadSprites).Select(SpriteSnapshot),
                    Is.EqualTo(sprites));
            }
            finally { AssetDatabase.DeleteAsset(group.AtlasPath); }
        }

        private static string SpriteSnapshot(Sprite sprite)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
            return guid + ":" + id + ":" + sprite.rect + ":" + sprite.pivot + ":" + sprite.border;
        }

        [Test]
        public void AtlasValidationRejectsRotationMipmapsAndStaleMembership()
        {
            var group = UiAtlasConfig.Read().groups[0];
            var original = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(group.AtlasPath);
            var atlas = UnityEngine.Object.Instantiate(original);
            try
            {
                var settings = atlas.GetPackingSettings(); settings.enableRotation = true;
                atlas.SetPackingSettings(settings);
                Assert.Throws<InvalidDataException>(() => UiAtlasTools.ValidateGroup(atlas, group));
                settings.enableRotation = false; atlas.SetPackingSettings(settings);
                var texture = atlas.GetTextureSettings(); texture.generateMipMaps = true;
                atlas.SetTextureSettings(texture);
                Assert.Throws<InvalidDataException>(() => UiAtlasTools.ValidateGroup(atlas, group));
                texture.generateMipMaps = false; atlas.SetTextureSettings(texture);
                if (group.includeInBuild) atlas.Remove(atlas.GetPackables());
                else atlas.Add(group.textures.SelectMany(UiAtlasConfig.LoadSprites)
                    .Cast<UnityEngine.Object>().ToArray());
                Assert.Throws<InvalidDataException>(() => UiAtlasTools.ValidateGroup(atlas, group));
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        [Test]
        public void BuildHookRejectsStaleActualUvRatherThanTrustingManifest()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/PetMotion/Mesh_body.asset");
            var original = mesh.uv;
            try
            {
                var uv = mesh.uv; uv[0] += new Vector2(.125f, 0); mesh.uv = uv;
                var exception = Assert.Throws<BuildFailedException>(() =>
                    new AtlasBuildGuard().OnPreprocessBuild(null));
                StringAssert.Contains("Stale Unity mesh", exception.Message);
            }
            finally { mesh.uv = original; }
        }

        [Test]
        public void BuildHookRejectsMissingManifestAndDuplicateMasterMembership()
        {
            string content = File.ReadAllText(AtlasBuildGuard.ManifestPath);
            try
            {
                File.WriteAllText(AtlasBuildGuard.ManifestPath, "{\"schema\":0}");
                AssetDatabase.ImportAsset(AtlasBuildGuard.ManifestPath, ImportAssetOptions.ForceUpdate);
                Assert.Throws<BuildFailedException>(() => new AtlasBuildGuard().OnPreprocessBuild(null));
            }
            finally
            {
                File.WriteAllText(AtlasBuildGuard.ManifestPath, content);
                AssetDatabase.ImportAsset(AtlasBuildGuard.ManifestPath, ImportAssetOptions.ForceUpdate);
            }
            var source = UiAtlasConfig.Read().groups[0];
            var group = new UiAtlasConfig.Group
            {
                id = "DuplicateTest", textures = source.textures, maxSize = 1024, padding = 4, includeInBuild = true,
            };
            var other = new UiAtlasConfig.Group
            {
                id = "DuplicateOther", textures = source.textures, maxSize = 1024, padding = 4, includeInBuild = true,
            };
            try
            {
                UiAtlasTools.SyncGroup(group);
                UiAtlasTools.SyncGroup(other);
                Assert.Throws<BuildFailedException>(() => new AtlasBuildGuard().OnPreprocessBuild(null));
            }
            finally
            {
                AssetDatabase.DeleteAsset(group.AtlasPath);
                AssetDatabase.DeleteAsset(other.AtlasPath);
            }
        }

        [Test]
        public void MeshFingerprintMatchesCrossLanguageFixture()
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(0, 1, 0) }, uv = new[] { new Vector2(.25f, .5f) },
                uv2 = new[] { new Vector2(0, 1) }, triangles = new[] { 0, 0, 0 },
            };
            try
            {
                Assert.That(AtlasMeshAudit.Hash(mesh),
                    Is.EqualTo("00f4e7bc5c7abf24946cf7b7b179c23d50245e5c0dbabc555544c9c6b03aa1c5"));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void BuildHookRejectsChangedAssetInIsolatedProjectItself()
        {
            const string path = "Assets/Art/PetMotion/T_Pet_Atlas.png";
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                using (var stream = new FileStream(path, FileMode.Append)) stream.WriteByte(0);
                var exception = Assert.Throws<BuildFailedException>(() =>
                    new AtlasBuildGuard().OnPreprocessBuild(null));
                StringAssert.Contains("Stale/missing atlas source", exception.Message);
            }
            finally { File.WriteAllBytes(path, bytes); }
        }

        [Test]
        public void BuildHookRejectsUnsafePetMipmapsAndCompression()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/PetMotion/T_Pet_Atlas.png");
            bool mipmaps = importer.mipmapEnabled;
            var compression = importer.textureCompression;
            try
            {
                importer.mipmapEnabled = true;
                Assert.Throws<BuildFailedException>(() => new AtlasBuildGuard().OnPreprocessBuild(null));
                importer.mipmapEnabled = mipmaps;
                importer.textureCompression = TextureImporterCompression.Compressed;
                Assert.Throws<BuildFailedException>(() => new AtlasBuildGuard().OnPreprocessBuild(null));
            }
            finally
            {
                importer.mipmapEnabled = mipmaps;
                importer.textureCompression = compression;
            }
        }

        [Test]
        public void StandaloneSelectionReleasesSpritesSoBuildCannotStripTheirSourceTextures()
        {
            var source = UiAtlasConfig.Read().groups[0];
            var group = new UiAtlasConfig.Group
            {
                id = "ModeSwitchTest", textures = source.textures, maxSize = 1024, padding = 4,
                includeInBuild = true,
            };
            try
            {
                UiAtlasTools.SyncGroup(group);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(group.AtlasPath);
                Assert.That(atlas.GetPackables(), Has.Length.EqualTo(8));
                SpriteAtlasUtility.PackAtlases(new[] {atlas}, BuildTarget.StandaloneWindows64);
                Assert.That(atlas.spriteCount, Is.EqualTo(8));
                group.includeInBuild = false;
                atlas.SetIncludeInBuild(false);
                atlas.Remove(atlas.GetPackables());
                Assert.Throws<InvalidDataException>(() => UiAtlasTools.ValidateGroup(atlas, group));
                UiAtlasTools.SyncGroup(group);
                Assert.That(atlas.IsIncludeInBuild(), Is.False);
                Assert.That(atlas.GetPackables(), Is.Empty);
                Assert.That(atlas.spriteCount, Is.Zero);
                Assert.DoesNotThrow(() => UiAtlasTools.ValidateGroup(atlas, group));
                atlas.SetIncludeInBuild(true);
                Assert.Throws<InvalidDataException>(() => UiAtlasTools.ValidateGroup(atlas, group));
            }
            finally { AssetDatabase.DeleteAsset(group.AtlasPath); }
        }
    }
}
