using System;
using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Prefab template for one memorial card. The sheet reuses these cells.</summary>
    public sealed class StationHandbookMemoryView : MonoBehaviour
    {
        [SerializeField] private Image _portrait;
        [SerializeField] private Text _label;
        [SerializeField] private Button _button;

        public void Bind(HandbookMemory memory, Action open)
        {
            if (_portrait != null)
            {
                _portrait.sprite = memory.Portrait;
                _portrait.enabled = memory.Portrait != null;
            }
            if (_label != null) _label.text = memory.Name;
            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            if (open != null) _button.onClick.AddListener(() => open());
        }

        public Button Button => _button;
    }
}
