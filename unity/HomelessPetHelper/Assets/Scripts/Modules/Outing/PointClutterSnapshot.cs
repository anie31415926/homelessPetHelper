namespace HomelessPetHelper.Modules.Outing
{
    public sealed class PointClutterSnapshot
    {
        public string PointId { get; }
        public ClutterLevel Level { get; }
        public long ElapsedMinutes { get; }
        public long? ClearSinceMinutes { get; }
        public bool IsSuspended { get; }

        internal PointClutterSnapshot(string pointId, ClutterLevel level, long elapsedMinutes,
            long? clearSinceMinutes, bool isSuspended)
        {
            PointId = pointId;
            Level = level;
            ElapsedMinutes = elapsedMinutes;
            ClearSinceMinutes = clearSinceMinutes;
            IsSuspended = isSuspended;
        }
    }
}
