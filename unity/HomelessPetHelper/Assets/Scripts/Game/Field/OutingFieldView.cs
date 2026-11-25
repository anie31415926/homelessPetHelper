using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Framework.UI.Motion;
using HomelessPetHelper.Framework.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Explicit static scene bindings shared by both phases. No rule ownership.</summary>
    public sealed class OutingFieldView : MonoBehaviour
    {
        [SerializeField] private RectTransform _frame, _grid;
        [SerializeField] private Image _background, _animal, _foreground;
        [SerializeField] private AnimalCoatView _coat;
        [SerializeField] private FieldPetMotion _petMotion;
        public void ShowAnimal(AnimalSnapshot snapshot)
        {
            if (_petMotion != null) _petMotion.ShowAnimal(snapshot);
            else _coat.Show(snapshot);
        }
        [SerializeField] private Sprite _cat, _dog;
        [SerializeField] private bool _fitPlayableArea;
        [SerializeField] private OutingFieldGraphic _terrain;
        [SerializeField] private GameObject _sceneRoot;
        [SerializeField] private MotionScope _motion;
        [SerializeField] private OutingWeatherGraphic _weatherGraphic;
        public OutingFieldLayout Layout { get; private set; }
        public OutingWeather Weather { get; private set; }
        public RectTransform Frame => _frame;
        public Image Animal => _animal;
        public FieldSceneSnapshot Snapshot { get; private set; }
        public void SetPresentationPaused(bool paused)
        { if (_motion != null) _motion.SetScenePaused(paused); }

        public void SetWeather(OutingWeather weather)
        {
            Weather = Layout == null ? default : weather;
            if (_weatherGraphic != null)
                _weatherGraphic.Show(Layout?.IllustratedInterior == true ? default : Weather, _motion);
        }

        public void Configure(OutingFieldLayout layout, Sprite background, bool dog, ApproachSession opening,
            FieldSceneSnapshot snapshot = null, SceneSpritePalette props = null, OutingArtwork artwork = null)
        {
            Layout = layout;
            SetPresentationPaused(false);
            SetWeather(default);
            if (_petMotion != null) _petMotion.Configure(layout != null && !dog);
            _sceneRoot.SetActive(layout != null);
            _background.gameObject.SetActive(layout != null);
            if (layout == null) { Snapshot = null; return; }
            if (background == null || (dog ? _dog : _cat) == null)
                throw new InvalidOperationException("Missing field scene art.");
            _background.sprite = background;
            _background.GetComponent<AspectRatioFitter>().aspectRatio =
                background.rect.width / background.rect.height;
            ConfigureFrame(layout);
            ConfigureForeground(layout.IllustratedInterior ? artwork : null);
            _animal.sprite = dog ? _dog : _cat;
            _animal.preserveAspect = true;
            var grid = layout.Grid;
            _grid.anchorMin = new Vector2(grid.x / layout.Width, 1 - grid.yMax / layout.Height);
            _grid.anchorMax = new Vector2(grid.xMax / layout.Width, 1 - grid.y / layout.Height);
            _grid.offsetMin = _grid.offsetMax = Vector2.zero;
            float animalWidth = layout.AnimalWidthFor(dog) * 1.6f;
            var point = layout.CellPoint(layout.Opening.Get(ApproachMode.Cleanup).Animal);
            _animal.rectTransform.anchorMin = new Vector2((point.x - animalWidth / 2) / layout.Width,
                1 - (point.y + animalWidth * .15f) / layout.Height);
            _animal.rectTransform.anchorMax = new Vector2((point.x + animalWidth / 2) / layout.Width,
                1 - (point.y - animalWidth) / layout.Height);
            _animal.rectTransform.pivot = new Vector2(.5f, .15f);
            _animal.rectTransform.localRotation = Quaternion.identity;
            _animal.rectTransform.localScale = Vector3.one;
            _animal.rectTransform.anchoredPosition = Vector2.zero;
            _animal.rectTransform.offsetMin = _animal.rectTransform.offsetMax = Vector2.zero;
            _terrain.Configure(layout, props);
            if (snapshot != null) Show(snapshot);
            else if (opening != null) Refresh(opening);
        }

        private void ConfigureForeground(OutingArtwork artwork)
        {
            if (_foreground == null) return;
            _foreground.sprite = artwork?.Foreground;
            _foreground.enabled = _foreground.sprite != null;
            if (!_foreground.enabled) return;
            var rect = _foreground.rectTransform;
            rect.anchorMin = artwork.ForegroundBounds.min;
            rect.anchorMax = artwork.ForegroundBounds.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private readonly Vector3[] _backgroundCorners = new Vector3[4];

        private void OnEnable() => AlignIllustratedFrame();

        private void LateUpdate() => AlignIllustratedFrame();

        private void AlignIllustratedFrame()
        {
            if (Layout?.IllustratedInterior != true) return;
            // The cover image can crop differently than the safe area used by the controls.
            // Project the baked room and the playable floor through the same rectangle.
            var parent = (RectTransform)_frame.parent;
            _background.rectTransform.GetWorldCorners(_backgroundCorners);
            var min = (Vector2)parent.InverseTransformPoint(_backgroundCorners[0]) - parent.rect.min;
            var max = (Vector2)parent.InverseTransformPoint(_backgroundCorners[2]) - parent.rect.max;
            if ((_frame.offsetMin - min).sqrMagnitude > .01f) _frame.offsetMin = min;
            if ((_frame.offsetMax - max).sqrMagnitude > .01f) _frame.offsetMax = max;
        }

        private void ConfigureFrame(OutingFieldLayout layout)
        {
            var fitter = _frame.GetComponent<AspectRatioFitter>();
            if (layout.IllustratedInterior)
            {
                fitter.enabled = false;
                _frame.anchorMin = Vector2.zero;
                _frame.anchorMax = Vector2.one;
                AlignIllustratedFrame();
                return;
            }
            fitter.enabled = !_fitPlayableArea;
            if (!_fitPlayableArea) { fitter.aspectRatio = layout.Width / layout.Height; return; }
            // Presentation uses the playable bounds, while model coordinates and travel times stay intact.
            float cellHeight = layout.Grid.height / layout.Opening.Get(ApproachMode.Cleanup).Rows;
            float top = Mathf.Max(0, layout.Grid.y - cellHeight * 1.5f);
            float bottom = Mathf.Min(layout.Height, layout.Grid.yMax + cellHeight);
            float height = bottom - top;
            _frame.anchorMin = new Vector2(0, 1 - (layout.Height - top) / height);
            _frame.anchorMax = new Vector2(1, 1 + top / height);
            _frame.offsetMin = _frame.offsetMax = Vector2.zero;
        }

        public void Refresh(ApproachSession opening)
        {
            if (Layout == null) return;
            Show(new FieldSceneSnapshot(opening));
        }

        private void Show(FieldSceneSnapshot snapshot)
        {
            Snapshot = snapshot;
            if (_petMotion != null) _petMotion.SetVisible(snapshot.AnimalRevealed);
            else _animal.enabled = snapshot.AnimalRevealed;
            _terrain.Show(snapshot);
        }
    }
}
