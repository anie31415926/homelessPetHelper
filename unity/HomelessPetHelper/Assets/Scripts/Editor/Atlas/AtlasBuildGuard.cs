using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HomelessPetHelper.Editor.Atlas
{
    public sealed class AtlasBuildGuard : IPreprocessBuildWithReport
    {
        public const string ManifestPath = "Assets/Settings/Atlas/PetAtlasManifest.json";
        private const string ProjectPrefix = "unity/HomelessPetHelper/";
        private const string PetFolder = "Assets/Art/PetMotion/";
        public int callbackOrder => -1000;

        [Serializable]
        public sealed class Manifest
        {
            public int schema;
            public FileEntry[] files;
            public MeshEntry[] meshes;
        }

        [Serializable]
        public sealed class FileEntry { public string path, sha256; }
        [Serializable]
        public sealed class MeshEntry { public string id, path, sha256; }

        public void OnPreprocessBuild(BuildReport report)
        {
            try { ValidateAll(); }
            catch (Exception exception)
            {
                throw new BuildFailedException("Atlas validation rejected build. " + exception.Message +
                    " Run npm ci, npm run atlas:generate, rebake changed pet meshes, then Atlas/Validate All.");
            }
        }

        [MenuItem("Homeless Pet Helper/Atlas/Validate All")]
        public static void ValidateAll()
        {
            UiAtlasTools.Validate();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (asset == null) throw new InvalidDataException("Missing pet atlas generation manifest.");
            var manifest = JsonUtility.FromJson<Manifest>(asset.text);
            if (manifest.schema != 1 || manifest.files == null || manifest.files.Length == 0 ||
                manifest.meshes == null || manifest.meshes.Length != 8)
                throw new InvalidDataException("Invalid pet atlas generation manifest.");
            ValidateFiles(manifest, FindRepository());
            foreach (var expected in manifest.meshes)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(expected.path);
                if (mesh == null || AtlasMeshAudit.Hash(mesh) != expected.sha256)
                    throw new InvalidDataException("Stale Unity mesh UV/geometry; rebake: " + expected.path);
            }
            ValidatePetBindings(manifest);
            Directory.CreateDirectory("Library/AtlasAudit");
            File.WriteAllText("Library/AtlasAudit/last-validation.txt", DateTimeOffset.Now.ToString("o") +
                "\nValidated source/derived hashes, imported meshes, UI settings and bindings.\n");
            Debug.Log("Atlas validation passed: " + manifest.files.Length +
                " file hashes, 8 meshes and UI atlas groups.");
        }

        private static string FindRepository()
        {
            var directory = new DirectoryInfo(Application.dataPath);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "tools/check-atlases.mjs")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            throw new InvalidDataException("Atlas build requires the repository with tools and art-source, " +
                "not an Assets-only checkout.");
        }

        private static void ValidateFiles(Manifest manifest, string root)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.files)
            {
                if (string.IsNullOrEmpty(entry.path) || entry.path.Contains("..") || entry.path.Contains('\\') ||
                    Path.IsPathRooted(entry.path) || !paths.Add(entry.path))
                    throw new InvalidDataException("Unsafe/duplicate manifest path.");
                // An isolated run must validate ITS assets, not silently accept the live source project's files.
                string file = entry.path.StartsWith(ProjectPrefix, StringComparison.Ordinal)
                    ? Path.Combine(Application.dataPath, "..", entry.path.Substring(ProjectPrefix.Length))
                    : Path.Combine(root, entry.path);
                if (!File.Exists(file) || AtlasMeshAudit.HashBytes(File.ReadAllBytes(file)) != entry.sha256)
                    throw new InvalidDataException("Stale/missing atlas source or derived file: " + entry.path);
            }
        }

        private static void ValidatePetBindings(Manifest manifest)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PetFolder + "T_Pet_Atlas.png");
            var importer = AssetImporter.GetAtPath(PetFolder + "T_Pet_Atlas.png") as TextureImporter;
            if (texture == null || importer == null || importer.mipmapEnabled || importer.isReadable ||
                importer.textureType != TextureImporterType.Default ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.sRGBTexture ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency ||
                texture.format != TextureFormat.RGBA32)
                throw new InvalidDataException("Unsafe pet atlas importer; " +
                    "2px gutter supports bilinear without mipmaps/compression only.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (texture.width != width || texture.height != height ||
                importer.maxTextureSize < Math.Max(width, height))
                throw new InvalidDataException("Pet atlas was resized by import settings.");
            foreach (string platform in new[] { "Standalone", "WebGL", "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings.overridden && (settings.maxTextureSize < Math.Max(width, height) ||
                    settings.format != TextureImporterFormat.RGBA32 ||
                    settings.crunchedCompression || settings.allowsAlphaSplitting))
                    throw new InvalidDataException("Unsafe pet atlas platform override: " + platform);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(PetFolder + "M_Pet_Atlas.mat");
            if (material == null || material.mainTexture != texture || material.shader !=
                AssetDatabase.LoadAssetAtPath<Shader>(PetFolder + "Sh_PetMotion.shader") ||
                material.mainTextureScale != Vector2.one || material.mainTextureOffset != Vector2.zero)
                throw new InvalidDataException("Invalid pet atlas material/texture/shader binding.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PetFolder + "Widget_PetMotion.prefab");
            if (prefab == null) throw new InvalidDataException("Missing pet rig.");
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 10)
                throw new InvalidDataException("Pet rig renderer count changed; review atlas binding rules.");
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != material)
                    throw new InvalidDataException("Pet rig has non-atlas material: " + renderer.name);
                string id = renderer.name == "Source0" ? "original" : renderer.name == "Source1" ? "sit" :
                    renderer.name == "Source2" ? "side" : renderer.name;
                var expected = manifest.meshes.SingleOrDefault(m => m.id == id);
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
                    renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (expected == null || mesh == null || AssetDatabase.GetAssetPath(mesh) != expected.path)
                    throw new InvalidDataException("Pet rig has stale mesh binding: " + renderer.name);
            }
        }
    }
}
