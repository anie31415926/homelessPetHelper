using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Outing.UI
{
    [Serializable]
    public sealed class CityMapPointBinding
    {
        [SerializeField] private string _pointId;
        [SerializeField] private Button _button;
        [SerializeField] private Image _highlight;
        [SerializeField] private Text _label;

        public string PointId => _pointId;
        internal Button Button => _button;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(_pointId) || _pointId != _pointId.Trim() ||
                _button == null || _button.image == null || _highlight == null || _label == null)
                throw new InvalidOperationException("Incomplete city map point binding: " + _pointId);
            if (_highlight == _button.image || !_highlight.transform.IsChildOf(_button.transform) ||
                !_label.transform.IsChildOf(_highlight.transform))
                throw new InvalidOperationException("Point artwork, label and highlight must be separate: " + _pointId);
        }

        internal void Render(string label, bool enabled, Color highlight)
        {
            _label.text = label;
            _button.interactable = enabled;
            _highlight.color = highlight;
        }
    }
}
