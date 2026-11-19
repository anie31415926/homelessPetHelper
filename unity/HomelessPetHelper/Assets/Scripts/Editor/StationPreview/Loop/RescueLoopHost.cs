using System;
using System.Linq;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor.StationPreview.Loop
{
    [InitializeOnLoad]
    public static class RescueLoopHost
    {
        public static RescueLoopPresenter Current { get; private set; }

        static RescueLoopHost()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += Close;
            SceneManager.sceneLoaded += OnLoaded;
            SceneManager.sceneUnloaded += OnUnloaded;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode) Open(SceneManager.GetActiveScene());
            else if (state == PlayModeStateChange.ExitingPlayMode) Close();
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Application.isPlaying) Open(scene);
        }

        private static void OnUnloaded(Scene scene)
        {
            if (scene.path == RescueLoopAssets.ScenePath) Close();
        }

        private static void Open(Scene scene)
        {
            if (scene.path != RescueLoopAssets.ScenePath) return;
            Close();
            GameObject[] roots = scene.GetRootGameObjects();
            Current = new RescueLoopPresenter(Find<StationPanelView>(roots), Find<CityMapView>(roots),
                Find<RescueCaseView>(roots));
        }

        private static T Find<T>(GameObject[] roots) where T : Component
        {
            T view = roots.Select(root => root.GetComponent<T>()).SingleOrDefault(item => item != null);
            if (view == null) throw new InvalidOperationException("Loop scene has no saved " + typeof(T).Name);
            return view;
        }

        private static void Close()
        {
            Current?.Dispose();
            Current = null;
        }
    }
}
