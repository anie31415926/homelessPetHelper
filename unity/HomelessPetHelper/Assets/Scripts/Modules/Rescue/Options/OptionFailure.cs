namespace HomelessPetHelper.Modules.Rescue.Options
{
    public enum OptionFailure
    {
        None = 0,
        WrongSession = 1,
        WrongTurn = 2,
        Suspended = 3,
        UnexpectedPhase = 4,
        InvalidOffer = 5,
        NoStabilizingChoice = 6,
        InvalidSelection = 7,
        UseLimitExceeded = 8,
        UnknownOption = 9,
        DuplicateSelection = 10,
        LockLimitReached = 11,
        Closed = 12,
        CapacityExhausted = 13
    }
}
