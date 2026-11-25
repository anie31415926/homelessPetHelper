using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Animals.UI
{
    [DisallowMultipleComponent]
    public sealed class PetPortraitView : MonoBehaviour
    {
        [SerializeField] private PetPortraitSet _art;
        [SerializeField] private Image _replacedImage;
        [SerializeField] private bool _followImage;
        [SerializeField] private bool _showOnEnable, _startAsDog;
        private Sprite _lastSprite;
        private RawImage _surface;
        private AnimalCoatView _coat;
        private bool _dog, _visible, _paused, _backgrounded;
        private float _seconds;

        private void OnEnable()
        {
            if (_showOnEnable) Show(_startAsDog);
        }

        public static PetPortraitView Attach(Image image, PetPortraitSet art)
        {
            if (art == null) return null;
            var view = image.gameObject.AddComponent<PetPortraitView>();
            view._art = art;
            view._replacedImage = image;
            return view;
        }

        public void Show(bool dog) => Show(dog, null);
        public void SetPaused(bool paused) => _paused = paused;
        public void Advance(float seconds) => _seconds = Mathf.Repeat(_seconds + seconds, 20);

        private void OnApplicationPause(bool paused) => _backgrounded = paused;

        public void Show(bool dog, AnimalSnapshot animal, bool visible = true)
        {
            _dog = dog;
            _visible = visible;
            if (_surface == null && visible)
            {
                _surface = new GameObject("AnimatedPet", typeof(RectTransform), typeof(RawImage))
                    .GetComponent<RawImage>();
                _surface.transform.SetParent(transform, false);
                _surface.raycastTarget = false;
                _coat = _surface.gameObject.AddComponent<AnimalCoatView>();
                _coat.Bind(_surface, _art.CoatShader);
            }
            if (_surface != null) _coat.Show(animal);
            if (_replacedImage != null) _replacedImage.enabled = !visible;
            if (_surface != null && _surface.gameObject.activeSelf != visible)
                _surface.gameObject.SetActive(visible);
            if (visible) Sample();
        }

        private void LateUpdate()
        {
            if (_followImage && _lastSprite != _replacedImage.sprite)
            {
                _lastSprite = _replacedImage.sprite;
                Show(_art.IsStationDog(_lastSprite), null, _lastSprite != null);
            }
            if (!_visible) return;
            var rect = ((RectTransform)transform).rect;
            float size = Mathf.Min(rect.width, rect.height);
            if (size <= 0) return;
            _surface.rectTransform.sizeDelta = Vector2.one * size;
            if (!_paused && !_backgrounded) Advance(Time.unscaledDeltaTime);
            Sample();
        }

        private void Sample()
        {
            _surface.texture = _dog ? _art.ShibaAtlas : _art.CatAtlas;
            float phase = Mathf.PingPong(_seconds * 5, 5);
            Rect uv = _dog ? PetPortraitSet.ShibaFrameUv(10 + Mathf.RoundToInt(phase)) :
                PetPortraitSet.CatPortraitUv(_seconds);
            if (_surface.uvRect != uv) _coat.SetFrameUv(_surface, uv);
        }
    }
}
