using System;

namespace HomelessPetHelper.Game.Loading
{
    public sealed class DemoLoadingTask : ILoadingTask
    {
        private readonly double _seconds;
        private double _elapsed;
        private bool _disposed;

        public DemoLoadingTask(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            _seconds = seconds;
        }

        public float Progress => _seconds == 0d ? 1f : (float)(_elapsed / _seconds);
        public bool IsComplete => _elapsed >= _seconds;

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DemoLoadingTask));
            if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds) ||
                unscaledDeltaSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            _elapsed = Math.Min(_seconds, _elapsed + unscaledDeltaSeconds);
        }

        public void Dispose() => _disposed = true;
    }
}
