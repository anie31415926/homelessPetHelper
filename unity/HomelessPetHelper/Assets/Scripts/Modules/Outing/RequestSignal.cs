namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Signals accepted by the single request transition entry point.
    /// Defer / reactivate / expire / cancel are unconfirmed (B group) and intentionally omitted.
    /// </summary>
    public enum RequestSignal
    {
        Accept = 0,
        Resolve = 1
    }
}
