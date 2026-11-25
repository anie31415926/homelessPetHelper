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
        [SerializeField] private Sprite _detailArt;
        [SerializeField] private Sprite _requestArt;

        public string PointId => _pointId;
        internal Button Button => _button;
        internal Sprite DetailArt => _detailArt;
        private float _badgeTime;
        private RectTransform _badge;

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

        internal void Render(string label, string stateLabel, bool enabled, Sprite paper, Sprite badgePaper,
            bool hidden, bool request)
        {
            _label.text = label;
            if (_label.enabled == hidden) _label.enabled = !hidden;
            _stateLabel.text = stateLabel;
            _button.interactable = enabled;
            _highlight.sprite = paper;
            _highlight.color = Color.clear;
            if (request && _badge == null) CreateBadge(badgePaper);
            if (_badge != null && _badge.gameObject.activeSelf != request)
            {
                _badgeTime = 0;
                _badge.localScale = Vector3.one;
                _badge.gameObject.SetActive(request);
            }
            _stateLabel.gameObject.SetActive(request);
            _selectionOutline.enabled = false;
        }

        private void CreateBadge(Sprite paper)
        {
            var background = new GameObject("HelpBadge", typeof(RectTransform), typeof(Image));
            _badge = (RectTransform)background.transform;
            _badge.SetParent(_label.rectTransform, false);
            _badge.anchorMin = _badge.anchorMax = new Vector2(.5f, .5f);
            _badge.pivot = new Vector2(.5f, 1);
            _badge.anchoredPosition = Vector2.down * (_label.fontSize * .7f);
            _badge.sizeDelta = new Vector2(176, 64);
            var image = background.GetComponent<Image>();
            image.sprite = paper;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            var text = _stateLabel.rectTransform;
            text.SetParent(_badge, false);
            text.anchorMin = new Vector2(_requestArt == null ? 0 : .32f, 0);
            text.anchorMax = Vector2.one;
            text.offsetMin = text.offsetMax = Vector2.zero;
            _stateLabel.raycastTarget = false;
            if (_requestArt == null) return;
            var icon = new GameObject("HelpAnimal", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(_badge, false);
            icon.rectTransform.anchorMin = new Vector2(.02f, .08f);
            icon.rectTransform.anchorMax = new Vector2(.34f, .92f);
            icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
            icon.sprite = _requestArt;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        internal void AdvanceBadge(float elapsed)
        {
            if (_badge == null || !_badge.gameObject.activeInHierarchy || _badgeTime >= 1.2f) return;
            _badgeTime = Mathf.Min(1.2f, _badgeTime + elapsed);
            float pulse = Mathf.Sin(Mathf.PI * _badgeTime / 1.2f);
            _badge.localScale = Vector3.one * (1 + .12f * pulse);
        }
    }
}
