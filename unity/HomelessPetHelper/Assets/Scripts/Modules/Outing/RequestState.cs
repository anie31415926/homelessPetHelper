namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Lifecycle of one help request. Only the transitions confirmed so far are exposed:
    /// Available -Accept-> Accepted -Resolve-> Resolved (the single terminal state).
    /// Defer / expire / cancel branches remain unconfirmed and are deliberately absent
    /// until the B group signs them off; no implicit default behaviour is provided.
    /// </summary>
    public enum RequestState
    {
        Available = 0,
        Accepted = 1,
        Resolved = 2
    }
}
