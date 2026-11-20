using UnityEngine;
using UnityEngine.UI;
using UiText = UnityEngine.UI.Text;

namespace HomelessPetHelper.Game.Approach
{
    public sealed class ApproachCommandView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private UiText _label;
        public Button Button => _button;
        public void Show(string caption, bool enabled)
        {
            _button.interactable = enabled;
            if (_label.text != caption) _label.text = caption;
        }
    }
}
