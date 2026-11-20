namespace HomelessPetHelper.Framework.Audio
{
    public enum AudioPlayResult
    {
        Played = 0,
        AlreadyPlaying = 1,
        UnknownCue = 2,
        WrongChannel = 3,
        MissingClip = 4,
        ClipNotReady = 5,
        Suspended = 6,
        Muted = 7,
        Cooldown = 8,
        VoiceLimit = 9,
        Disposed = 10,
        NotBound = 11
    }
}
