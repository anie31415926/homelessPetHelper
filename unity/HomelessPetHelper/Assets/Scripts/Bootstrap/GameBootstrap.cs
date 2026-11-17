using HomelessPetHelper.Modules.Rescue;
using UnityEngine;

namespace HomelessPetHelper.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        private bool _isBackgrounded;
        private bool _hasFocus = true;

        public RescueFlow Flow { get; private set; }

        private void Awake()
        {
            Flow = new RescueFlow();
            Application.targetFrameRate = 60;
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
            Flow.SetSuspended(_isBackgrounded || !_hasFocus);
        }
    }
}
