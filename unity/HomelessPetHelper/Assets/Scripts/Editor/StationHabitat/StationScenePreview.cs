using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Editor.StationHabitat
{
    [InitializeOnLoad]
    public static class StationScenePreview
    {
        private static GameLoopSession _loop;
        private static StationPreviewSession _presenter;

        static StationScenePreview()
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
            if (scene.path == StationSceneAuthor.ScenePath) Close();
        }
        private static void Open(Scene scene)
        {
            if (scene.path != StationSceneAuthor.ScenePath) return;
            Close();
            var content = RuntimePreviewContent.Load();
            var config = content.CreateMapConfig();
            var pool = config.CreateRequestPool();
            _loop = new GameLoopSession(4, config.MinutesPerDay,
                config.CreateRules(new PointUnlockState(), pool), pool, config.TimePolicy);
            for (int i = 0; i < 3; i++) AddSample(i);
            var view = scene.GetRootGameObjects().SelectMany(root =>
                root.GetComponentsInChildren<StationPanelView>(true)).Single();
            _presenter = new StationPreviewSession(view, content.StationText, content.Artwork, content.Font, _loop);
        }

        private static void AddSample(int index)
        {
            var animal = new AnimalIndividual(new Guid("7bf07376-4f8a-4d6d-a925-579a9e1c0f0" + index), "cat.shy",
                new AnimalDisposition(false, index == 1 ? AnimalEmotion.Watchful : AnimalEmotion.Relaxed,
                    AnimalStress.None, AnimalBehavior.Settled),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()),
                new AnimalCarePermissions(index == 0, index == 0, index == 0), new AnimalCoatCondition(0, 0));
            var arrival = new StationAnimalIntake(Guid.NewGuid(), animal,
                new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 0, Array.Empty<string>()));
            if (!_loop.Station.TryReceive(_loop.StationSessionId, arrival, out _, out var failure))
                throw new InvalidOperationException("Invalid station scene sample: " + failure);
            if (index < 2 && !_loop.Station.TryAssessKnownHealthyAnimal(_loop.StationSessionId, animal.Current.AnimalId,
                out failure)) throw new InvalidOperationException("Sample assessment failed: " + failure);
        }

        private static void Close()
        {
            _presenter?.Dispose();
            _presenter = null;
            _loop?.Dispose();
            _loop = null;
        }
    }
}
