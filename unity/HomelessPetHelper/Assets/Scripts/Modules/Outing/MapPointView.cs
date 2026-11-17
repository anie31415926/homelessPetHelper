namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable per-point display item. Holds only rule-side facts: effective openness,
    /// whether a help request is attached, and text keys for presentation.
    /// UI decides the actual badge/colour/icon; no visual state is baked in.
    /// </summary>
    public sealed class MapPointView
    {
        public string PointId { get; }

        public string DisplayNameKey { get; }

        public string RegionId { get; }

        public PointDisplayState DisplayState { get; }

        public string RequestTextKey { get; }

        public MapPointView(string pointId, string displayNameKey, string regionId,
            PointDisplayState displayState, string requestTextKey)
        {
            PointId = pointId;
            DisplayNameKey = displayNameKey;
            RegionId = regionId;
            DisplayState = displayState;
            RequestTextKey = requestTextKey;
        }
    }
}
