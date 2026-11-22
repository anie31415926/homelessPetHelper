using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Approach;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HomelessPetHelper.Game.Approach
{
    /// <summary>Bounded cell reuse and single-pointer capture for a configured grid.</summary>
    public sealed class ApproachBoardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerExitHandler, ICancelHandler
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private ApproachCellView[] _cells;
        private int? _pointer;
        private readonly List<ApproachCellView> _pool = new List<ApproachCellView>();
        private int _columns, _rows, _count;
        public event Action<int> Pressed, Released;
        public int ContactCell { get; private set; } = -1;
        public bool IsHeld => _pointer.HasValue;
        public int CellCount => _count == 0 ? _cells.Length : _count;
        public int Columns => _columns;
        public int Rows => _rows;
        public int PooledCells => _pool.Count;
        public ApproachCellView Cell(int cell) => _pool.Count == 0 ? _cells[cell] :
            cell >= 0 && cell < CellCount ? _pool[cell] : throw new ArgumentOutOfRangeException(nameof(cell));

        public void Configure(ApproachTemplate template)
        {
            if (template == null || template.CellCount > ApproachCatalog.MaxCells ||
                _rect == null || _cells == null || _cells.Length == 0 || _cells[0] == null)
                throw new ArgumentException("Missing authored board cell template.");
            CancelContact();
            if (_pool.Count == 0) _pool.AddRange(_cells);
            while (_pool.Count < template.CellCount)
            {
                // Repeat only the explicitly bound cell, never construct a panel tree at runtime.
                var cell = Instantiate(_cells[0], _cells[0].transform.parent);
                cell.gameObject.SetActive(false);
                _pool.Add(cell);
            }
            _columns = template.Columns; _rows = template.Rows; _count = template.CellCount;
            var ratio = _rect.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            if (ratio != null) ratio.aspectRatio = (float)_columns / _rows;
            for (int index = 0; index < _pool.Count; index++)
            {
                var cell = _pool[index];
                cell.gameObject.SetActive(index < _count);
                if (index >= _count) continue;
                var rect = (RectTransform)cell.transform;
                Vector2 insetMin = rect.offsetMin, insetMax = rect.offsetMax;
                int column = index % _columns, row = index / _columns;
                rect.anchorMin = new Vector2((float)column / _columns, 1f - (float)(row + 1) / _rows);
                rect.anchorMax = new Vector2((float)(column + 1) / _columns, 1f - (float)row / _rows);
                rect.offsetMin = insetMin; rect.offsetMax = insetMax;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

        public void OnPointerDown(PointerEventData data)
        {
            if (!isActiveAndEnabled || _pointer.HasValue || data.button != PointerEventData.InputButton.Left) return;
            int cell = Hit(data);
            if (cell < 0) return;
            _pointer = data.pointerId;
            ContactCell = cell;
            Pressed?.Invoke(cell);
        }

        public void OnDrag(PointerEventData data)
        {
            if (_pointer != data.pointerId) return;
            ContactCell = Hit(data);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pointer != data.pointerId) return;
            int cell = Hit(data);
            CancelContact();
            if (cell >= 0) Released?.Invoke(cell);
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (_pointer == data.pointerId) ContactCell = -1;
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (_pointer == data.pointerId) CancelContact();
        }

        public void OnCancel(BaseEventData data) => CancelContact();

        public void CancelContact()
        {
            _pointer = null;
            ContactCell = -1;
        }
        private void OnDisable() => CancelContact();

        private int Hit(PointerEventData data)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, data.position,
                data.pressEventCamera, out var point) || !_rect.rect.Contains(point)) return -1;
            float x = (point.x - _rect.rect.xMin) / _rect.rect.width;
            float y = (_rect.rect.yMax - point.y) / _rect.rect.height;
            int col = Mathf.FloorToInt(x * _columns);
            int row = Mathf.FloorToInt(y * _rows);
            return col >= 0 && col < _columns && row >= 0 && row < _rows ? row * _columns + col : -1;
        }
    }
}
