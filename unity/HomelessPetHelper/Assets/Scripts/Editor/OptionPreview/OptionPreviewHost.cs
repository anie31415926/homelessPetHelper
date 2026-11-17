using HomelessPetHelper.Modules.Rescue.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor.OptionPreview
{
    [InitializeOnLoad]
    public static class OptionPreviewHost
    {
        private static OptionPreviewSession _session;
        private const string TextPath = "Assets/EditorScenes/OptionPreviewText.json";

        static OptionPreviewHost()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
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
            if (scene.path == OptionPreviewAssets.ScenePath)
            {
                Close();
            }
        }

        private static void Open(Scene scene)
        {
            if (scene.path != OptionPreviewAssets.ScenePath)
            {
                return;
            }

            Close();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out OptionPanelView view))
                {
                    _session = new OptionPreviewSession(view, AssetDatabase.LoadAssetAtPath<TextAsset>(TextPath));
                    return;
                }
            }

            throw new System.InvalidOperationException("Preview scene has no bound option panel.");
        }

        private static void Close()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
