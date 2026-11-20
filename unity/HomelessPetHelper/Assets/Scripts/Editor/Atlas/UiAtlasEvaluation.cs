using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Editor.Atlas
{
    public static class UiAtlasEvaluation
    {
        private const string TemporaryFolder = UiAtlasConfig.OutputFolder + "/Evaluation";

        [Serializable]
        public sealed class Report
        {
            public string unityVersion, scope;
            public long standaloneBaseBytes, mainDependencyBaseBytes, visibleMapBaseBytes;
            public Usage[] references;
            public Candidate[] candidates;
        }

        [Serializable]
        public sealed class Usage
        {
            public string texture;
            public int savedImages, visibleImages;
            public bool mainDependency;
        }

        [Serializable]
        public sealed class Candidate
        {
            public string id, target;
            public long baseBytes;
            public string[] pages;
            public float rectangularOccupancy;
        }

        [MenuItem("Homeless Pet Helper/Atlas/Evaluate UI Grouping")]
        public static void Evaluate()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use tools/atlas-unity.ps1 -Action Evaluate in an isolated copy.");
            if (AssetDatabase.IsValidFolder(TemporaryFolder))
                throw new InvalidOperationException("Evaluation folder already exists; refusing to overwrite.");
            var group = UiAtlasConfig.Read().groups.Single(g => g.id == "CityMap");
            var candidates = new List<Candidate>();
            var originalAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(group.AtlasPath);
            bool originalInclude = originalAtlas.IsIncludeInBuild();
            Directory.CreateDirectory(TemporaryFolder);
            AssetDatabase.Refresh();
            try
            {
                originalAtlas.SetIncludeInBuild(false);
                foreach (var target in new[] { BuildTarget.StandaloneWindows64, BuildTarget.WebGL })
                {
                    candidates.Add(Pack("All1024", group.textures, 1024, target));
                    candidates.Add(Pack("All512", group.textures, 512, target));
                    candidates.Add(Pack("Buildings512", group.textures.Where(p => !p.Contains("/Animals/")).ToArray(),
                        512, target));
                    candidates.Add(Pack("Animals1024", group.textures.Where(p => p.Contains("/Animals/")).ToArray(),
                        1024, target));
                }
                var report = new Report
                {
                    unityVersion = Application.unityVersion,
                    scope = "Lossless native packing with original sizes, 4px padding, no rotation/tight packing. " +
                        "Base RGBA32 only; not measured draw calls, frame time, download or device memory.",
                    standaloneBaseBytes = group.textures.Sum(p =>
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                        return (long)texture.width * texture.height * 4;
                    }),
                    references = ReadUsage(group.textures), candidates = candidates.ToArray(),
                };
                report.mainDependencyBaseBytes = report.references.Where(r => r.mainDependency)
                    .Sum(r => BaseBytes(r.texture));
                report.visibleMapBaseBytes = report.references.Where(r => r.visibleImages > 0)
                    .Sum(r => BaseBytes(r.texture));
                Directory.CreateDirectory("Library/AtlasAudit");
                File.WriteAllText("Library/AtlasAudit/ui-evaluation.json", JsonUtility.ToJson(report, true));
                Debug.Log(JsonUtility.ToJson(report, true));
            }
            finally
            {
                originalAtlas.SetIncludeInBuild(originalInclude);
                AssetDatabase.DeleteAsset(TemporaryFolder);
                AssetDatabase.SaveAssets();
            }
        }

        private static Candidate Pack(string id, string[] paths, int size, BuildTarget target)
        {
            var group = new UiAtlasConfig.Group
            {
                id = "Evaluate" + id, lifetime = "isolated evaluation", maxSize = size, padding = 4,
                textures = paths, includeInBuild = true,
            };
            UiAtlasTools.SyncGroup(group);
            // Move the temporary asset under the evaluation folder; never touch the production atlas GUID.
            string destination = TemporaryFolder + "/" + Path.GetFileName(group.AtlasPath);
            string error = AssetDatabase.MoveAsset(group.AtlasPath, destination);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(destination);
            try
            {
                SpriteAtlasUtility.PackAtlases(new[] {atlas}, target);
                var sprites = new Sprite[atlas.spriteCount];
                atlas.GetSprites(sprites);
                try
                {
                    if (sprites.Length != paths.Sum(p => UiAtlasConfig.LoadSprites(p).Length) ||
                        paths.SelectMany(UiAtlasConfig.LoadSprites).Any(s => !atlas.CanBindTo(s)))
                        throw new InvalidDataException("Incomplete candidate packing: " + id);
                    var pages = sprites.Select(s => UnityEditor.Sprites.SpriteUtility.GetSpriteTexture(s, true))
                        .Distinct().ToArray();
                    return new Candidate
                    {
                        id = id, target = target.ToString(),
                        baseBytes = pages.Sum(p => (long)p.width * p.height * 4),
                        pages = pages.Select(p => p.width + "x" + p.height + " " + p.format).ToArray(),
                        rectangularOccupancy = sprites.Sum(s => s.rect.width * s.rect.height) /
                            pages.Sum(p => (float)p.width * p.height),
                    };
                }
                finally { foreach (var sprite in sprites) Object.DestroyImmediate(sprite); }
            }
            finally { AssetDatabase.DeleteAsset(destination); }
        }

        private static Usage[] ReadUsage(string[] paths)
        {
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Panels/Panel_CityMap.prefab");
            try
            {
                var images = root.GetComponentsInChildren<Image>(true);
                return paths.Select(path => new Usage
                {
                    texture = path,
                    mainDependency = dependencies.Contains(path),
                    savedImages = images.Count(i => i.sprite != null && AssetDatabase.GetAssetPath(i.sprite) == path),
                    visibleImages = images.Count(i => i.sprite != null &&
                        AssetDatabase.GetAssetPath(i.sprite) == path &&
                        i.isActiveAndEnabled && i.color.a > 0),
                }).ToArray();
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static long BaseBytes(string path)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return (long)texture.width * texture.height * 4;
        }

        public static void BuildStandaloneProbe()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use the isolated Atlas BuildProbe command.");
            AtlasBuildGuard.ValidateAll();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" }, target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/AtlasProbe/HomelessPetHelper.exe",
                // Incremental reports can omit reused texture payloads; audit a clean isolated build.
                options = BuildOptions.Development | BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Atlas probe build failed: " + report.summary.result);
            var dependencies = AssetDatabase.GetDependencies("Assets/Scenes/Main.unity", true);
            var packed = report.packedAssets.SelectMany(asset => asset.contents).ToArray();
            if (packed.Length == 0)
                throw new InvalidDataException("Build report has no packed-asset detail; " +
                    "cannot audit texture payloads.");
            File.WriteAllLines("Library/AtlasAudit/player-texture-inventory.txt", packed.Where(asset =>
                asset.sourceAssetPath.StartsWith("Assets/Art/CityMap/", StringComparison.Ordinal)).Select(asset =>
                asset.sourceAssetPath + " / " + asset.type + " / " + asset.packedSize));
            var lines = new List<string> { "Build bytes: " + report.summary.totalSize };
            foreach (var group in UiAtlasConfig.Read().groups.Where(g => !g.includeInBuild))
            {
                if (packed.Any(asset => asset.sourceAssetPath == group.AtlasPath && asset.packedSize > 0))
                    throw new InvalidDataException("Disabled UI atlas was included in player: " + group.AtlasPath);
                foreach (string path in group.textures.Where(dependencies.Contains))
                {
                    ulong bytes = 0;
                    foreach (var asset in packed.Where(asset => asset.sourceAssetPath == path &&
                        asset.type == typeof(Texture2D))) bytes += asset.packedSize;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (bytes < (ulong)texture.width * (ulong)texture.height * 4)
                        throw new InvalidDataException("Standalone RGBA32 texture payload missing: " + path);
                    lines.Add(path + " / Texture2D serialized bytes: " + bytes);
                }
            }
            File.WriteAllLines("Library/AtlasAudit/standalone-player.txt", lines);
            Debug.Log(string.Join("\n", lines));
        }
    }
}
