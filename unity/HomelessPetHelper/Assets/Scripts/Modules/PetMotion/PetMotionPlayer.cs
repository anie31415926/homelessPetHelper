using System;
using UnityEngine;

namespace HomelessPetHelper.Modules.PetMotion
{
    public sealed class PetMotionPlayer : MonoBehaviour
    {
        [SerializeField] private Transform _rig;
        [SerializeField] private AnimationClip[] _clips;
        [SerializeField] private GameObject _standing;
        [SerializeField] private GameObject _sit;
        [SerializeField] private GameObject _side;
        [SerializeField] private GameObject[] _sources;
        [SerializeField] private Transform[] _bones;
        private Vector3[] _restPositions;
        private int _action;
        private float _time;
        private bool _source;
        private bool _applicationPaused;
        private bool _hasFocus = true;

        public int Action => _action;
        public float TimeSeconds => _time;
        public float Duration => _clips[_action].length;
        public bool Playing { get; set; } = true;
        public float Speed { get; private set; } = 1;
        public bool Source => _source;
        public int Revision { get; private set; }

        private void Awake()
        {
            _restPositions = new Vector3[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
                _restPositions[i] = _bones[i].localPosition;
        }

        private void Start()
        {
            if (_rig == null || _clips == null || _clips.Length != 6 || _sources.Length != 3)
                throw new InvalidOperationException("Incomplete pet motion preview bindings.");
            Select(0);
        }

        private void Update()
        {
            if (!Playing || _source || _applicationPaused || !_hasFocus)
                return;
            _time = Mathf.Repeat(_time + Mathf.Min(Time.unscaledDeltaTime, .05f) * Speed, Duration);
            Sample();
        }

        private void OnApplicationPause(bool paused) => _applicationPaused = paused;
        private void OnApplicationFocus(bool focused) => _hasFocus = focused;

        public void Select(int action)
        {
            if (action < 0 || action >= _clips.Length)
                throw new ArgumentOutOfRangeException(nameof(action));
            _action = action;
            _time = 0;
            SetVisible();
            Sample();
        }

        public void SetSource(bool source)
        {
            _source = source;
            SetVisible();
            Sample();
        }

        public void SetSpeed(float speed)
        {
            if (float.IsNaN(speed) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = Mathf.Clamp(speed, .5f, 1.5f);
        }

        public void Seek(float normalizedTime)
        {
            if (float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime))
                throw new ArgumentOutOfRangeException(nameof(normalizedTime));
            Playing = false;
            _time = Mathf.Clamp01(normalizedTime) * Duration;
            Sample();
        }

        public void ResetPreview()
        {
            Playing = true;
            Speed = 1;
            _source = false;
            Select(0);
        }

        private void Sample()
        {
            // Reset values omitted from the next clip (constant tracks are not exported).
            _rig.localPosition = Vector3.zero;
            _rig.localScale = Vector3.one;
            for (int i = 0; i < _bones.Length; i++)
            {
                _bones[i].localPosition = _restPositions[i];
                _bones[i].localRotation = Quaternion.identity;
                _bones[i].localScale = Vector3.one;
            }
            if (!_source)
                _clips[_action].SampleAnimation(_rig.gameObject, _time);
            Revision++;
        }

        private void SetVisible()
        {
            _standing.SetActive(!_source && _action < 4);
            _sit.SetActive(!_source && _action == 4);
            _side.SetActive(!_source && _action == 5);
            for (int i = 0; i < _sources.Length; i++)
                _sources[i].SetActive(_source && i == (_action < 4 ? 0 : _action - 3));
        }
    }
}
