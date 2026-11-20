using System;

namespace HomelessPetHelper.Modules.Rescue
{
    /// <summary>A live route objective owned by the outing, not a UI-supplied success flag.</summary>
    public interface IRouteOpeningActivity : IDisposable
    {
        Guid SessionId { get; }
        bool IsComplete { get; }
        bool IsSuspended { get; }
        bool IsClosed { get; }
        void SetSuspended(bool suspended);
    }
}
