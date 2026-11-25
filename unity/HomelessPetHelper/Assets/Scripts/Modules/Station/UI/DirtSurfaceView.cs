using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
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
        [SerializeField] private RawImage _fur;
        [SerializeField] private CareBubbleView _bubbles;
        [SerializeField] private Sprite _legacyCat, _legacyDog, _rescueCat, _rescueDog;
        [SerializeField] private TextAsset _catProjection, _dogProjection;
        private Texture2D _texture;
        private Color32[] _pixels;
        private Texture2D _furTexture;
        private Color32[] _furPixels;
        private bool _canClean, _breathing;
        private bool _pressed;
        private int _pointerId;
        private Vector2 _screenPoint;
        private Camera _eventCamera;
        private bool _focused = true;
        private bool _applicationPaused;
        private Sprite _inputArt;
        private CareCoatProjection _projection;
        private IReadOnlyDirtField _field, _furField;
        private Rect _coatRect;
        private PetPortraitView _portrait;
        private Vector3 _restScale;
        private float _breathTime;

        private void Awake() => _restScale = transform.localScale;
        internal void SetPortraitPaused(bool paused) => _portrait?.SetPaused(paused);

        public void ShowPortrait(PetPortraitSet art, AnimalSnapshot animal, bool visible)
        {
            if (art == null) return;
            if (_portrait == null) _portrait = PetPortraitView.Attach(_animal, art);
            _portrait.Show(art.IsStationDog(_inputArt), animal, visible);
            _dirt.enabled = !visible;
            _fur.enabled = !visible;
        }

        public event Action<Vector2, int> StrokeRequested;

        public void Refresh(Sprite animal, IReadOnlyDirtField field, IReadOnlyDirtField fur = null)
        {
            if (_animal == null || _dirt == null || field == null)
            {
                throw new InvalidOperationException("Dirt surface requires bound graphics and a field.");
            }

            BindArt(animal);
            _field = field;
            _furField = fur;
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
                _pixels[index] = _projection?.Paint(field, index, false) ??
                    new Color32(100, 82, 59, field.Sample(index));
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
            RefreshFur(fur);
        }

        private void RefreshFur(IReadOnlyDirtField fur)
        {
            _fur.gameObject.SetActive(fur != null);
            if (fur == null) return;
            if (_furTexture == null || _furTexture.width != fur.Width || _furTexture.height != fur.Height)
            {
                if (_furTexture != null) Destroy(_furTexture);
                _furTexture = new Texture2D(fur.Width, fur.Height, TextureFormat.RGBA32, false);
                _furTexture.name = "Station messy coat overlay";
                _furTexture.filterMode = FilterMode.Bilinear;
                _furTexture.wrapMode = TextureWrapMode.Clamp;
                _furPixels = new Color32[fur.Width * fur.Height];
                _fur.texture = _furTexture;
            }

            for (int index = 0; index < _furPixels.Length; index++)
            {
                _furPixels[index] = _projection?.Paint(fur, index, true) ??
                    new Color32(104, 117, 94, fur.Sample(index));
            }

            _furTexture.SetPixels32(_furPixels);
            _furTexture.Apply(false, false);
        }

        private void BindArt(Sprite animal)
        {
            if (_inputArt == animal && _animal.sprite != null) return;
            _inputArt = animal;
            bool cat = _rescueCat != null && animal == _legacyCat;
            bool dog = _rescueDog != null && animal == _legacyDog;
            _projection = cat || dog ? new CareCoatProjection(cat ? _catProjection : _dogProjection) : null;
            _animal.sprite = cat ? _rescueCat : dog ? _rescueDog : animal;
            FitCoat();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_projection != null) FitCoat();
        }

        private void FitCoat()
        {
            var surface = ((RectTransform)transform).rect;
            var size = surface.size;
            if (size.x <= 0 || size.y <= 0) return;
            if (_projection != null)
            {
                float aspect = _animal.sprite.rect.width / _animal.sprite.rect.height;
                size = new Vector2(Mathf.Min(size.x, size.y * aspect), Mathf.Min(size.y, size.x / aspect));
            }
            _coatRect = new Rect(surface.center - size / 2, size);
            if (_rescueCat == null && _rescueDog == null) return;
            var inset = (surface.size - size) / 2;
            FitGraphic(_animal.rectTransform, inset);
            FitGraphic(_dirt.rectTransform, inset);
            FitGraphic(_fur.rectTransform, inset);
        }

        private static void FitGraphic(RectTransform graphic, Vector2 inset)
        {
            graphic.offsetMin = inset;
            graphic.offsetMax = -inset;
        }

        public void ShowCleaningContact(Vector2 point)
        {
            // The preview still invokes its original callback. Main derives feedback from accepted strokes.
            if (_projection == null) _bubbles.Contact(point);
        }

        public void SetCleaning(bool canClean)
        {
            _canClean = canClean;
            _animal.raycastTarget = canClean;
            CancelPointer();
            if (!canClean) RestPose();
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
                CancelPointer(false);
            }
        }

        private void Update()
        {
            if (_canClean && _projection != null && _focused && !_applicationPaused && !_pressed)
            {
                _breathTime = Mathf.Repeat(_breathTime + Time.unscaledDeltaTime, 2.4f);
                float breath = .5f - .5f * Mathf.Cos(_breathTime * (2 * Mathf.PI / 2.4f));
                transform.localScale = Vector3.Scale(_restScale, new Vector3(1, 1 + .028f * breath, 1));
                _breathing = true;
            }
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
                var bounds = _projection == null ? rect.rect : _coatRect;
                if (!bounds.Contains(point)) return;
                Vector2 uv = Rect.PointToNormalized(bounds, point);
                if (_projection == null) StrokeRequested?.Invoke(uv, strength);
                else EmitProjectedStroke(rect.rect, point, uv, strength);
            }
        }

        private void EmitProjectedStroke(Rect surface, Vector2 local, Vector2 uv, int strength)
        {
            if (!_projection.TryTranslate(uv, out var destination)) return;
            long dirt = _field.RemainingAmount;
            long fur = _furField?.RemainingAmount ?? 0;
            StrokeRequested?.Invoke(destination, strength);
            Vector2 contact = Rect.PointToNormalized(surface, local);
            if (_field.RemainingAmount < dirt) _bubbles.Contact(contact);
            else if (_furField != null && _furField.RemainingAmount < fur) _bubbles.Contact(contact, true);
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

        private void CancelPointer(bool clearFeedback = true)
        {
            _pressed = false;
            _eventCamera = null;
            if (clearFeedback) _bubbles.ClearFeedback();
        }

        private void OnDisable()
        {
            RestPose();
            CancelPointer();
            ReleaseTexture();
        }

        private void RestPose()
        {
            if (!_breathing) return;
            transform.localScale = _restScale;
            _breathing = false;
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

            if (_fur != null) _fur.texture = null;
            if (_furTexture != null)
            {
                Destroy(_furTexture);
                _furTexture = null;
                _furPixels = null;
            }
        }
    }
}
