using System;

namespace HomelessPetHelper.Modules.WorldTime
{
    /// <summary>
    /// In-memory game minutes advanced explicitly on the calling gameplay thread. No wall clock, Tick,
    /// automatic catch-up, event chain, health simulation or adoption scheduling is involved.
    /// </summary>
    public sealed class ActionWorldClock
    {
        private readonly Guid _sessionId;
        private readonly int _minutesPerDay;

        public WorldTimeSnapshot Current { get; private set; }

        public bool IsSuspended { get; private set; }

        public bool IsClosed { get; private set; }

        public ActionWorldClock(Guid sessionId, int minutesPerDay, long initialElapsedMinutes = 0)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty world session ID is required.", nameof(sessionId));
            }

            if (minutesPerDay <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minutesPerDay));
            }

            if (initialElapsedMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialElapsedMinutes));
            }

            _sessionId = sessionId;
            _minutesPerDay = minutesPerDay;
            Current = new WorldTimeSnapshot(new WorldTimeVersion(sessionId, initialElapsedMinutes), minutesPerDay);
        }

        public void SetSuspended(bool isSuspended)
        {
            IsSuspended = isSuspended;
        }

        public void Close()
        {
            IsClosed = true;
        }

        /// <summary>
        /// Commits positive game minutes for an already-validated complete action. Retain its expected version
        /// in the action owner; retries must not substitute Current.Version. Free interactions do not call this.
        /// Returns a change for later presentation/notification. Rejection returns null and changes no time.
        /// </summary>
        public bool TryAdvance(WorldTimeVersion expectedVersion, long durationMinutes,
            out WorldTimeChange change, out WorldTimeFailure failure)
        {
            change = null;
            if (!ValidateVersion(expectedVersion, out failure))
            {
                return false;
            }

            if (durationMinutes <= 0)
            {
                failure = WorldTimeFailure.InvalidDuration;
                return false;
            }

            if (durationMinutes > long.MaxValue - Current.ElapsedMinutes)
            {
                failure = WorldTimeFailure.TimeOverflow;
                return false;
            }

            var version = new WorldTimeVersion(_sessionId, Current.ElapsedMinutes + durationMinutes);
            var nextTime = new WorldTimeSnapshot(version, _minutesPerDay);
            change = new WorldTimeChange(Current, nextTime);
            Current = nextTime;
            return true;
        }

        private bool ValidateVersion(WorldTimeVersion expectedVersion, out WorldTimeFailure failure)
        {
            failure = WorldTimeFailure.None;
            if (IsClosed)
            {
                failure = WorldTimeFailure.Closed;
            }
            else if (IsSuspended)
            {
                failure = WorldTimeFailure.Suspended;
            }
            else if (expectedVersion.SessionId != _sessionId)
            {
                failure = WorldTimeFailure.WrongSession;
            }
            else if (expectedVersion.ElapsedMinutes != Current.ElapsedMinutes)
            {
                failure = WorldTimeFailure.StaleVersion;
            }

            return failure == WorldTimeFailure.None;
        }
    }
}
