namespace HomelessPetHelper.Modules.Approach
{
    public readonly struct ApproachDebris
    {
        public int Cell { get; }
        public ApproachTool Tool { get; }
        public double WorkSeconds { get; }
        public ApproachDebris(int cell, ApproachTool tool, double workSeconds)
        {
            Cell = cell;
            Tool = tool;
            WorkSeconds = workSeconds;
        }
    }
}
