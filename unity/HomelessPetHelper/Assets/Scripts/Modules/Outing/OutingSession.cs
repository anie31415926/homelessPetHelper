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

        public MapDeparturePlan DeparturePlan { get; private set; }

        public OutingReturnProgress Progress { get; private set; }

        public bool HasOpenedRoute { get; private set; }

        public bool HasDeparted => SelectedPointId != null;

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
            if (!CanDepart(sessionId, pointId, requestId, out failure))
            {
                return false;
            }

            SelectedPointId = pointId;
            AcceptedRequestId = requestId;
            Progress = OutingReturnProgress.Exploring;
            Stage = OutingStage.Departed;
            return true;
        }

        public bool CanDepart(Guid sessionId, string pointId, Guid requestId, out OutingFailure failure)
        {
            return CanDepartCore(sessionId, pointId, requestId, true, out failure);
        }

        private bool CanDepartCore(Guid sessionId, string pointId, Guid requestId,
            bool requiresRequest, out OutingFailure failure)
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

            if (requiresRequest && requestId == Guid.Empty)
            {
                failure = OutingFailure.InvalidRequest;
                return false;
            }

            return true;
        }

        /// <summary>Commits a revalidated plan, including request-free exploration.</summary>
        public bool TryDepart(Guid sessionId, MapDeparturePlan plan, out OutingFailure failure)
        {
            if (!CanDepart(sessionId, plan, out failure)) return false;
            DeparturePlan = plan;
            SelectedPointId = plan.PointId;
            AcceptedRequestId = plan.RequestId;
            Progress = OutingReturnProgress.Exploring;
            Stage = OutingStage.Departed;
            return true;
        }

        public bool CanDepart(Guid sessionId, MapDeparturePlan plan, out OutingFailure failure)
        {
            if (plan == null)
            {
                failure = OutingFailure.InvalidDefinition;
                return false;
            }

            return CanDepartCore(sessionId, plan.PointId, plan.RequestId,
                plan.EntryKind != MapEntryKind.Exploration, out failure);
        }

        /// <summary>Caller supplies a validated stage entry, never a UI click or wall-time tick.</summary>
        public bool TryRecordProgress(Guid sessionId, OutingReturnProgress progress, out OutingFailure failure)
        {
            if (!CanRecordProgress(sessionId, progress, out failure)) return false;
            Progress = progress;
            if (progress == OutingReturnProgress.OpeningRoute) HasOpenedRoute = true;
            return true;
        }

        public bool CanRecordProgress(Guid sessionId, OutingReturnProgress progress, out OutingFailure failure)
        {
            if (!CanSettle(sessionId, out failure)) return false;
            if (progress <= Progress || progress > OutingReturnProgress.HandingOver)
            {
                failure = OutingFailure.InvalidProgress;
                return false;
            }

            return true;
        }

        /// <summary>Ends a validated unresolved return or a free map cancellation once.</summary>
        public bool TryEndUnresolved(Guid sessionId, out OutingFailure failure)
        {
            if (!CanEndUnresolved(sessionId, out failure)) return false;
            Stage = OutingStage.Settled;
            return true;
        }

        public bool CanEndUnresolved(Guid sessionId, out OutingFailure failure)
        {
            failure = sessionId != SessionId ? OutingFailure.WrongSession :
                IsSuspended ? OutingFailure.Suspended :
                IsSettled ? OutingFailure.AlreadySettled : OutingFailure.None;
            return failure == OutingFailure.None;
        }

        /// <summary>
        /// Completes this outing once. Repeated settlement is rejected so time is committed once.
        /// </summary>
        public bool TrySettle(Guid sessionId, out OutingFailure failure)
        {
            if (!CanSettle(sessionId, out failure))
            {
                return false;
            }

            Stage = OutingStage.Settled;
            return true;
        }

        public bool CanSettle(Guid sessionId, out OutingFailure failure)
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

            if (IsSuspended)
            {
                failure = OutingFailure.Suspended;
                return false;
            }

            if (Stage != OutingStage.Departed)
            {
                failure = OutingFailure.NotDeparted;
                return false;
            }

            return true;
        }
    }
}
