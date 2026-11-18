using HomelessPetHelper.Modules.Outing.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor.CityMap
{
    [InitializeOnLoad]
    public static class CityMapHost
    {
        private static CityMapPresenter _presenter;

        static CityMapHost()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Open(SceneManager.GetActiveScene());
            else if (state == PlayModeStateChange.ExitingPlayMode) Close();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying) Open(scene);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (scene.path == CityMapAssets.ScenePath) Close();
        }

        private static void Open(Scene scene)
        {
            if (scene.path != CityMapAssets.ScenePath) return;
            Close();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!root.TryGetComponent(out CityMapView view)) continue;
                _presenter = new CityMapPresenter(view,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.TextPath));
                return;
            }
            throw new System.InvalidOperationException("Map scene has no saved map view.");
        }

        private static void Close()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
