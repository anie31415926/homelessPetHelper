namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Signals accepted by the single request transition entry point.
    /// ReleaseAfterReturn requires the caller to validate that the active outing really ended;
    /// it is not a pause signal. External rescue is possible only while a request is available.
    /// </summary>
    public enum RequestSignal
    {
        Accept = 0,
        Resolve = 1,
        ReleaseAfterReturn = 2,
        HelpedElsewhere = 3
    }
}
