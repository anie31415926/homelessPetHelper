namespace HomelessPetHelper.Framework.Persistence
{
    public enum SaveErrorCode
    {
        NotOpen = 1,
        Corrupt = 2,
        FutureVersion = 3,
        InvalidSection = 4,
        MissingMigration = 5,
        StorageReadFailed = 6,
        WriteUncertain = 7,
        Conflict = 8,
        Busy = 9,
        RevisionOverflow = 10
    }
}
