using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Persistent in-memory clutter for one location. Only explicit gameplay-time commits
    /// restore a cleared location to Light; opening views never does. This is not animal dirt
    /// and does not decide route completion, obstacle layout, animal identity or rescue success.
    /// </summary>
    public sealed class PointClutterState
    {
        private readonly long _regrowthDelayMinutes;
        private long? _clearSinceMinutes;

        public string PointId { get; }
        public ClutterLevel Level { get; private set; }
        public long ElapsedMinutes { get; private set; }
        public bool IsSuspended { get; private set; }

        public PointClutterState(string pointId, ClutterLevel level, long elapsedMinutes,
            long regrowthDelayMinutes)
        {
            if (string.IsNullOrWhiteSpace(pointId) || pointId != pointId.Trim())
            {
                throw new ArgumentException("A trimmed point ID is required.", nameof(pointId));
            }

            if (!IsValidLevel(level))
            {
                throw new ArgumentOutOfRangeException(nameof(level));
            }

            if (elapsedMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMinutes));
            }

            if (regrowthDelayMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(regrowthDelayMinutes));
            }

            PointId = pointId;
            Level = level;
            ElapsedMinutes = elapsedMinutes;
            _regrowthDelayMinutes = regrowthDelayMinutes;
            _clearSinceMinutes = level == ClutterLevel.Clear ? elapsedMinutes : (long?)null;
        }

        public void SetSuspended(bool isSuspended)
        {
            IsSuspended = isSuspended;
        }

        public PointClutterSnapshot Capture()
        {
            return new PointClutterSnapshot(PointId, Level, ElapsedMinutes, _clearSinceMinutes, IsSuspended);
        }

        /// <summary>
        /// Records an externally validated reduction at the current gameplay time. How partial
        /// obstacle removal maps to levels is a field rule, not guessed here. Clear is optional.
        /// Duplicate/non-reducing input is refused and cannot reset the regrowth timer.
        /// </summary>
        public bool TryReduceTo(ClutterLevel remainingLevel, out OutingFailure failure)
        {
            failure = OutingFailure.None;
            if (IsSuspended)
            {
                failure = OutingFailure.Suspended;
                return false;
            }

            if (!IsValidLevel(remainingLevel))
            {
                failure = OutingFailure.InvalidClutterLevel;
                return false;
            }

            if (remainingLevel >= Level)
            {
                failure = OutingFailure.ClutterNotReduced;
                return false;
            }

            Level = remainingLevel;
            _clearSinceMinutes = remainingLevel == ClutterLevel.Clear ? ElapsedMinutes : (long?)null;
            return true;
        }

        /// <summary>
        /// Receives monotonically increasing committed world minutes from one world clock.
        /// Replaying the same timestamp is harmless; stale/negative time is refused atomically.
        /// No automatic Light -> Moderate -> Heavy escalation has been approved.
        /// </summary>
        public bool TryAdvanceTo(long elapsedMinutes, out OutingFailure failure)
        {
            failure = OutingFailure.None;
            if (IsSuspended)
            {
                failure = OutingFailure.Suspended;
                return false;
            }

            if (elapsedMinutes < ElapsedMinutes)
            {
                failure = OutingFailure.InvalidTime;
                return false;
            }

            if (_clearSinceMinutes.HasValue &&
                elapsedMinutes - _clearSinceMinutes.Value >= _regrowthDelayMinutes)
            {
                Level = ClutterLevel.Light;
                _clearSinceMinutes = null;
            }

            ElapsedMinutes = elapsedMinutes;
            return true;
        }

        private static bool IsValidLevel(ClutterLevel level)
        {
            return level >= ClutterLevel.Clear && level <= ClutterLevel.Heavy;
        }
    }
}
