using System;

namespace HomelessPetHelper.Framework.Audio
{
    [Flags]
    public enum AudioPauseReason
    {
        None = 0,
        ApplicationPaused = 1,
        FocusLost = 2,
        OwnerDisabled = 4,
        ExternalInterruption = 8,
        GameplayPaused = 16
    }
}
