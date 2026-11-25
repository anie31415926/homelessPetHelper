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
        [SerializeField] private Text _stateLabel;
        [SerializeField] private Outline _selectionOutline;

        public string PointId => _pointId;
        internal Button Button => _button;

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(_pointId) || _pointId != _pointId.Trim() ||
                _button == null || _button.image == null || _highlight == null || _label == null ||
                _stateLabel == null || _selectionOutline == null)
                throw new InvalidOperationException("Incomplete city map point binding: " + _pointId);
            if (_highlight == _button.image || !_highlight.transform.IsChildOf(_button.transform) ||
                !_label.transform.IsChildOf(_highlight.transform) ||
                !_stateLabel.transform.IsChildOf(_highlight.transform) || _stateLabel == _label ||
                _selectionOutline.gameObject != _highlight.gameObject)
                throw new InvalidOperationException("Point artwork, label and highlight must be separate: " + _pointId);
        }

        internal void Render(string label, string stateLabel, bool enabled, Sprite paper, bool hidden)
        {
            _label.text = label;
            if (_label.enabled == hidden) _label.enabled = !hidden;
            _stateLabel.text = stateLabel;
            _button.interactable = enabled;
            _highlight.sprite = paper;
            _highlight.color = Color.clear;
            _stateLabel.gameObject.SetActive(false);
            _selectionOutline.enabled = false;
        }
    }
}
