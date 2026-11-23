using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HomelessPetHelper.Game.Loading
{
    public sealed class UnitySceneLoadingTask : ILoadingTask
    {
        private readonly AsyncOperation _operation;
        private bool _disposed;

        public UnitySceneLoadingTask(string destination)
        {
            ValidateDestination(destination);
            _operation = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single);
            if (_operation == null) throw new InvalidOperationException("Unity returned no scene load operation.");
            _operation.allowSceneActivation = false;
        }

        public float Progress => Mathf.Clamp01(_operation.progress / .9f);
        public bool IsComplete => _operation.progress >= .9f || _operation.isDone;
        public bool HasActivated => _operation.isDone;

        public static void ValidateDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination) || !Application.CanStreamedLevelBeLoaded(destination))
                throw new InvalidOperationException("Loading destination is not in Build Settings: " + destination);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UnitySceneLoadingTask));
        }

        public void Activate()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UnitySceneLoadingTask));
            if (!IsComplete) throw new InvalidOperationException("Destination scene is not ready.");
            _operation.allowSceneActivation = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // Unity scene loads cannot be cancelled; release the activation gate to unblock its queue.
            if (!_operation.isDone) _operation.allowSceneActivation = true;
        }
    }
}
