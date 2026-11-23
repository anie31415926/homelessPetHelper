using System;

namespace HomelessPetHelper.Game.Loading
{
    // The scene owner ticks and disposes this task on Unity's main thread, including on failure/cancel.
    // Progress is normalized; IsComplete must only become true when all preparation has succeeded.
    // Dispose is idempotent. Transfer any resources needed by Main to their next owner before completing.
    public interface ILoadingTask : IDisposable
    {
        float Progress { get; }
        bool IsComplete { get; }
        void Tick(float unscaledDeltaSeconds);
    }
}
