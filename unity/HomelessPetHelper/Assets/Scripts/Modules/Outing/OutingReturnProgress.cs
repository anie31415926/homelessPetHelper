namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>Stages actually reached, not rescue results or elapsed real time.</summary>
    public enum OutingReturnProgress
    {
        BeforeDeparture = 0,
        Exploring = 1,
        OpeningRoute = 2,
        Negotiating = 3,
        HandingOver = 4
    }
}
