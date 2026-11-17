using UnityEngine;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaView : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            Rect area = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (resolution.x <= 0 || resolution.y <= 0 ||
                (area == _lastSafeArea && resolution == _lastResolution))
            {
                return;
            }

            _lastSafeArea = area;
            _lastResolution = resolution;
            _rectTransform.anchorMin = new Vector2(area.xMin / resolution.x, area.yMin / resolution.y);
            _rectTransform.anchorMax = new Vector2(area.xMax / resolution.x, area.yMax / resolution.y);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
