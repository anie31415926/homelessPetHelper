using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor.StationPreview
{
    [InitializeOnLoad]
    public static class StationPreviewHost
    {
        private static StationPreviewSession _session;

        static StationPreviewHost()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
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

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying)
            {
                Open(scene);
            }
        }

        private static void SceneUnloaded(Scene scene)
        {
            if (scene.path == StationPreviewAssets.ScenePath)
            {
                Close();
            }
        }

        private static void Open(Scene scene)
        {
            if (scene.path != StationPreviewAssets.ScenePath)
            {
                return;
            }

            Close();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out StationPanelView view))
                {
                    _session = new StationPreviewSession(view,
                        AssetDatabase.LoadAssetAtPath<TextAsset>(StationPreviewAssets.TextPath),
                        RuntimePreviewContent.Load().Artwork, RuntimePreviewContent.Load().Font);
                    return;
                }
            }

            throw new System.InvalidOperationException("Station scene has no bound panel.");
        }

        private static void Close()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
