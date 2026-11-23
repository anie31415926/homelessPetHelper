using System;
using UnityEngine;

namespace HomelessPetHelper.Game.Loading
{
    [DisallowMultipleComponent]
    public sealed class LoadingArtworkMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform _orbit;
        [SerializeField] private RectTransform[] _animals = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] _doorAnimals = Array.Empty<RectTransform>();
        [SerializeField] private RectTransform[] _decorations = Array.Empty<RectTransform>();
        [SerializeField, Range(0f, 30f)] private float _orbitDegreesPerSecond = 14f;
        private Quaternion[] _animalRotations, _doorRotations;
        private Vector2[] _decorationPositions;
        private Vector3[] _decorationScales;
        private Quaternion _orbitRotation;
        private float _elapsed;
        private bool _captured;

        public int OrbitAnimalCount => _animals.Length;
        public int DoorAnimalCount => _doorAnimals.Length;

        private void OnEnable()
        {
            Capture();
            _elapsed = 0f;
            Sample(0f);
        }

        private void Update()
        {
            _elapsed = Mathf.Repeat(_elapsed + Time.unscaledDeltaTime, 3600f);
            Sample(_elapsed);
        }

        private void OnDisable()
        {
            if (!_captured) return;
            _orbit.localRotation = _orbitRotation;
            for (int i = 0; i < _animals.Length; i++) _animals[i].localRotation = _animalRotations[i];
            for (int i = 0; i < _doorAnimals.Length; i++) _doorAnimals[i].localRotation = _doorRotations[i];
            for (int i = 0; i < _decorations.Length; i++)
            {
                _decorations[i].anchoredPosition = _decorationPositions[i];
                _decorations[i].localScale = _decorationScales[i];
            }
        }

        public void Sample(float seconds)
        {
            Capture();
            _orbit.localRotation = _orbitRotation * Quaternion.Euler(0f, 0f, -seconds * _orbitDegreesPerSecond);
            for (int i = 0; i < _animals.Length; i++)
                _animals[i].localRotation = _animalRotations[i] *
                    Quaternion.Euler(0f, 0f, Mathf.Sin(seconds * 3.1f + i * .79f) * 5f);
            for (int i = 0; i < _doorAnimals.Length; i++)
                _doorAnimals[i].localRotation = _doorRotations[i] *
                    Quaternion.Euler(0f, 0f, Mathf.Sin(seconds * 2.5f + i * 1.8f) * 6f);
            float width = ((RectTransform)_orbit.parent).rect.width;
            for (int i = 0; i < _decorations.Length; i++)
            {
                float wave = Mathf.Sin(seconds * 1.8f + i * 1.17f);
                _decorations[i].anchoredPosition = _decorationPositions[i] + new Vector2(0f, wave * width * .007f);
                _decorations[i].localScale = _decorationScales[i] * (1f + wave * .09f);
            }
        }

        private void Capture()
        {
            if (_captured) return;
            if (_orbit == null) throw new InvalidOperationException("Missing Loading orbit binding.");
            _orbitRotation = _orbit.localRotation;
            _animalRotations = CaptureRotations(_animals);
            _doorRotations = CaptureRotations(_doorAnimals);
            _decorationPositions = new Vector2[_decorations.Length];
            _decorationScales = new Vector3[_decorations.Length];
            for (int i = 0; i < _decorations.Length; i++)
            {
                _decorationPositions[i] = _decorations[i].anchoredPosition;
                _decorationScales[i] = _decorations[i].localScale;
            }
            _captured = true;
        }

        private static Quaternion[] CaptureRotations(RectTransform[] items)
        {
            var result = new Quaternion[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) throw new InvalidOperationException("Missing Loading artwork binding.");
                result[i] = items[i].localRotation;
            }
            return result;
        }
    }
}
