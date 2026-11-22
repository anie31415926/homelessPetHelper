using System;
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
        [SerializeField] private Sprite _cat, _dog;
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
            _sceneRoot.SetActive(layout != null);
            _background.gameObject.SetActive(layout != null);
            if (layout == null) { Snapshot = null; return; }
            if (background == null || (dog ? _dog : _cat) == null)
                throw new InvalidOperationException("Missing field scene art.");
            _background.sprite = background;
            _background.GetComponent<AspectRatioFitter>().aspectRatio =
                background.rect.width / background.rect.height;
            _frame.GetComponent<AspectRatioFitter>().aspectRatio = layout.Width / layout.Height;
            _animal.sprite = dog ? _dog : _cat;
            _animal.preserveAspect = true;
            var grid = layout.Grid;
            _grid.anchorMin = new Vector2(grid.x / layout.Width, 1 - grid.yMax / layout.Height);
            _grid.anchorMax = new Vector2(grid.xMax / layout.Width, 1 - grid.y / layout.Height);
            _grid.offsetMin = _grid.offsetMax = Vector2.zero;
            float animalWidth = layout.AnimalWidthFor(dog);
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
