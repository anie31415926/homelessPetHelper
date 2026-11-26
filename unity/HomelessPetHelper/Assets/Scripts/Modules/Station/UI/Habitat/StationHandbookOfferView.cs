using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Prefab template for one build row. The sheet reuses these cells.</summary>
    public sealed class StationHandbookOfferView : MonoBehaviour
    {
        [SerializeField] private Image _surface;
        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;
        [SerializeField] private Text _state;
        [SerializeField] private Button _button;
        [SerializeField] private Sprite _availablePaper;
        [SerializeField] private Sprite _ownedPaper;
        [SerializeField] private Color _idleState = new Color32(88, 104, 92, 200);
        [SerializeField] private Color _ownedState = new Color32(58, 102, 74, 255);

        public void Bind(HandbookOffer offer, Sprite fallbackIcon, Action open)
        {
            Sprite icon = offer.Icon != null ? offer.Icon : fallbackIcon;
            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }
            if (_label != null) _label.text = offer.Title;
            if (_state != null)
            {
                _state.text = offer.State;
                _state.color = offer.Owned ? _ownedState : _idleState;
            }
            if (_surface != null)
                _surface.sprite = offer.Owned && _ownedPaper != null ? _ownedPaper : _availablePaper;
            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            if (open != null) _button.onClick.AddListener(() => open());
        }

        public Button Button => _button;
    }
}
