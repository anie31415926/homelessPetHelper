using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable read-only view of the city map for presentation. Built from the current
    /// definition, unlock state and request pool; never changes after construction.
    /// UI reads this snapshot and submits strong-typed intents; it never writes rule fields.
    /// </summary>
    public sealed class MapViewSnapshot
    {
        public string BackgroundId { get; }

        public IReadOnlyList<string> RegionLabels { get; }

        public IReadOnlyList<MapPointView> Points { get; }

        public MapViewSnapshot(string backgroundId, IReadOnlyList<string> regionLabels,
            IReadOnlyList<MapPointView> points)
        {
            BackgroundId = backgroundId;
            RegionLabels = new ReadOnlyCollection<string>(new List<string>(regionLabels));
            Points = new ReadOnlyCollection<MapPointView>(new List<MapPointView>(points));
        }
    }
}
