using System;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.PetMotion;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Renders the existing cat rig at the authored animal transform, on the panel scene clock.</summary>
    public sealed class FieldPetMotion : MonoBehaviour
    {
        [SerializeField] private PetMotionPlayer _prefab;
        [SerializeField] private PetPortraitSet _portraits;
        [SerializeField] private Image _still;
        [SerializeField] private RawImage _surface;
        [SerializeField] private AnimalCoatView _stillCoat, _motionCoat;
        [SerializeField] private MotionScope _clock;
        [SerializeField] private Vector3 _renderPosition;
        [SerializeField, Range(0, 2)] private int _action = 1;
        [SerializeField, Range(64, 512)] private int _resolution = 256;
        [SerializeField] private float _halfHeight = 320, _halfWidth = 285;
        private PetMotionPlayer _player;
        private Camera _camera;
        private RenderTexture _target;
        private bool _cat, _dog, _visible = true, _dogWalking;
        private float _renderedSeconds = -1;
        private static readonly ProfilerMarker RenderMarker = new ProfilerMarker("Field.AnimalMotion");

        public PetMotionPlayer Player => _player;
        public RawImage Surface => _surface;

        public void Configure(bool cat, bool dog = false)
        {
            _cat = cat;
            _dog = dog && _portraits != null;
            if (!cat) Release();
            else _surface.uvRect = new Rect(0, 0, 1, 1);
            RefreshVisibility();
        }

        /// <summary>Idle is the default standing wait; walking stays available for actual movement.</summary>
        public void SetDogWalking(bool walking) => _dogWalking = walking;


        public void ShowAnimal(AnimalSnapshot snapshot)
        {
            _stillCoat.Show(snapshot);
            _motionCoat.Show(snapshot);
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            if (!visible) Release();
            RefreshVisibility();
        }

        private void OnEnable() => RefreshVisibility();

        private void RefreshVisibility()
        {
            _still.enabled = _visible && !_cat && !_dog;
            _stillCoat.enabled = _still.enabled;
            _surface.gameObject.SetActive(_visible && (_cat || _dog));
        }

        private void LateUpdate()
        {
            using var sample = RenderMarker.Auto();
            if (!_visible) return;
            if (_dog)
            {
                _surface.texture = _portraits.ShibaAtlas;
                Rect uv = _dogWalking
                    ? PetPortraitSet.ShibaWalkUv(_clock.SceneSeconds)
                    : PetPortraitSet.ShibaIdleUv(_clock.SceneSeconds);
                if (_surface.uvRect != uv) _motionCoat.SetFrameUv(_surface, uv);
                return;
            }
            if (!_cat) return;
            Rect rect = _surface.rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            if (_player == null) CreateRenderer();
            float ratio = rect.width / rect.height;
            var size = ratio >= 1 ? new Vector2Int(_resolution, Mathf.Max(64,
                Mathf.RoundToInt(_resolution / ratio))) : new Vector2Int(Mathf.Max(64,
                Mathf.RoundToInt(_resolution * ratio)), _resolution);
            if (_target == null || _target.width != size.x || _target.height != size.y)
                CreateTarget(size, ratio);
            float seconds = _clock.SceneSeconds;
            if (_renderedSeconds == seconds && _player.Action == _action) return;
            // Manual sampling also freezes during guides, popup suspension and application backgrounding.
            if (_player.Action != _action) _player.Select(_action);
            _player.Seek(Mathf.Repeat(seconds, _player.Duration) / _player.Duration);
            _camera.Render();
            _renderedSeconds = seconds;
        }

        private void CreateRenderer()
        {
            if (_prefab == null || _clock == null)
                throw new InvalidOperationException("Missing field animal motion binding.");
            _player = Instantiate(_prefab, _renderPosition, Quaternion.identity);
            // The rig is independent of the UI transform, including battle jump, squash and rotation.
            _player.Playing = false;
            _camera = _player.GetComponentInChildren<Camera>(true);
            if (_camera == null) throw new InvalidOperationException("Missing field animal motion camera.");
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
        }

        private void CreateTarget(Vector2Int size, float ratio)
        {
            ReleaseTarget();
            _target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
            { name = "FieldCatMotion", useMipMap = false, autoGenerateMips = false };
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = ratio;
            _camera.orthographicSize = Mathf.Max(_halfHeight, _halfWidth / ratio);
            _surface.texture = _target;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void Release()
        {
            ReleaseTarget();
            if (_player == null) return;
            _player.gameObject.SetActive(false);
            Destroy(_player.gameObject);
            _player = null;
            _camera = null;
        }

        private void ReleaseTarget()
        {
            if (_surface != null) _surface.texture = null;
            if (_camera != null) _camera.targetTexture = null;
            _renderedSeconds = -1;
            if (_target == null) return;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
