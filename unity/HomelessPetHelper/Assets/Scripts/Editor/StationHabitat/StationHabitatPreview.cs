using System;
using System.Linq;
using HomelessPetHelper.Modules.Station.UI.Habitat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HomelessPetHelper.Editor.StationHabitat
{
    /// <summary>Editor-only healthy sample. Never adds an individual to the formal station.</summary>
    [InitializeOnLoad]
    public static class StationHabitatPreview
    {
        [Serializable] private sealed class Copy { public string name; }
        private static StationHabitatView _view;
        private static Toggle _pause;

        static StationHabitatPreview()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            SceneManager.sceneLoaded += SceneLoaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private static void PlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Open(SceneManager.GetActiveScene());
            else if (state == PlayModeStateChange.ExitingPlayMode) Close();
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying) Open(scene);
        }

        private static void SceneUnloaded(Scene scene)
        {
            if (scene.path == StationHabitatAuthor.ScenePath) Close();
        }

        private static void Open(Scene scene)
        {
            if (scene.path != StationHabitatAuthor.ScenePath) return;
            Close();
            var roots = scene.GetRootGameObjects();
            _view = roots.SelectMany(x => x.GetComponentsInChildren<StationHabitatView>(true)).Single();
            _pause = roots.SelectMany(x => x.GetComponentsInChildren<Toggle>(true)).Single();
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(StationHabitatAuthor.TextPath);
            _view.ShowResident(new Guid("abe25c06-2563-4aba-8abc-b5117468e6bf"),
                JsonUtility.FromJson<Copy>(text.text).name, false);
            _pause.onValueChanged.AddListener(Pause);
            Pause(_pause.isOn);
        }

        private static void Close()
        {
            if (_pause != null) _pause.onValueChanged.RemoveListener(Pause);
            _pause = null;
            _view = null;
        }

        private static void Pause(bool paused)
        {
            if (_view != null) _view.SetSuspended(paused);
        }
    }
}
