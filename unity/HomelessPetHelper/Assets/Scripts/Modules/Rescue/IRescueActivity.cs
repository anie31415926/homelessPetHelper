using System;

namespace HomelessPetHelper.Modules.Rescue
{
    /// <summary>Replaceable field rules. Turns, cards and scene objects never cross this boundary.</summary>
    public interface IRescueActivity : IRescueAnimalSource, IDisposable
    {
        bool IsAssistanceRequested { get; }
        bool IsSuspended { get; }
        bool IsClosed { get; }
        void SetSuspended(bool suspended);
    }
}
