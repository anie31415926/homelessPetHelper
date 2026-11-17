namespace HomelessPetHelper.Editor.RescueCase
{
    public enum CaseFailure
    {
        None = 0,
        Closed = 1,
        Suspended = 2,
        WrongSession = 3,
        WrongTurn = 4,
        UnexpectedStage = 5,
        InvalidSelection = 6,
        UnknownOption = 7,
        NoFood = 8,
        HandRejected = 9,
        AlreadyInteracted = 10,
        FoodAlreadyPlaced = 11,
        AlreadyFed = 12,
        AlreadyWithinReach = 13
    }
}
