using System;
using HomelessPetHelper.Modules.Approach;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HomelessPetHelper.Game.Approach
{
    /// <summary>Single-pointer capture for the static grid. Cancel/disable always releases contact.</summary>
    public sealed class ApproachBoardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IPointerExitHandler, ICancelHandler
    {
        [SerializeField] private RectTransform _rect;
        [SerializeField] private ApproachCellView[] _cells;
        private int? _pointer;
        public event Action<int> Pressed, Released;
        public int ContactCell { get; private set; } = -1;
        public bool IsHeld => _pointer.HasValue;
        public int CellCount => _cells.Length;
        public ApproachCellView Cell(int cell) => _cells[cell];

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
            int col = Mathf.FloorToInt(x * ApproachCatalog.BoardColumns);
            int row = Mathf.FloorToInt(y * ApproachCatalog.BoardRows);
            return col >= 0 && col < ApproachCatalog.BoardColumns && row >= 0 && row < ApproachCatalog.BoardRows ?
                row * ApproachCatalog.BoardColumns + col : -1;
        }
    }
}
