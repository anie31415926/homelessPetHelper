namespace HomelessPetHelper.Modules.WorldTime
{
    public sealed class WorldTimeChange
    {
        public WorldTimeSnapshot Before { get; }

        public WorldTimeSnapshot After { get; }

        public long AdvancedMinutes => After.ElapsedMinutes - Before.ElapsedMinutes;

        internal WorldTimeChange(WorldTimeSnapshot before, WorldTimeSnapshot after)
        {
            Before = before;
            After = after;
        }
    }
}
