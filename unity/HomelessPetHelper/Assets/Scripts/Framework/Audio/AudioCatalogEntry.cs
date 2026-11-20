using System;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio
{
    [Serializable]
    public sealed class AudioCatalogEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private AudioChannel _channel = AudioChannel.Effects;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0, 1)] private float _gain = 1;
        [SerializeField, Range(1, 32)] private int _maxVoices = 2;
        [SerializeField, Range(0, 60)] private float _minIntervalSeconds = 0.05f;
        [SerializeField] private bool _loop;

        public AudioCue CreateCue() => new AudioCue(_id, _channel, _clip,
            new AudioCuePolicy(_gain, _maxVoices, _minIntervalSeconds, _loop));
    }
}
