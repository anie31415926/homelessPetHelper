using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// One outing's session state, fixed at construction and never reset by ordinary operations.
    /// Holds identity, seed, chosen point, bound request, phase and the gameplay-time mark.
    /// Does not copy on-site/negotiation rules and never reads a real clock.
    /// </summary>
    public sealed class OutingSession
    {
        public Guid SessionId { get; }

        public int Seed { get; }

        public OutingStage Stage { get; private set; } = OutingStage.MapSelecting;

        public OutingTimeMark TimeMark { get; }

        public bool IsSuspended { get; private set; }

        public string SelectedPointId { get; private set; }

        public Guid AcceptedRequestId { get; private set; }

        public bool HasDeparted => Stage != OutingStage.MapSelecting;

        public bool IsSettled => Stage == OutingStage.Settled;

        public OutingSession(Guid sessionId, int seed, OutingTimeMark timeMark)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty session ID is required.", nameof(sessionId));
            }

            if (seed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seed), "Seed must be nonnegative.");
            }

            if (timeMark.TimeSessionId == Guid.Empty || timeMark.EnteredMinutes < 0)
            {
                throw new ArgumentException("A valid time mark is required.", nameof(timeMark));
            }

            SessionId = sessionId;
            Seed = seed;
            TimeMark = timeMark;
        }

        public void SetSuspended(bool isSuspended)
        {
            IsSuspended = isSuspended;
        }

        /// <summary>
        /// Binds a chosen point and its accepted request, then leaves the map.
        /// The caller must have validated point openness and request availability via the rules.
        /// Rejection changes no state. One session may depart only once.
        /// </summary>
        public bool TryDepart(Guid sessionId, string pointId, Guid requestId, out OutingFailure failure)
        {
            failure = OutingFailure.None;
            if (sessionId != SessionId)
            {
                failure = OutingFailure.WrongSession;
                return false;
            }

            if (IsSuspended)
            {
                failure = OutingFailure.Suspended;
                return false;
            }

            if (Stage != OutingStage.MapSelecting)
            {
                failure = OutingFailure.AlreadyDeparted;
                return false;
            }

            if (string.IsNullOrWhiteSpace(pointId))
            {
                failure = OutingFailure.InvalidPoint;
                return false;
            }

            if (requestId == Guid.Empty)
            {
                failure = OutingFailure.InvalidRequest;
                return false;
            }

            SelectedPointId = pointId;
            AcceptedRequestId = requestId;
            Stage = OutingStage.Departed;
            return true;
        }

        /// <summary>
        /// Completes this outing once. Repeated settlement is rejected so time is committed once.
        /// </summary>
        public bool TrySettle(Guid sessionId, out OutingFailure failure)
        {
            failure = OutingFailure.None;
            if (sessionId != SessionId)
            {
                failure = OutingFailure.WrongSession;
                return false;
            }

            if (Stage == OutingStage.Settled)
            {
                failure = OutingFailure.AlreadySettled;
                return false;
            }

            if (Stage != OutingStage.Departed)
            {
                failure = OutingFailure.NotDeparted;
                return false;
            }

            Stage = OutingStage.Settled;
            return true;
        }
    }
}
