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
        CapacityNotIncreased = 9
    }
}
