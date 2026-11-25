using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable planning result, not a reservation. The caller must revalidate capacity and
    /// request availability when committing departure. An exploration plan promises no animal.
    /// </summary>
    public sealed class MapDeparturePlan
    {
        public string PointId { get; }
        public Guid RequestId { get; }
        public MapEntryKind EntryKind { get; }
        public OwnerReunionPlan ReunionPlan { get; }
        public bool RequiresStationSpace { get; }

        internal MapDeparturePlan(string pointId, HelpRequest request, bool visitOnly = false)
        {
            PointId = pointId;
            RequestId = request == null ? Guid.Empty : request.RequestId;
            ReunionPlan = request == null ? null : request.ReunionPlan;
            EntryKind = request == null ? MapEntryKind.Exploration :
                ReunionPlan == null ? MapEntryKind.HelpRequest : MapEntryKind.OwnerReunion;
            RequiresStationSpace = EntryKind == MapEntryKind.HelpRequest ||
                (EntryKind == MapEntryKind.Exploration && !visitOnly);
        }
    }
}
