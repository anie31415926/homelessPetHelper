using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Framework.UI.Motion;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Game.Field
{
    /// <summary>Explicit static scene bindings shared by both phases. No rule ownership.</summary>
    public sealed class OutingFieldView : MonoBehaviour
    {
        [SerializeField] private RectTransform _frame, _grid;
        [SerializeField] private Image _background, _animal;
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
        public OutingFieldLayout Layout { get; private set; }
        public RectTransform Frame => _frame;
        public Image Animal => _animal;
        public FieldSceneSnapshot Snapshot { get; private set; }
        public void SetPresentationPaused(bool paused)
        { if (_motion != null) _motion.SetScenePaused(paused); }

        public void Configure(OutingFieldLayout layout, Sprite background, bool dog, ApproachSession opening,
            FieldSceneSnapshot snapshot = null)
        {
            Layout = layout;
            SetPresentationPaused(false);
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
            _animal.rectTransform.anchoredPosition = Vector2.zero;
            _animal.rectTransform.offsetMin = _animal.rectTransform.offsetMax = Vector2.zero;
            _terrain.Configure(layout);
            if (snapshot != null) Show(snapshot);
            else if (opening != null) Refresh(opening);
        }

        private void ConfigureFrame(OutingFieldLayout layout)
        {
            var fitter = _frame.GetComponent<AspectRatioFitter>();
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
            _terrain.Show(snapshot);
        }
    }
}
