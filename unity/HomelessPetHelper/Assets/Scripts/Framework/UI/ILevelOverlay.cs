namespace HomelessPetHelper.Framework.UI
{
    /// <summary>Explicit UI composition boundary. Implementations own the popup chain, not gameplay rules.</summary>
    public interface ILevelOverlay
    {
        string PauseCaption { get; }
        string ObjectivesCaption { get; }
        void OpenPause();
        void OpenObjectives();
        void RequestReturn();
        void SetApplicationFocus(bool focused);
        void SetApplicationPaused(bool paused);
    }
}
