using UnityEngine;
using UnityEngine.UI;

namespace HomelessPetHelper.Modules.Siege.UI
{
    // A short/tablet window gives space to controls, rather than shrinking the entire page.
    public sealed class SiegeTrialLayout : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _safeArea;

        [SerializeField]
        private LayoutElement _fieldLayout;
        private float _height = -1;

        private void LateUpdate()
        {
            float height = _safeArea.rect.height;
            if (Mathf.Approximately(_height, height))
                return;
            _height = height;
            _fieldLayout.minHeight = height < 1800 ? 200 : 340;
        }
    }
}
