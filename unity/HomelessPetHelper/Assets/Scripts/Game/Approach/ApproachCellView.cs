using HomelessPetHelper.Game.Text;
using UnityEngine;
using UnityEngine.UI;
using UiText = UnityEngine.UI.Text;

namespace HomelessPetHelper.Game.Approach
{
    public sealed class ApproachCellView : MonoBehaviour
    {
        [SerializeField] private Image _surface;
        [SerializeField] private UiText _label;
        private string _caption;
        private int _percent = -1;
        private bool _hasValue;

        public void Show(Color color, string caption, int percent, OptionPreviewText text)
        {
            if (_surface.color != color) _surface.color = color;
            if (_hasValue && _caption == caption && _percent == percent) return;
            _caption = caption;
            _percent = percent;
            _hasValue = true;
            _label.text = percent < 0 ? caption : text.Get("cell.work", caption, percent);
        }
    }
}
