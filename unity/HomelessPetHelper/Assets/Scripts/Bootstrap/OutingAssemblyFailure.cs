namespace HomelessPetHelper.Bootstrap
{
    /// <summary>
    /// Rejection reasons from the assembly layer that drives one outing end to end.
    /// Values identify which gate refused; they do not imply penalties or default
    /// consequences for unconfirmed branches (defer / expire / full capacity).
    /// </summary>
    public enum OutingAssemblyFailure
    {
        None = 0,
        NoActiveSession = 1,
        WrongSession = 2,
        SessionAlreadySettled = 3,
        AlreadyDeparted = 4,
        NotDeparted = 5,
        Suspended = 6,
        InvalidSeed = 7,
        InvalidPoint = 8,
        PointLocked = 9,
        PointHasNoRequest = 10,
        RequestNotAvailable = 11,
        InvalidFieldSignal = 12,
        WrongStage = 13,
        StationFull = 14,
        StationRejected = 15,
        InvalidDuration = 16,
        TimeVersionStale = 17,
        TimeRejected = 18,
        InvalidArrival = 19,
        IdentityMismatch = 20,
        InternalInconsistent = 21,
        SessionIdReused = 22,
        BasicAssessmentRequired = 23,
        NecessaryCarePending = 24,
        RescueNotComplete = 25,
        DirectHandoffRequired = 26,
        DurationPolicyRequired = 27,
        RouteNotComplete = 28
    }
}
