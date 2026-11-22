using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.PetMotion
{
    public sealed class PetMotionControls : MonoBehaviour
    {
        [SerializeField] private PetMotionPlayer _player;
        [SerializeField] private Dropdown _action;
        [SerializeField] private Toggle _playing;
        [SerializeField] private Toggle _source;
        [SerializeField] private Slider _speed;
        [SerializeField] private Slider _progress;
        [SerializeField] private Button _reset;

        private void OnEnable()
        {
            _action.onValueChanged.AddListener(Select);
            _playing.onValueChanged.AddListener(Play);
            _source.onValueChanged.AddListener(Source);
            _speed.onValueChanged.AddListener(Speed);
            _progress.onValueChanged.AddListener(Seek);
            _reset.onClick.AddListener(ResetPreview);
            Sync();
        }

        private void OnDisable()
        {
            _action.onValueChanged.RemoveListener(Select);
            _playing.onValueChanged.RemoveListener(Play);
            _source.onValueChanged.RemoveListener(Source);
            _speed.onValueChanged.RemoveListener(Speed);
            _progress.onValueChanged.RemoveListener(Seek);
            _reset.onClick.RemoveListener(ResetPreview);
        }

        private void Update()
        {
            _progress.SetValueWithoutNotify(_player.TimeSeconds / _player.Duration);
        }

        private void Select(int value) { _player.TransitionTo(value); Sync(); }
        private void Play(bool value) => _player.Playing = value;
        private void Source(bool value) => _player.SetSource(value);
        private void Speed(float value) => _player.SetSpeed(value);
        private void Seek(float value) { _player.Seek(value); Sync(); }
        private void ResetPreview() { _player.ResetPreview(); Sync(); }

        private void Sync()
        {
            _action.SetValueWithoutNotify(_player.Action);
            _playing.SetIsOnWithoutNotify(_player.Playing);
            _source.SetIsOnWithoutNotify(_player.Source);
            _speed.SetValueWithoutNotify(_player.Speed);
        }
    }
}
