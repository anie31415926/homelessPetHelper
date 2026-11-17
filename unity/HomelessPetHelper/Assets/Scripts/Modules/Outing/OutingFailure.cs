namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Rejection reasons for outing rules. Separates business refusals from invalid
    /// input so callers can react without guessing. No value implies a penalty or
    /// default consequence for unconfirmed branches (defer/expire/full capacity).
    /// </summary>
    public enum OutingFailure
    {
        None = 0,
        WrongSession = 1,
        InvalidSession = 2,
        Suspended = 3,
        InvalidPoint = 4,
        PointLocked = 5,
        PointHasNoRequest = 6,
        InvalidRequest = 7,
        RequestNotAvailable = 8,
        InvalidSignal = 9,
        UnexpectedSignal = 10,
        WrongStage = 11,
        AlreadyDeparted = 12,
        AlreadySettled = 13,
        NotDeparted = 14,
        InvalidSeed = 15,
        InvalidDefinition = 16,
        DuplicatePointId = 17,
        DuplicateRequestId = 18,
        CapacityNotAvailable = 19
    }
}
