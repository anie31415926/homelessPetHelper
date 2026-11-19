namespace HomelessPetHelper.Modules.Station.Cleaning
{
    public enum StationCoatCareFailure
    {
        None = 0,
        Suspended = 1,
        SessionUnavailable = 2,
        AnimalUnavailable = 3,
        CoatChanged = 4,
        AssessmentRequired = 5,
        HealthCareRequired = 6,
        AnimalNeedsSpace = 7,
        WashNotAccepted = 8,
        GroomNotAccepted = 9,
        AlreadyActive = 10,
        NotActive = 11,
        TimeVersionStale = 12,
        TimeRejected = 13
    }
}
