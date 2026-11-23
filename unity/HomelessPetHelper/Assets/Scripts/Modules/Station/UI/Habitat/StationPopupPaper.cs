using HomelessPetHelper.Framework.UI.Motion;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Uses the shared placement cue on paper only; text, hit targets and layout stay fixed.</summary>
    public sealed class StationPopupPaper : MonoBehaviour
    {
        [SerializeField] private MotionScope _scope;
        [SerializeField] private RectTransform _paper;
        private float _startedAt;
        private bool _playing;

        private void OnEnable() { _startedAt = _scope.UiSeconds; _playing = true; }
        private void OnDisable() => ResetPaper();
        private void OnApplicationFocus(bool focused) { if (!focused) ResetPaper(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetPaper(); }
        private void ResetPaper() { _playing = false; _paper.localScale = Vector3.one; }
        private void LateUpdate()
        {
            if (!_playing) return;
            float seconds = _scope.UiSeconds - _startedAt;
            var pose = _scope.Library.Sample(MotionCue.Place, seconds);
            _paper.localScale = new Vector3(pose.Scale.x, pose.Scale.y, 1);
            if (seconds >= _scope.Library.Duration(MotionCue.Place))
            { _paper.localScale = Vector3.one; _playing = false; }
        }
    }
}
