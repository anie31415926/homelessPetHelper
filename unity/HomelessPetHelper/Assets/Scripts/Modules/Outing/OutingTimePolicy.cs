using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Configurable stage-based duration calculation, not a clock commit. Each increment is
    /// charged once in the returned total, independent of clicks, play duration or rescue result.
    /// The assembly layer validates reached stages and commits the total once per outing.
    /// </summary>
    public sealed class OutingTimePolicy
    {
        private readonly long[] _totals;
        private readonly long _openingMinutes;

        public OutingTimePolicy(long explorationMinutes, long openingMinutes,
            long negotiationMinutes, long handoverMinutes)
        {
            if (explorationMinutes <= 0 || openingMinutes <= 0 ||
                negotiationMinutes <= 0 || handoverMinutes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(explorationMinutes),
                    "Every stage increment must be positive gameplay minutes.");
            }

            _totals = new long[5];
            _openingMinutes = openingMinutes;
            checked
            {
                _totals[1] = explorationMinutes;
                _totals[2] = _totals[1] + openingMinutes;
                _totals[3] = _totals[2] + negotiationMinutes;
                _totals[4] = _totals[3] + handoverMinutes;
            }
        }

        public bool TryGetDuration(OutingReturnProgress progress, out long durationMinutes,
            out OutingFailure failure)
        {
            durationMinutes = 0;
            failure = OutingFailure.None;
            if (progress < OutingReturnProgress.BeforeDeparture || progress > OutingReturnProgress.HandingOver)
            {
                failure = OutingFailure.InvalidProgress;
                return false;
            }

            durationMinutes = _totals[(int)progress];
            return true;
        }

        /// <summary>A route-free approach must not charge for an opening activity that never happened.</summary>
        public bool TryGetDuration(OutingReturnProgress progress, bool hasOpenedRoute,
            out long durationMinutes, out OutingFailure failure)
        {
            if (!TryGetDuration(progress, out durationMinutes, out failure)) return false;
            if (!hasOpenedRoute && progress == OutingReturnProgress.OpeningRoute)
            {
                durationMinutes = 0;
                failure = OutingFailure.InvalidProgress;
                return false;
            }
            if (!hasOpenedRoute && progress >= OutingReturnProgress.Negotiating)
                durationMinutes -= _openingMinutes;
            return true;
        }
    }
}
