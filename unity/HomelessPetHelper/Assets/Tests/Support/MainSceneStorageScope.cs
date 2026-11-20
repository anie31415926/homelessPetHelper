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
        public MainSceneStorageScope(Func<ISaveStorage> createStorage = null)
        {
            _createStorage = createStorage ?? (() => new MemorySaveStorage());
            SceneManager.sceneLoaded += Configure;
        }

        private void Configure(Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var game in root.GetComponentsInChildren<MainGameRoot>(true))
                    game.ConfigureProgressStorage(_createStorage());
        }

        public void Dispose() => SceneManager.sceneLoaded -= Configure;
    }
}
