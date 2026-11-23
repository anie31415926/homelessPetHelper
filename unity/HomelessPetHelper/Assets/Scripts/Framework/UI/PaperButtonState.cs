using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Neutral paper for unavailable controls. The label and hit target stay unchanged.</summary>
    public sealed class PaperButtonState : MonoBehaviour
    {
        [SerializeField] private Selectable _control;
        [SerializeField] private Image _surface;
        [SerializeField] private UiLayoutMetrics _metrics;
        private Sprite _availablePaper;
        private bool? _available;

        private void Awake() => _availablePaper = _surface.sprite;
        private void OnEnable() { _available = null; Refresh(); }
        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            bool available = _control.IsInteractable();
            if (_available == available) return;
            _available = available;
            _surface.sprite = available ? _availablePaper : _metrics.disabledButtonPaper;
            _surface.color = available ? Color.white : _metrics.disabledPaperTint;
        }
    }
}
