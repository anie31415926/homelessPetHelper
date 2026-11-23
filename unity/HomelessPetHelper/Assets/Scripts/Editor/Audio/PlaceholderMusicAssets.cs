using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Game;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Editor.Audio
{
    public static class PlaceholderMusicAssets
    {
        public const string Folder = "Assets/Art/Audio/PlaceholderMusic/";
        public const string CatalogPath = "Assets/Settings/Audio/PlaceholderMusicCatalog.asset";
        private static readonly string[] Names = { "Station", "Explore", "Rescue" };
        private static readonly string[] Ids = { GameAudioCues.StationMusic,
            GameAudioCues.ExploreMusic, GameAudioCues.RescueMusic };

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use bgm-integration.ps1.");
            ConfigureClips();
            var catalog = CreateCatalog();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var serialized = new SerializedObject(content);
            serialized.FindProperty("_musicCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            MeasurePackedClips();
        }

        private static string ClipPath(string name) => Folder + "BGM_" + name + "_Placeholder.wav";

        private static void ConfigureClips()
        {
            foreach (string name in Names)
            {
                string path = ClipPath(name);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.userData = "temporary;not-final;bgm-placeholder-20261123;rights-pending";
                var settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.30f;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 32000;
                importer.defaultSampleSettings = settings;
                // Unity 2022.3 rejects a WebGL audio override. Default is the browser fallback, not streaming.
                settings.loadType = AudioClipLoadType.Streaming;
                if (!importer.SetOverrideSampleSettings("Standalone", settings))
                    throw new InvalidOperationException("Cannot set native streaming override.");
                importer.SaveAndReimport();
            }
        }

        private static AudioCatalog CreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            entries.arraySize = Names.Length;
            for (int i = 0; i < Names.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_id").stringValue = Ids[i];
                entry.FindPropertyRelative("_channel").intValue = (int)AudioChannel.Music;
                entry.FindPropertyRelative("_clip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath(Names[i]));
                entry.FindPropertyRelative("_gain").floatValue = 0.65f;
                entry.FindPropertyRelative("_maxVoices").intValue = 1;
                entry.FindPropertyRelative("_minIntervalSeconds").floatValue = 0;
                entry.FindPropertyRelative("_loop").boolValue = true;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.CreateCues();
            AssetDatabase.SetLabels(catalog, new[] { "temporary", "not-final" });
            return catalog;
        }

        private static void MeasurePackedClips()
        {
            string output = Path.GetFullPath("BgmEvidence");
            Directory.CreateDirectory(output);
            var builds = Names.Select(name => new AssetBundleBuild
            {
                assetBundleName = name.ToLowerInvariant() + ".bundle",
                assetNames = new[] { ClipPath(name) }
            }).ToArray();
            if (BuildPipeline.BuildAssetBundles(output, builds, BuildAssetBundleOptions.UncompressedAssetBundle,
                BuildTarget.StandaloneWindows64) == null) throw new InvalidOperationException("Music audit failed.");
            var records = builds.Select(build => new PackedClip
            {
                path = build.assetNames[0], bundleBytes = new FileInfo(Path.Combine(output,
                    build.assetBundleName)).Length
            }).ToArray();
            var report = new ImportReport { clips = records, totalBundleBytes = records.Sum(item => item.bundleBytes) };
            File.WriteAllText(Path.Combine(output, "import-report.json"), JsonUtility.ToJson(report, true));
            if (records.Any(item => item.bundleBytes > 393216) || report.totalBundleBytes > 1048576)
                throw new InvalidOperationException("Imported BGM exceeds this batch's compressed-size budget.");
        }

        [Serializable] private sealed class PackedClip { public string path; public long bundleBytes; }
        [Serializable] private sealed class ImportReport { public PackedClip[] clips; public long totalBundleBytes; }
    }
}
