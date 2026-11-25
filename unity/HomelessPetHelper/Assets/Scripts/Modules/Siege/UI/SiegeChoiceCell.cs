using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Framework.UI.Motion;

namespace HomelessPetHelper.Modules.Siege.UI
{
    public sealed class SiegeChoiceCell : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        ICancelHandler, IPointerDownHandler, IPointerUpHandler
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
        private Action<int> _inspect;
        private int? _pressPointer;
        private float _pressAt;
        private bool _compact, _suppressClick;
        private const float InspectHoldSeconds = .5f;
        [SerializeField] private Color _normal = new Color32(242, 243, 244, 255);
        [SerializeField] private Color _selectedColor = new Color32(173, 225, 207, 255);
        [SerializeField] private UiMotionFeedback _motion;

        public void BindMotion(MotionScope scope) { if (_motion != null) _motion.BindScope(scope); }

        /// <summary>A guide must not open its only hole over a card the player cannot press.</summary>
        public bool IsClickable => GuideTargetRules.IsClickable(_button);

        private void OnEnable() => _button.onClick.AddListener(Select);

        private void OnDisable()
        {
            _button.onClick.RemoveListener(Select);
            _selected = null;
            _id = 0;
            CancelDrag();
            _dragged = _dropped = null;
            _inspect = null; _pressPointer = null; _suppressClick = false;
        }

        private void Select()
        {
            if (!_suppressClick) _selected?.Invoke(_id);
            _suppressClick = false;
        }

        public void SetCompact(Action<int> inspect)
        {
            _inspect = inspect;
            if (_compact || inspect == null) return;
            _compact = true;
            Place(_art.rectTransform, .055f, .12f, .32f, .88f);
            Place(_label.rectTransform, .34f, .45f, .96f, .9f);
            Place((RectTransform)_costIcon.transform, .44f, .11f, .59f, .43f);
            Place(_cost.rectTransform, .61f, .11f, .94f, .43f);
            _cost.alignment = TextAnchor.MiddleLeft;
        }

        private static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData data)
        {
            _suppressClick = false;
            if (_inspect == null || !_button.IsInteractable() || data.button != PointerEventData.InputButton.Left)
                return;
            _pressPointer = data.pointerId;
            _pressAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (_pressPointer != data.pointerId) return;
            _pressPointer = null;
            if (Time.unscaledTime - _pressAt < InspectHoldSeconds || !_button.IsInteractable()) return;
            _suppressClick = true;
            data.eligibleForClick = false;
            _inspect?.Invoke(_id);
        }

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
            _pressPointer = null;
            if (!_pointer.HasValue) return;
            _pointer = null; _cancelled?.Invoke();
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (_pointer.HasValue || !_button.IsInteractable() || _dragged == null ||
                data.button != PointerEventData.InputButton.Left) return;
            _pressPointer = null;
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
                _availability.gameObject.SetActive(!_compact && (!affordable || !string.IsNullOrEmpty(planned)));
                if (_compact)
                {
                    // Keep the price/name readable; mute the shared paper surface.
                    if (!affordable) _button.image.color = new Color32(207, 201, 185, 255);
                }
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
