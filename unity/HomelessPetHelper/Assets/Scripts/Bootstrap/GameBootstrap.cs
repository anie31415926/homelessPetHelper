using HomelessPetHelper.Modules.Rescue;
using UnityEngine;

namespace HomelessPetHelper.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private bool _isBackgrounded;
        private bool _hasFocus = true;
        private GameLoopSession _session;

        public RescueFlow Flow { get; private set; }

        private void Awake()
        {
            Flow = new RescueFlow();
            Application.targetFrameRate = 60;
        }

        public void BindSession(GameLoopSession session)
        {
            _session = session;
            Flow = session?.Flow ?? new RescueFlow();
            RefreshSuspension();
        }

        private void OnApplicationPause(bool isPaused)
        {
            _isBackgrounded = isPaused;
            RefreshSuspension();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasFocus = hasFocus;
            RefreshSuspension();
        }

        private void RefreshSuspension()
        {
            // Shared panels preserve their explicit pause until the player resumes them.
            if (_session != null)
            {
                if (_isBackgrounded || !_hasFocus) _session.SetSuspended(true);
                return;
            }
            Flow.SetSuspended(_isBackgrounded || !_hasFocus);
        }
    }
}
