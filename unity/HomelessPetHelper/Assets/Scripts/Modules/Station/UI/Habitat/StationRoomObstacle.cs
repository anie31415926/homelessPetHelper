using HomelessPetHelper.Modules.Station.Habitat;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    /// <summary>Editable normalized ground collider consumed by the actual wander logic.</summary>
    public sealed class StationRoomObstacle : MonoBehaviour
    {
        [SerializeField] private Vector2 _center;
        [SerializeField] private Vector2 _radius;
        [SerializeField] private Vector2 _clearance = new Vector2(.04f, .018f);
        public HabitatPoint Center => new HabitatPoint(_center.x, _center.y);
        public HabitatPoint Radius => new HabitatPoint(_radius.x + _clearance.x, _radius.y + _clearance.y);

        private void OnDrawGizmosSelected()
        {
            var rect = (RectTransform)transform;
            Gizmos.color = Color.cyan;
            Vector3 previous = Point(rect, 0);
            for (int i = 1; i <= 48; i++)
            {
                Vector3 next = Point(rect, i * Mathf.PI * 2 / 48);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
        private Vector3 Point(RectTransform rect, float angle) => rect.TransformPoint(new Vector3(
            rect.rect.xMin + (_center.x + Mathf.Cos(angle) * (_radius.x + _clearance.x)) * rect.rect.width,
            rect.rect.yMin + (_center.y + Mathf.Sin(angle) * (_radius.y + _clearance.y)) * rect.rect.height));
    }
}
