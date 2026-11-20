using System;
using System.IO;
using System.Linq;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Audio.UI;
using HomelessPetHelper.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HomelessPetHelper.Editor.Audio
{
    public static class BasicAudioAssets
    {
        public const string CatalogPath = "Assets/Settings/Audio/BasicAudioCatalog.asset";
        private static readonly string[] Names = { "UI_Tap", "UI_Open", "Merge", "RescueSuccess", "Reward", "Notice" };
        private static readonly string[] Ids = { GameAudioCues.Tap, GameAudioCues.Open, GameAudioCues.Merge,
            GameAudioCues.RescueSuccess, GameAudioCues.Reward, GameAudioCues.Notice };

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use audio-integration.ps1.");
            ConfigureClips();
            AudioCatalog catalog = CreateCatalog();
            BindContent(catalog);
            foreach (string path in new[] { "Assets/Prefabs/Panels/Panel_StationLoop.prefab",
                "Assets/Prefabs/Panels/Panel_CityMap.prefab", "Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab" })
                ConfigurePrefab(path);
            ConfigureMain();
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureClips()
        {
            foreach (string name in Names)
            {
                string path = "Assets/Art/Audio/SFX_" + name + ".wav";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing sound: " + path);
                importer.forceToMono = true;
                importer.loadInBackground = false;
                var settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static AudioCatalog CreateCatalog()
        {
            EnsureFolder("Assets/Settings/Audio");
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog != null) return catalog; // Re-running authoring preserves later authored mixing choices.
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            entries.arraySize = Names.Length;
            for (int i = 0; i < Names.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_id").stringValue = Ids[i];
                entry.FindPropertyRelative("_channel").intValue = (int)AudioChannel.Effects;
                entry.FindPropertyRelative("_clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Art/Audio/SFX_" + Names[i] + ".wav");
                entry.FindPropertyRelative("_gain").floatValue = i < 2 ? 0.45f : 0.7f;
                entry.FindPropertyRelative("_maxVoices").intValue = i < 2 ? 2 : 1;
                entry.FindPropertyRelative("_minIntervalSeconds").floatValue = i < 2 ? 0.06f : 0.15f;
                entry.FindPropertyRelative("_loop").boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            catalog.CreateCues();
            return catalog;
        }

        private static void BindContent(AudioCatalog catalog)
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var serialized = new SerializedObject(content);
            serialized.FindProperty("_audioCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ButtonAudioSetup.AddToRoot(root, !path.Contains("Widget_SiegeChoice"));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureMain()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var owner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainGameRoot>(true))
                .Single();
            var serialized = new SerializedObject(owner);
            var scopes = serialized.FindProperty("_audioScopes");
            scopes.arraySize = 3;
            string[] fields = { "_station", "_map", "_siege" };
            for (int i = 0; i < fields.Length; i++)
            {
                var view = (Component)serialized.FindProperty(fields[i]).objectReferenceValue;
                // Siege source prefab belongs to another task; store additive audio overrides only in Main.
                var scope = ButtonAudioSetup.AddToRoot(view.gameObject, true);
                scopes.GetArrayElementAtIndex(i).objectReferenceValue = scope;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
