using System;
using HomelessPetHelper.Modules.Station.Cleaning;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI
{
    public sealed class DirtSurfaceView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private Image _animal;
        [SerializeField] private RawImage _dirt;
        private Texture2D _texture;
        private Color32[] _pixels;
        private bool _canClean;
        private bool _pressed;
        private int _pointerId;
        private Vector2 _screenPoint;
        private Camera _eventCamera;
        private bool _focused = true;
        private bool _applicationPaused;

        public event Action<Vector2, int> StrokeRequested;

        public void Refresh(Sprite animal, DirtField field)
        {
            if (_animal == null || _dirt == null || field == null)
            {
                throw new InvalidOperationException("Dirt surface requires bound graphics and a field.");
            }

            _animal.sprite = animal;
            if (_texture == null || _texture.width != field.Width || _texture.height != field.Height)
            {
                ReleaseTexture();
                _texture = new Texture2D(field.Width, field.Height, TextureFormat.RGBA32, false);
                _texture.name = "Station dirt overlay";
                _texture.filterMode = FilterMode.Bilinear;
                _texture.wrapMode = TextureWrapMode.Clamp;
                _pixels = new Color32[field.Width * field.Height];
                _dirt.texture = _texture;
            }

            for (int index = 0; index < _pixels.Length; index++)
            {
                _pixels[index] = new Color32(100, 82, 59, field.Sample(index));
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
        }

        public void SetCleaning(bool canClean)
        {
            _canClean = canClean;
            _animal.raycastTarget = canClean;
            CancelPointer();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!_canClean || _pressed || !_focused || _applicationPaused ||
                data.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pressed = true;
            _pointerId = data.pointerId;
            _screenPoint = data.position;
            _eventCamera = data.pressEventCamera;
            EmitStroke(24);
        }

        public void OnDrag(PointerEventData data)
        {
            if (_pressed && data.pointerId == _pointerId)
            {
                _screenPoint = data.position;
                EmitStroke(24);
            }
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pressed && data.pointerId == _pointerId)
            {
                CancelPointer();
            }
        }

        private void Update()
        {
            if (_pressed && _canClean && _focused && !_applicationPaused)
            {
                int strength = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 320), 1, 16);
                EmitStroke(strength);
            }
        }

        private void EmitStroke(int strength)
        {
            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, _screenPoint, _eventCamera,
                out Vector2 point))
            {
                float x = (point.x - rect.rect.xMin) / rect.rect.width;
                float y = (point.y - rect.rect.yMin) / rect.rect.height;
                if (x >= 0 && x <= 1 && y >= 0 && y <= 1)
                {
                    StrokeRequested?.Invoke(new Vector2(x, y), strength);
                }
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            CancelPointer();
        }

        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            CancelPointer();
        }

        private void CancelPointer()
        {
            _pressed = false;
            _eventCamera = null;
        }

        private void OnDisable()
        {
            CancelPointer();
            ReleaseTexture();
        }

        private void OnDestroy()
        {
            ReleaseTexture();
        }

        private void ReleaseTexture()
        {
            if (_dirt != null)
            {
                _dirt.texture = null;
            }

            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
                _pixels = null;
            }
        }
    }
}
