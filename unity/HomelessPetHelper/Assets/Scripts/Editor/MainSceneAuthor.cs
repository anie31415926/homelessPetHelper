using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.PetMotion;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor
{
    public static class MainSceneAuthor
    {
        public const string MainPath = "Assets/Scenes/Main.unity";
        public const string FontPath = "Assets/Art/Fonts/PetRound-Game.ttf";

        [MenuItem("Homeless Pet Helper/Open Main Game")]
        public static void Open()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(MainPath);
        }

        public static void Apply()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated MainAssets authoring.");
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) throw new InvalidOperationException("Generate the bundled licensed font first.");
            var content = AssetDatabase.LoadAssetAtPath<GameContent>(RuntimePreviewContent.Path);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GameContent>();
                AssetDatabase.CreateAsset(content, RuntimePreviewContent.Path);
            }
            foreach (var pair in new[] {
                ("_mapConfig", "Assets/Settings/Game/CityMapConfig.json"),
                ("_mapText", "Assets/Settings/Game/CityMapText.json"),
                ("_stationText", "Assets/Settings/Game/StationPreviewText.json"),
                ("_siegeData", "Assets/Settings/Siege/SiegeTrialData.json"),
                ("_siegeText", "Assets/Settings/Siege/SiegeTrialText.json"),
                ("_catMask", "Assets/Art/Station/T_Station_CatTemporaryMask.bytes"),
                ("_dogMask", "Assets/Art/Station/T_Station_DogTemporaryMask.bytes") })
                Bind(content, pair.Item1, AssetDatabase.LoadAssetAtPath<TextAsset>(pair.Item2));
            Bind(content, "_cat", AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Station/T_Station_CatTemporary.png"));
            Bind(content, "_dog", AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Art/Station/T_Station_DogTemporary.png"));
            Bind(content, "_font", font);
            content.Validate();
            foreach (string path in new[] { "Assets/Prefabs/Panels/Panel_StationLoop.prefab",
                "Assets/Prefabs/Panels/Panel_CityMap.prefab", "Assets/Prefabs/Panels/Panel_SiegeTrial.prefab",
                "Assets/Prefabs/Widgets/Widget_SiegeChoice.prefab" })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var label in root.GetComponentsInChildren<Text>(true))
                    {
                        label.font = font;
                        label.lineSpacing = .94f;
                        if (path.EndsWith("Panel_SiegeTrial.prefab", StringComparison.Ordinal) &&
                            label.name == "Title")
                        {
                            label.fontSize = 40;
                            label.resizeTextMaxSize = 40;
                        }
                        if (path.EndsWith("Panel_StationLoop.prefab", StringComparison.Ordinal) &&
                            label.name == "Title")
                            label.text = "救助站";
                    }
                    if (path.EndsWith("Panel_StationLoop.prefab", StringComparison.Ordinal))
                    {
                        foreach (var image in root.GetComponentsInChildren<Image>(true))
                        {
                            string old = AssetDatabase.GetAssetPath(image.sprite);
                            if (!old.StartsWith("Assets/EditorScenes/StationArt/", StringComparison.Ordinal)) continue;
                            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Station/T_Station_" +
                                (old.Contains("dog") ? "Dog" : "Cat") + "Temporary.png");
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(MainPath);
            var bootstrap = scene.GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var host = bootstrap.GetComponent<MainGameRoot>() ?? bootstrap.gameObject.AddComponent<MainGameRoot>();
            T Panel<T>(string path) where T : Component
            {
                var existing = scene.GetRootGameObjects()
                    .SelectMany(x => x.GetComponentsInChildren<T>(true)).SingleOrDefault();
                if (existing != null) return existing;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<T>();
            }
            var station = Panel<StationPanelView>("Assets/Prefabs/Panels/Panel_StationLoop.prefab");
            var map = Panel<CityMapView>("Assets/Prefabs/Panels/Panel_CityMap.prefab");
            var siege = Panel<SiegeTrialView>("Assets/Prefabs/Panels/Panel_SiegeTrial.prefab");
            Bind(host, "_bootstrap", bootstrap);
            Bind(host, "_content", content);
            Bind(host, "_station", station);
            Bind(host, "_map", map);
            Bind(host, "_siege", siege);
            AuthorMotion(siege);
            station.gameObject.SetActive(true);
            map.gameObject.SetActive(false);
            siege.gameObject.SetActive(false);
            if (!scene.GetRootGameObjects().Any(x => x.GetComponent<EventSystem>() != null))
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            foreach (var camera in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Camera>(true)))
                if (camera.CompareTag("MainCamera")) camera.cullingMask = 0;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var bad = AssetDatabase.GetDependencies(MainPath, true)
                .Where(path => path.StartsWith("Assets/EditorScenes/", StringComparison.Ordinal)).ToArray();
            if (bad.Length != 0)
                throw new InvalidOperationException("Main has preview dependencies: " + string.Join(", ", bad));
        }

        private static void AuthorMotion(SiegeTrialView siege)
        {
            var existing = siege.GetComponentInChildren<PetMotionPlayer>(true);
            if (existing != null)
            {
                existing.transform.SetParent(null, false);
                existing.GetComponentInChildren<Camera>(true).backgroundColor = Color.clear;
                existing.gameObject.SetActive(false);
            }
            if (siege.GetComponent<SiegePetMotion>() != null) return;
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/PetMotion/Widget_PetMotion.prefab"));
            var player = rig.GetComponent<PetMotionPlayer>();
            var field = siege.GetComponentInChildren<SiegeBattlefield>(true);
            var cat = (Image)new SerializedObject(field).FindProperty("_cat").objectReferenceValue;
            cat.color = new Color(1, 1, 1, 0);
            var node = new GameObject("AnimatedAnimal", typeof(RectTransform), typeof(RawImage),
                typeof(PetMotionSurface));
            var rect = (RectTransform)node.transform;
            rect.SetParent(cat.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            node.GetComponent<RawImage>().raycastTarget = false;
            Bind(node.GetComponent<PetMotionSurface>(), "_surface", node.GetComponent<RawImage>());
            Bind(node.GetComponent<PetMotionSurface>(), "_player", player);
            Bind(node.GetComponent<PetMotionSurface>(), "_camera", rig.GetComponentInChildren<Camera>(true));
            var binding = siege.gameObject.AddComponent<SiegePetMotion>();
            Bind(binding, "_view", siege);
            Bind(binding, "_player", player);
            rig.GetComponentInChildren<Camera>(true).backgroundColor = Color.clear;
            rig.SetActive(false);
        }

        internal static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            if (value == null) throw new InvalidOperationException("Missing binding: " + field);
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
