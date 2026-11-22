using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HomelessPetHelper.Framework.UI.Motion;

namespace HomelessPetHelper.Modules.Siege.UI
{
    public sealed class SiegeChoiceCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        ICancelHandler
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Text _label;
        [SerializeField] private Text _cost, _availability, _rosterLabel;
        [SerializeField] private GameObject _selectedMark, _costIcon;
        private int _id;
        private Action<int> _selected;
        [SerializeField] private SiegePropIcon _art;
        private Action<int, Vector2> _dragged, _dropped;
        private int? _pointer;
        private Action _cancelled;
        [SerializeField] private Color _normal = new Color32(242, 243, 244, 255);
        [SerializeField] private Color _selectedColor = new Color32(173, 225, 207, 255);
        [SerializeField] private UiMotionFeedback _motion;

        public void BindMotion(MotionScope scope) { if (_motion != null) _motion.BindScope(scope); }

        private void OnEnable() => _button.onClick.AddListener(Select);

        private void OnDisable()
        {
            _button.onClick.RemoveListener(Select);
            _selected = null;
            _id = 0;
            CancelDrag();
            _dragged = _dropped = null;
        }

        private void Select() => _selected?.Invoke(_id);

        public void SetArt(string visual)
        {
            if (_art == null) throw new InvalidOperationException("Missing hand card art binding.");
            _art.gameObject.SetActive(true);
            _art.Show(visual);
        }

        public void SetDragHandlers(Action<int, Vector2> dragged, Action<int, Vector2> dropped, Action cancelled)
        { _dragged = dragged; _dropped = dropped; _cancelled = cancelled; }
        public void CancelDrag()
        {
            if (!_pointer.HasValue) return;
            _pointer = null; _cancelled?.Invoke();
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (_pointer.HasValue || !_button.IsInteractable() || _dragged == null ||
                data.button != PointerEventData.InputButton.Left) return;
            _pointer = data.pointerId;
            data.eligibleForClick = false;
            _dragged?.Invoke(_id, data.position);
        }
        public void OnDrag(PointerEventData data)
        { if (_pointer == data.pointerId && _button.IsInteractable()) _dragged?.Invoke(_id, data.position); }
        public void OnEndDrag(PointerEventData data)
        {
            if (_pointer != data.pointerId) return;
            _pointer = null;
            if (_button.IsInteractable()) _dropped?.Invoke(_id, data.position);
        }
        public void OnCancel(BaseEventData data) => CancelDrag();

        public void SetCost(int cost, bool affordable, string unavailable, string planned = null)
        {
            if (_cost != null)
            {
                _cost.gameObject.SetActive(true);
                _cost.text = cost.ToString(CultureInfo.InvariantCulture);
                _costIcon.SetActive(true);
                _label.gameObject.SetActive(true);
                _rosterLabel.gameObject.SetActive(false);
            }
            if (_availability != null)
            {
                _availability.text = affordable ? planned ?? string.Empty : unavailable;
                _availability.gameObject.SetActive(!affordable || !string.IsNullOrEmpty(planned));
            }
        }

        public void SetSlotWidth(float width)
        {
            var layout = GetComponent<LayoutElement>();
            layout.flexibleWidth = 0;
            layout.preferredWidth = width;
        }

        public void Bind(int id, string label, bool selected, bool interactable, Action<int> onSelect)
        {
            if (_id != id || !interactable)
            {
                CancelDrag();
                if (_motion != null) _motion.ResetFeedback();
            }
            _id = id;
            if (_cost != null)
            {
                _cost.gameObject.SetActive(false);
                _costIcon.SetActive(false);
                _art.gameObject.SetActive(false);
                _availability.gameObject.SetActive(false);
                _label.gameObject.SetActive(false);
                _rosterLabel.gameObject.SetActive(true);
                _rosterLabel.text = label;
            }
            if (_selectedMark != null) _selectedMark.SetActive(selected);
            _selected = onSelect;
            if (_label.text != label)
                _label.text = label;
            _button.interactable = interactable;
            _button.image.color = selected ? _selectedColor : _normal;
            if (_motion != null) _motion.SetSelected(selected);
        }
    }
}
