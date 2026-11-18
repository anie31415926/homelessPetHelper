using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    public sealed class OptionCardView : MonoBehaviour
    {
        [SerializeField] private Text _title;
        [SerializeField] private Text _identity;
        [SerializeField] private Toggle _selection;
        [SerializeField] private Toggle _retention;
        [SerializeField] private Image _surface;
        [SerializeField] private Image _artwork;
        [SerializeField, Tooltip("Optional: compact rescue bars use hold; legacy cards use selection toggles.")]
        private HoldActionButton _hold;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectedColor = new Color(0.86f, 0.94f, 0.89f);

        private long _instanceId;

        public event Action<long, bool> SelectionChanged;
        public event Action<long, bool> RetentionChanged;
        public event Action<long> HoldConfirmed;

        private void OnEnable()
        {
            _selection.onValueChanged.AddListener(OnSelectionChanged);
            _retention.onValueChanged.AddListener(OnRetentionChanged);
            if (_hold != null) _hold.Confirmed += OnHoldConfirmed;
        }

        private void OnDisable()
        {
            _selection.onValueChanged.RemoveListener(OnSelectionChanged);
            _retention.onValueChanged.RemoveListener(OnRetentionChanged);
            if (_hold != null)
            {
                _hold.CancelHold();
                _hold.Confirmed -= OnHoldConfirmed;
            }
        }

        public void Bind(long instanceId, string title, string identity, Font font)
        {
            if (_hold != null) _hold.CancelHold();
            _instanceId = instanceId;
            _title.text = title;
            _identity.text = identity;
            foreach (Text text in GetComponentsInChildren<Text>(true))
            {
                text.font = font;
            }

            _artwork.gameObject.SetActive(_artwork.sprite != null);
        }

        public void Refresh(bool selected, bool retained, bool canRetain, bool interactable, bool selectable = true)
        {
            _selection.SetIsOnWithoutNotify(selected);
            _retention.SetIsOnWithoutNotify(retained);
            _selection.interactable = interactable && selectable;
            _retention.interactable = interactable && (retained || canRetain);
            _surface.color = selected ? _selectedColor : _normalColor;
            if (_hold != null)
            {
                _hold.interactable = interactable && selectable;
                if (!_hold.interactable) _hold.CancelHold();
                _identity.gameObject.SetActive(!selectable);
            }
        }

        public void Unbind()
        {
            SelectionChanged = null;
            RetentionChanged = null;
            HoldConfirmed = null;
            if (_hold != null) _hold.CancelHold();
            _instanceId = 0;
            _title.text = string.Empty;
            _identity.text = string.Empty;
            Refresh(false, false, false, false);
        }

        private void OnSelectionChanged(bool value)
        {
            SelectionChanged?.Invoke(_instanceId, value);
        }

        private void OnRetentionChanged(bool value)
        {
            RetentionChanged?.Invoke(_instanceId, value);
        }

        private void OnHoldConfirmed() => HoldConfirmed?.Invoke(_instanceId);
    }
}
