using UnityEngine;

namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Keeps an authored popup and its cloned dropdown list inside its saved bounds.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoundedPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform _bounds;
        private readonly Vector3[] _corners = new Vector3[4];

        private void LateUpdate()
        {
            var rect = (RectTransform)transform;
            rect.GetWorldCorners(_corners);
            Vector3 bottom = _bounds.InverseTransformPoint(_corners[0]);
            Vector3 top = _bounds.InverseTransformPoint(_corners[2]);
            Rect available = _bounds.rect;
            float x = top.x > available.xMax ? available.xMax - top.x :
                bottom.x < available.xMin ? available.xMin - bottom.x : 0;
            float y = top.y > available.yMax ? available.yMax - top.y :
                bottom.y < available.yMin ? available.yMin - bottom.y : 0;
            rect.position += _bounds.TransformVector(new Vector3(x, y, 0));
        }
    }
}
