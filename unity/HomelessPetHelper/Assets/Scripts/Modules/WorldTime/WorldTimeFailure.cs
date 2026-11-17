namespace HomelessPetHelper.Modules.WorldTime
{
    public enum WorldTimeFailure
    {
        None = 0,
        Closed = 1,
        Suspended = 2,
        WrongSession = 3,
        StaleVersion = 4,
        InvalidDuration = 5,
        TimeOverflow = 6
    }
}
