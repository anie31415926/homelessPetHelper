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
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _selectedColor = new Color(0.86f, 0.94f, 0.89f);

        private long _instanceId;

        public event Action<long, bool> SelectionChanged;
        public event Action<long, bool> RetentionChanged;

        private void OnEnable()
        {
            _selection.onValueChanged.AddListener(OnSelectionChanged);
            _retention.onValueChanged.AddListener(OnRetentionChanged);
        }

        private void OnDisable()
        {
            _selection.onValueChanged.RemoveListener(OnSelectionChanged);
            _retention.onValueChanged.RemoveListener(OnRetentionChanged);
        }

        public void Bind(long instanceId, string title, string identity, Font font)
        {
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
        }

        public void Unbind()
        {
            SelectionChanged = null;
            RetentionChanged = null;
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
    }
}
