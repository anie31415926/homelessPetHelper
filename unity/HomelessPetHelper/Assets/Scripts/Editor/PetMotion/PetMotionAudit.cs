using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace HomelessPetHelper.Editor.PetMotion
{
    public static class PetMotionAudit
    {
        [Serializable]
        private sealed class Row
        {
            public string path, format;
            public int width, height;
            public long sourceBytes, rgba32BaseBytes, editorObjectMemoryBytes;
            public bool readable, mipmaps, inEnabledScenes, inPreloadedAssets;
        }

        [Serializable]
        private sealed class Report
        {
            public string unityVersion, target, memoryMeaning, bundleMeaning;
            public Row[] textures;
            public string[] previewDependencies, enabledSceneOverlaps, preloadedOverlaps;
            public long textureSourceBytes, rgba32BaseBytes, editorTextureObjectBytes;
            public long meshSourceBytes, clipSourceBytes, editorMeshObjectBytes, editorClipObjectBytes;
            public long lz4RigBundleBytes, lzmaRigBundleBytes;
        }

        public static void WriteReport()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/PetMotionAudit"));
            Directory.CreateDirectory(folder);
            var preview = AssetDatabase.GetDependencies(PetMotionAssets.RigPath, true);
            var included = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.enabled)
                    included.UnionWith(AssetDatabase.GetDependencies(scene.path, true));
            var preloaded = new HashSet<string>();
            foreach (var asset in PlayerSettings.GetPreloadedAssets())
                if (asset != null)
                    preloaded.UnionWith(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(asset), true));
            var rows = new List<Row>();
            foreach (var path in preview.Where(p => p.EndsWith(".png", StringComparison.Ordinal)))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                rows.Add(new Row
                {
                    path = path, width = texture.width, height = texture.height,
                    format = texture.format.ToString(), sourceBytes = new FileInfo(path).Length,
                    rgba32BaseBytes = (long)texture.width * texture.height * 4,
                    editorObjectMemoryBytes = Profiler.GetRuntimeMemorySizeLong(texture),
                    readable = importer.isReadable, mipmaps = importer.mipmapEnabled,
                    inEnabledScenes = included.Contains(path), inPreloadedAssets = preloaded.Contains(path),
                });
            }
            var report = new Report
            {
                unityVersion = Application.unityVersion, target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                memoryMeaning = "Editor loaded-object memory, not target GPU VRAM; RGBA32 is base texel reference.",
                bundleMeaning = "Measured explicit Windows64 rig-only AssetBundles; not Player/WeChat/download size.",
                textures = rows.OrderBy(x => x.path).ToArray(), previewDependencies = preview,
                enabledSceneOverlaps = preview.Where(included.Contains).ToArray(),
                preloadedOverlaps = preview.Where(preloaded.Contains).ToArray(),
                textureSourceBytes = rows.Sum(x => x.sourceBytes), rgba32BaseBytes = rows.Sum(x => x.rgba32BaseBytes),
                editorTextureObjectBytes = rows.Sum(x => x.editorObjectMemoryBytes),
            };
            foreach (string path in preview)
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh != null)
                {
                    report.meshSourceBytes += new FileInfo(path).Length;
                    report.editorMeshObjectBytes += Profiler.GetRuntimeMemorySizeLong(mesh);
                }
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                {
                    report.clipSourceBytes += new FileInfo(path).Length;
                    report.editorClipObjectBytes += Profiler.GetRuntimeMemorySizeLong(clip);
                }
            }
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                report.lz4RigBundleBytes = Bundle(folder, "lz4", BuildAssetBundleOptions.ChunkBasedCompression);
                report.lzmaRigBundleBytes = Bundle(folder, "lzma", BuildAssetBundleOptions.None);
            }
            File.WriteAllText(Path.Combine(folder, "pet-motion.json"), JsonUtility.ToJson(report, true));
            Debug.Log("Pet motion resource audit: " + folder);
        }

        private static long Bundle(string folder, string name, BuildAssetBundleOptions options)
        {
            string output = Path.Combine(folder, name);
            Directory.CreateDirectory(output);
            var build = new AssetBundleBuild { assetBundleName = "pet-motion", assetNames =
                new[] { PetMotionAssets.RigPath } };
            var manifest = BuildPipeline.BuildAssetBundles(output, new[] { build }, options,
                BuildTarget.StandaloneWindows64);
            if (manifest == null)
                throw new InvalidOperationException("Pet motion comparison bundle failed.");
            return new FileInfo(Path.Combine(output, "pet-motion")).Length;
        }
    }
}
