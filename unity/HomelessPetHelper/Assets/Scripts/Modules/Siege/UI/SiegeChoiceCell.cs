using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    public sealed class SiegeChoiceCell : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [SerializeField]
        private Text _label;
        private int _id;
        private Action<int> _selected;
        private static readonly Color Normal = new Color32(242, 243, 244, 255);
        private static readonly Color Selected = new Color32(173, 225, 207, 255);

        private void OnEnable() => _button.onClick.AddListener(Select);

        private void OnDisable()
        {
            _button.onClick.RemoveListener(Select);
            _selected = null;
            _id = 0;
        }

        private void Select() => _selected?.Invoke(_id);

        public void SetSlotWidth(float width)
        {
            var layout = GetComponent<LayoutElement>();
            layout.flexibleWidth = 0;
            layout.preferredWidth = width;
        }

        public void Bind(int id, string label, bool selected, bool interactable, Action<int> onSelect)
        {
            _id = id;
            _selected = onSelect;
            if (_label.text != label)
                _label.text = label;
            _button.interactable = interactable;
            _button.image.color = selected ? Selected : Normal;
        }
    }
}
