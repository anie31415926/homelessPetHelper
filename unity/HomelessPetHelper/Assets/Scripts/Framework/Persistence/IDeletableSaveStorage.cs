namespace HomelessPetHelper.Framework.Persistence
{
    /// <summary>Optional removal of an individual checkpoint. A missing checkpoint is a no-op; other errors throw.</summary>
    public interface IDeletableSaveStorage : ISaveStorage
    {
        void Delete(int slot);
    }
}
