using HomelessPetHelper.Modules.Station.Habitat;
using UnityEngine;

namespace HomelessPetHelper.Modules.Station.UI.Habitat
{
    [DefaultExecutionOrder(100)]
    public sealed class StationRoomView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private StationRoomObstacle[] _obstacles;
        [SerializeField] private RectTransform _bed;
        [SerializeField] private StationResidentView[] _residents;
        [SerializeField] private float _bedDepth = .255f;
        [SerializeField] private Vector2 _min = new Vector2(.09f, .08f);
        [SerializeField] private Vector2 _max = new Vector2(.91f, .54f);
        [SerializeField] private Vector2 _start = new Vector2(.5f, .15f);
        [SerializeField] private Vector2 _rest = new Vector2(.26f, .16f);
        public RectTransform Content => _content;
        private StationResidentView[] _ordered;

        private void Awake() => _ordered = (StationResidentView[])_residents.Clone();

        private void LateUpdate()
        {
            // Four saved actors only; stable ordering changes the canvas only when feet cross depth bands.
            int first = _bed.GetSiblingIndex();
            for (int i = 0; i < _ordered.Length; i++)
            {
                var resident = _ordered[i];
                first = Mathf.Min(first, resident.transform.GetSiblingIndex());
                int previous = i - 1;
                while (previous >= 0 && _ordered[previous].GroundY < resident.GroundY)
                {
                    _ordered[previous + 1] = _ordered[previous];
                    previous--;
                }
                _ordered[previous + 1] = resident;
            }
            bool bedPlaced = false;
            foreach (var resident in _ordered)
            {
                if (!bedPlaced && resident.GroundY < _bedDepth)
                {
                    SetOrder(_bed, first++);
                    bedPlaced = true;
                }
                SetOrder(resident.transform, first++);
            }
            if (!bedPlaced) SetOrder(_bed, first);
        }

        private static void SetOrder(Transform target, int index)
        { if (target.GetSiblingIndex() != index) target.SetSiblingIndex(index); }

        public StationHabitatArea CreateArea()
        {
            var centers = new HabitatPoint[_obstacles.Length];
            var radii = new HabitatPoint[_obstacles.Length];
            for (int i = 0; i < centers.Length; i++)
            {
                centers[i] = _obstacles[i].Center;
                radii[i] = _obstacles[i].Radius;
            }
            return new StationHabitatArea(Point(_min), Point(_max), Point(_start), Point(_rest), centers, radii);
        }
        private static HabitatPoint Point(Vector2 value) => new HabitatPoint(value.x, value.y);
    }
}
