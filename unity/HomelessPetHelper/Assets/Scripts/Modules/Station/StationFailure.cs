namespace HomelessPetHelper.Modules.Station
{
    public enum StationFailure
    {
        None = 0,
        Closed = 1,
        Suspended = 2,
        WrongSession = 3,
        InvalidArrival = 4,
        DuplicateOuting = 5,
        DuplicateAnimal = 6,
        TemporaryCapacityFull = 7,
        InvalidCapacity = 8,
        CapacityNotIncreased = 9,
        AnimalNotFound = 10,
        BasicAssessmentRequired = 11,
        BasicAssessmentAlreadyCompleted = 12,
        InvalidCareAssessment = 13,
        CareProblemNotFound = 14,
        UnexpectedCareStatus = 15,
        NecessaryCarePending = 16,
        AssessmentSourceUnavailable = 17
    }
}
