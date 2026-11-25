using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor.RescueCase
{
    [InitializeOnLoad]
    public static class RescueCaseHost
    {
        private static RescueCasePresenter _presenter;

        static RescueCaseHost()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Open(SceneManager.GetActiveScene());
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Close();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying)
            {
                Open(scene);
            }
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            if (scene.path == RescueCaseAssets.ScenePath)
            {
                Close();
            }
        }

        private static void Open(Scene scene)
        {
            if (scene.path != RescueCaseAssets.ScenePath)
            {
                return;
            }

            Close();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out RescueCaseView view))
                {
                    _presenter = new RescueCasePresenter(view,
                        AssetDatabase.LoadAssetAtPath<TextAsset>(RescueCaseAssets.TextPath),
                        new RescueOptionContentLoader(
                            AssetDatabase.LoadAssetAtPath<TextAsset>(RescueCaseAssets.OptionsPath),
                            AssetDatabase.LoadAssetAtPath<TextAsset>(RescueCaseAssets.OptionTextPath)),
                        CaseStart.Normal);
                    return;
                }
            }

            throw new System.InvalidOperationException("Trial scene has no bound rescue case panel.");
        }

        private static void Close()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
