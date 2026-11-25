namespace HomelessPetHelper.Modules.WorldTime
{
    public sealed class WorldTimeSnapshot
    {
        public WorldTimeVersion Version { get; }

        public long ElapsedMinutes => Version.ElapsedMinutes;

        public int MinutesPerDay { get; }

        public long DayIndex => ElapsedMinutes / MinutesPerDay;

        public int MinuteOfDay => (int)(ElapsedMinutes % MinutesPerDay);

        /// <summary>In-game calendar date derived from persisted minutes; no separate save field.</summary>
        public VirtualDate Date => VirtualCalendar.ToDate(ElapsedMinutes, MinutesPerDay);

        internal WorldTimeSnapshot(WorldTimeVersion version, int minutesPerDay)
        {
            Version = version;
            MinutesPerDay = minutesPerDay;
        }
    }
}
