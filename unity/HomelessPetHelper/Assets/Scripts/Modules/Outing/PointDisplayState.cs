namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Display status of a map point in a read-only snapshot. Visual markers (help badge,
    /// colour, icon) are a presentation concern; these values are the rule-side facts the UI reads.
    /// </summary>
    public enum PointDisplayState
    {
        Locked = 0,
        HasHelpRequest = 1,
        NoRequest = 2
    }
}
