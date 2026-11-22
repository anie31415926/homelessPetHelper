using System;
using HomelessPetHelper.Framework.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.UI
{
    public sealed class AudioSettingsView : MonoBehaviour
    {
        [SerializeField] private Slider _master, _music, _effects;
        [SerializeField] private Toggle _masterMute, _musicMute, _effectsMute;
        private Action<AudioPreferences> _changed;

        public void Bind(AudioPreferences preferences, Action<AudioPreferences> changed)
        {
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
            Refresh(preferences);
        }

        public void Refresh(AudioPreferences preferences)
        {
            _master.SetValueWithoutNotify(preferences.Master.Volume);
            _music.SetValueWithoutNotify(preferences.Music.Volume);
            _effects.SetValueWithoutNotify(preferences.Effects.Volume);
            _masterMute.SetIsOnWithoutNotify(preferences.Master.IsMuted);
            _musicMute.SetIsOnWithoutNotify(preferences.Music.IsMuted);
            _effectsMute.SetIsOnWithoutNotify(preferences.Effects.IsMuted);
        }

        public void Unbind() => _changed = null;

        private void OnEnable()
        {
            _master.onValueChanged.AddListener(VolumeChanged);
            _music.onValueChanged.AddListener(VolumeChanged);
            _effects.onValueChanged.AddListener(VolumeChanged);
            _masterMute.onValueChanged.AddListener(MuteChanged);
            _musicMute.onValueChanged.AddListener(MuteChanged);
            _effectsMute.onValueChanged.AddListener(MuteChanged);
        }

        private void OnDisable()
        {
            _master.onValueChanged.RemoveListener(VolumeChanged);
            _music.onValueChanged.RemoveListener(VolumeChanged);
            _effects.onValueChanged.RemoveListener(VolumeChanged);
            _masterMute.onValueChanged.RemoveListener(MuteChanged);
            _musicMute.onValueChanged.RemoveListener(MuteChanged);
            _effectsMute.onValueChanged.RemoveListener(MuteChanged);
        }

        private void VolumeChanged(float value) => Changed();
        private void MuteChanged(bool value) => Changed();
        private void Changed() => _changed?.Invoke(new AudioPreferences(
            new AudioBusSettings(_master.value, _masterMute.isOn),
            new AudioBusSettings(_music.value, _musicMute.isOn),
            new AudioBusSettings(_effects.value, _effectsMute.isOn)));
    }
}
