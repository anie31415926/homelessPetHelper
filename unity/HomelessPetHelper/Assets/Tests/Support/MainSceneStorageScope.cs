using System;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Tests.Support
{
    /// <summary>Inject before Main.Start, keeping every scene test away from player gameplay saves.</summary>
    public sealed class MainSceneStorageScope : IDisposable
    {
        private readonly Func<ISaveStorage> _createStorage;
        private readonly Func<ISaveStorage> _createGuidance, _createAudio, _createPlay;
        private readonly bool _freshGuidance;
        public MainSceneStorageScope(Func<ISaveStorage> createStorage = null, bool freshGuidance = false,
            Func<ISaveStorage> createGuidanceStorage = null, Func<ISaveStorage> createAudioStorage = null,
            Func<ISaveStorage> createPlayStorage = null)
        {
            _createStorage = createStorage ?? (() => new MemorySaveStorage());
            _createGuidance = createGuidanceStorage ?? (() => new MemorySaveStorage());
            _createAudio = createAudioStorage ?? (() => new MemorySaveStorage());
            _freshGuidance = freshGuidance;
            _createPlay = createPlayStorage ?? (() => new MemorySaveStorage());
            SceneManager.sceneLoaded += Configure;
        }

        private void Configure(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var game in root.GetComponentsInChildren<MainGameRoot>(true))
                {
                    game.ConfigureProgressStorage(_createStorage());
                    var guidance = _createGuidance();
                    if (!_freshGuidance) SeedReturningPlayer(guidance);
                    game.ConfigurePreferenceStorage(_createAudio(), guidance, _createPlay());
                }
        }

        private static void SeedReturningPlayer(ISaveStorage storage)
        {
            var progress = new Game.Guidance.GuidanceProgressSession(storage);
            var content = UnityEngine.Resources.FindObjectsOfTypeAll<GameContent>();
            foreach (var asset in content)
            {
                var catalog = asset.CreateGuides();
                foreach (string context in new[] { "field.yard.cleanup", "field.yard.puzzle", "siege.field" })
                {
                    var guide = catalog.Create(context);
                    while (guide.IsActive) guide.ReportSucceeded(guide.Current.CompletionAction);
                    if (!progress.TryRecordCompleted(guide))
                        throw new InvalidOperationException("Cannot prepare isolated returning-player UI progress.");
                }
                break;
            }
        }

        public void Dispose() => SceneManager.sceneLoaded -= Configure;
    }
}
