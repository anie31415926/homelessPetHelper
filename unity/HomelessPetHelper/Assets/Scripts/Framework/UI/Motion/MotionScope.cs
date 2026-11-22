using System;
using Unity.Profiling;
using UnityEngine;

namespace HomelessPetHelper.Framework.UI.Motion
{
    /// <summary>Panel-owned clocks. Scene suspension never pauses menu feedback or changes gameplay time.</summary>
    public sealed class MotionScope : MonoBehaviour
    {
        [SerializeField] private MotionLibrary _library;
        private bool _scenePaused, _focused = true, _backgrounded;
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Presentation.MotionClock");
        public MotionLibrary Library => _library;
        public float UiSeconds { get; private set; }
        public float SceneSeconds { get; private set; }
        public bool ScenePaused => _scenePaused || !_focused || _backgrounded;

        private void OnEnable()
        {
            if (_library == null) throw new InvalidOperationException("Missing panel motion library.");
            _library.Validate();
            UiSeconds = SceneSeconds = 0;
        }

        public void SetScenePaused(bool paused) => _scenePaused = paused;

        private void Update()
        {
            using (TickMarker.Auto())
            {
                if (!_focused || _backgrounded) return;
                float seconds = Mathf.Min(Time.unscaledDeltaTime, .1f);
                UiSeconds += seconds;
                if (!_scenePaused) SceneSeconds += seconds;
            }
        }

        private void OnApplicationFocus(bool focused) => _focused = focused;
        private void OnApplicationPause(bool paused) => _backgrounded = paused;
    }
}
