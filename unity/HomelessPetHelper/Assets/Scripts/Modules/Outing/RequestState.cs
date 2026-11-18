namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// A request may be completed by the player or helped elsewhere. Neither terminal state
    /// generates a station intake or rewards here. Returned unresolved outings become available.
    /// </summary>
    public enum RequestState
    {
        Available = 0,
        Accepted = 1,
        Resolved = 2,
        HelpedElsewhere = 3
    }
}
