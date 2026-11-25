#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaView : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;
#if UNITY_WEBGL && !UNITY_EDITOR
        private Rect _channelArea;
        private bool _refreshChannel = true;
        private bool _hasChannelArea;

        [DllImport("__Internal")]
        private static extern int HPHReadWeChatSafeArea(out Vector4 edges);

        private void OnEnable() => _refreshChannel = true;
        private void OnApplicationFocus(bool focused) => _refreshChannel |= focused;
        private void OnApplicationPause(bool paused) => _refreshChannel |= !paused;
#endif

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (resolution.x <= 0 || resolution.y <= 0) return;

            Rect area = Screen.safeArea;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_refreshChannel || resolution != _lastResolution)
            {
                _hasChannelArea = HPHReadWeChatSafeArea(out var edges) != 0;
                _channelArea = Rect.MinMaxRect(edges.x, edges.y, edges.z, edges.w);
                _refreshChannel = false;
            }
            if (_hasChannelArea) area = IntersectChannelArea(area, resolution);
#endif
            if (area == _lastSafeArea && resolution == _lastResolution) return;

            _lastSafeArea = area;
            _lastResolution = resolution;
            _rectTransform.anchorMin = new Vector2(area.xMin / resolution.x, area.yMin / resolution.y);
            _rectTransform.anchorMax = new Vector2(area.xMax / resolution.x, area.yMax / resolution.y);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private Rect IntersectChannelArea(Rect unityArea, Vector2Int resolution)
        {
            Rect channelArea = Rect.MinMaxRect(_channelArea.xMin * resolution.x, _channelArea.yMin * resolution.y,
                _channelArea.xMax * resolution.x, _channelArea.yMax * resolution.y);
            Rect shared = Rect.MinMaxRect(Mathf.Max(unityArea.xMin, channelArea.xMin),
                Mathf.Max(unityArea.yMin, channelArea.yMin), Mathf.Min(unityArea.xMax, channelArea.xMax),
                Mathf.Min(unityArea.yMax, channelArea.yMax));
            return shared.width > 0 && shared.height > 0 ? shared : channelArea;
        }
#endif
    }
}
