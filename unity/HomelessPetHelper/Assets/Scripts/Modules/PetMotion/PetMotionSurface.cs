using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.PetMotion
{
    public sealed class PetMotionSurface : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private RawImage _surface;
        [SerializeField] private PetMotionPlayer _player;
        private RenderTexture _target;
        private int _revision = -1;
        private Vector2Int _size;

        private void LateUpdate()
        {
            Rect rect = _surface.rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0)
                return;
            float ratio = rect.width / rect.height;
            var size = ratio >= 1 ? new Vector2Int(768, Mathf.Max(64, Mathf.RoundToInt(768 / ratio))) :
                new Vector2Int(Mathf.Max(64, Mathf.RoundToInt(768 * ratio)), 768);
            if (_size != size || _target == null)
            {
                Release();
                _size = size;
                _target = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
                {
                    name = "PetMotionPreviewTarget",
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                };
                _target.Create();
                _surface.texture = _target;
                _camera.targetTexture = _target;
                _camera.aspect = ratio;
                _camera.orthographicSize = Mathf.Max(320, 285 / ratio);
                _revision = -1;
            }
            if (_revision == _player.Revision)
                return;
            _camera.Render();
            _revision = _player.Revision;
        }

        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void Release()
        {
            if (_surface != null) _surface.texture = null;
            if (_camera != null) _camera.targetTexture = null;
            if (_target == null)
                return;
            _target.Release();
            Destroy(_target);
            _target = null;
        }
    }
}
