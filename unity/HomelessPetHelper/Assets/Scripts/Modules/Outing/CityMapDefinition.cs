using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable static definition of a city map. BackgroundId points to a presentation asset.
    /// Points are a flat read-only set; regions live only as grouping labels on each point.
    /// Uniqueness of point IDs is enforced at construction.
    /// </summary>
    public sealed class CityMapDefinition
    {
        public string BackgroundId { get; }

        public IReadOnlyList<string> RegionLabels { get; }

        public IReadOnlyList<MapPointDefinition> Points { get; }

        private readonly Dictionary<string, MapPointDefinition> _pointsById;

        public CityMapDefinition(string backgroundId, IEnumerable<string> regionLabels,
            IEnumerable<MapPointDefinition> points)
        {
            if (string.IsNullOrWhiteSpace(backgroundId) || backgroundId != backgroundId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty background ID is required.", nameof(backgroundId));
            }

            if (regionLabels == null)
            {
                throw new ArgumentNullException(nameof(regionLabels));
            }

            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            var labels = regionLabels.ToArray();
            if (labels.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Region labels must be nonempty.", nameof(regionLabels));
            }

            var pointArray = points.ToArray();
            if (pointArray.Length == 0)
            {
                throw new ArgumentException("At least one map point is required.", nameof(points));
            }

            _pointsById = new Dictionary<string, MapPointDefinition>(pointArray.Length);
            foreach (var point in pointArray)
            {
                if (point == null)
                {
                    throw new ArgumentException("Map points must not be null.", nameof(points));
                }

                if (_pointsById.ContainsKey(point.PointId))
                {
                    throw new ArgumentException("Duplicate point ID: " + point.PointId, nameof(points));
                }

                _pointsById.Add(point.PointId, point);
            }

            BackgroundId = backgroundId;
            RegionLabels = Array.AsReadOnly(labels);
            Points = new ReadOnlyCollection<MapPointDefinition>(pointArray);
        }

        public bool TryGetPoint(string pointId, out MapPointDefinition point)
        {
            if (pointId == null)
            {
                point = null;
                return false;
            }

            return _pointsById.TryGetValue(pointId, out point);
        }
    }
}
