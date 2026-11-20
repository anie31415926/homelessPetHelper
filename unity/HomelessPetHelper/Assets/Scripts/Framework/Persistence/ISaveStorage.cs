namespace HomelessPetHelper.Framework.Persistence
{
    /// <summary>
    /// Two independently writable durable slots, owned by one service on the gameplay thread.
    /// Read returns null only for a genuinely missing slot; errors must throw, never return defaults.
    /// Write must finish persistence before returning. Archive must preserve and verify the exact old content.
    /// A failed Write may have changed its slot; the service must reopen before retrying.
    /// </summary>
    public interface ISaveStorage
    {
        string Read(int slot);
        void Write(int slot, string content);
        void Archive(int slot);
    }
}
