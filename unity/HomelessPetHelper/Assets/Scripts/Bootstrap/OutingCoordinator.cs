using System;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.WorldTime;

namespace HomelessPetHelper.Bootstrap
{
    /// <summary>
    /// Assembly layer that drives one outing end to end: leave station -> map select ->
    /// on-site signals -> return and settle. Owns the single active OutingSession and
    /// decides phase order; delegates all rules to the modules and never re-implements them.
    ///
    /// Every operation is split into a read-only validation part (which may reject without
    /// mutation) and a commit part whose preconditions are already guaranteed. The on-site
    /// phase is not generated here: callers inject already-validated field signals, so no
    /// success is fabricated. Unconfirmed branches (defer / expire / cancel / full-capacity
    /// consequences) carry no default behaviour.
    ///
    /// Pure C#, no Unity dependency; use on one gameplay thread.
    /// </summary>
    public sealed class OutingCoordinator
    {
        private readonly RescueFlow _flow;
        private readonly RescueStation _station;
        private readonly Guid _stationSessionId;
        private readonly ActionWorldClock _clock;
        private readonly Guid _worldSessionId;
        private readonly CityMapRules _mapRules;
        private readonly RequestPoolState _requestPool;

        private OutingSession _session;
        private OutingSummary _lastSummary;

        public OutingSession Session => _session;
        public OutingSummary LastSummary => _lastSummary;

        public OutingCoordinator(RescueFlow flow, RescueStation station, Guid stationSessionId,
            ActionWorldClock clock, Guid worldSessionId, CityMapRules mapRules,
            RequestPoolState requestPool)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _station = station ?? throw new ArgumentNullException(nameof(station));
            if (stationSessionId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty station session ID is required.", nameof(stationSessionId));
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (worldSessionId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty world session ID is required.", nameof(worldSessionId));
            }

            _mapRules = mapRules ?? throw new ArgumentNullException(nameof(mapRules));
            _requestPool = requestPool ?? throw new ArgumentNullException(nameof(requestPool));
            _stationSessionId = stationSessionId;
            _worldSessionId = worldSessionId;
        }

        /// <summary>
        /// Starts one outing: capacity pre-check (G-section reminder), then fixes the session,
        /// captures the gameplay-time version and moves the flow into the map phase.
        /// A full station is refused with no default consequence; the caller decides what to do.
        /// </summary>
        public bool TryBeginOuting(Guid sessionId, int seed, out OutingAssemblyFailure failure)
        {
            failure = OutingAssemblyFailure.None;
            if (sessionId == Guid.Empty)
            {
                failure = OutingAssemblyFailure.WrongSession;
                return false;
            }

            if (_session != null && !_session.IsSettled)
            {
                failure = OutingAssemblyFailure.AlreadyDeparted;
                return false;
            }

            if (seed < 0)
            {
                failure = OutingAssemblyFailure.InvalidSeed;
                return false;
            }

            if (_flow.Stage != RescueStage.Station || _flow.IsSuspended)
            {
                failure = _flow.IsSuspended ? OutingAssemblyFailure.Suspended : OutingAssemblyFailure.WrongStage;
                return false;
            }

            // G-section: full station blocks departure. Consequence is the caller's decision.
            if (_station.IsFull || _station.IsSuspended || _station.IsClosed)
            {
                failure = _station.IsFull ? OutingAssemblyFailure.StationFull : OutingAssemblyFailure.StationRejected;
                return false;
            }

            if (_clock.IsSuspended || _clock.IsClosed)
            {
                failure = OutingAssemblyFailure.TimeRejected;
                return false;
            }

            // Commit: all preconditions above are guaranteed.
            var mark = new OutingTimeMark(_worldSessionId, _clock.Current.Version.ElapsedMinutes);
            var session = new OutingSession(sessionId, seed, mark);
            if (!_flow.TryAdvance(RescueSignal.LeaveStation, out var flowFailure))
            {
                failure = MapFlowFailure(flowFailure);
                return false;
            }

            _session = session;
            _lastSummary = null;
            return true;
        }

        /// <summary>
        /// Selects a point, accepts its available request, binds both to the session and
        /// moves the flow to the on-site phase. Point behaviour for no-request points is a
        /// B-group decision; this layer only reports the fact.
        /// </summary>
        public bool TrySelectAndDepart(Guid sessionId, string pointId, out OutingAssemblyFailure failure)
        {
            failure = OutingAssemblyFailure.None;
            if (!TryRequireActiveSession(sessionId, out failure))
            {
                return false;
            }

            if (_session.Stage != OutingStage.MapSelecting || _session.IsSuspended ||
                _flow.Stage != RescueStage.Map || _flow.IsSuspended)
            {
                failure = (_session.IsSuspended || _flow.IsSuspended)
                    ? OutingAssemblyFailure.Suspended
                    : OutingAssemblyFailure.WrongStage;
                return false;
            }

            // Read-only gate first: no mutation on refusal.
            if (!_mapRules.TrySelectPoint(pointId, out var request, out var selectFailure))
            {
                failure = MapOutingFailure(selectFailure);
                return false;
            }

            // Commit: point open + request available + correct stages already verified.
            if (!_requestPool.TryTransition(request.RequestId, RequestSignal.Accept, out var acceptFailure))
            {
                failure = MapOutingFailure(acceptFailure);
                return false;
            }

            if (!_session.TryDepart(sessionId, pointId, request.RequestId, out var departFailure))
            {
                failure = MapOutingFailure(departFailure);
                return false;
            }

            if (!_flow.TryAdvance(RescueSignal.Depart, out var flowFailure))
            {
                failure = MapFlowFailure(flowFailure);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Injects an already-validated on-site completion signal. Only the two field
        /// transitions are accepted and only in their expected phase; the assembly layer
        /// never manufactures rescue success itself. Both open-route variants converge here.
        /// </summary>
        public bool TryAdvanceField(Guid sessionId, RescueSignal signal, out OutingAssemblyFailure failure)
        {
            failure = OutingAssemblyFailure.None;
            if (!TryRequireActiveSession(sessionId, out failure))
            {
                return false;
            }

            var expectedStage = signal == RescueSignal.ApproachCompleted
                ? RescueStage.Approach
                : signal == RescueSignal.NegotiationCompleted
                    ? RescueStage.Negotiation
                    : RescueStage.Station;

            if (expectedStage == RescueStage.Station)
            {
                failure = OutingAssemblyFailure.InvalidFieldSignal;
                return false;
            }

            if (!_session.HasDeparted || _session.IsSuspended || _flow.IsSuspended)
            {
                failure = _session.IsSuspended || _flow.IsSuspended
                    ? OutingAssemblyFailure.Suspended
                    : OutingAssemblyFailure.NotDeparted;
                return false;
            }

            if (_flow.Stage != expectedStage)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }

            if (_flow.TryAdvance(signal, out var flowFailure))
            {
                failure = OutingAssemblyFailure.None;
                return true;
            }

            failure = MapFlowFailure(flowFailure);
            return false;
        }

        /// <summary>
        /// Returns to station and settles once: receives the rescued animal, resolves the
        /// request, seals the session and commits gameplay time against the version captured
        /// at departure. Re-settlement is refused before any mutation, so time advances once.
        /// The arrival identity must match this outing; no animal is created by the map layer.
        /// </summary>
        public bool TryReturnAndSettle(Guid sessionId, StationAnimalIntake arrival,
            long durationMinutes, out OutingSummary summary, out OutingAssemblyFailure failure)
        {
            summary = null;
            failure = OutingAssemblyFailure.None;
            if (!TryRequireActiveSession(sessionId, out failure))
            {
                return false;
            }

            if (_session.Stage != OutingStage.Departed || _session.IsSuspended ||
                _flow.IsSuspended)
            {
                failure = _session.IsSuspended || _flow.IsSuspended
                    ? OutingAssemblyFailure.Suspended
                    : OutingAssemblyFailure.WrongStage;
                return false;
            }

            if (_flow.Stage != RescueStage.Rescued)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }

            if (arrival == null)
            {
                failure = OutingAssemblyFailure.InvalidArrival;
                return false;
            }

            if (arrival.OutingId != sessionId)
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }

            if (durationMinutes <= 0)
            {
                failure = OutingAssemblyFailure.InvalidDuration;
                return false;
            }

            // Pre-validate every commit-side module so no step can fail midway.
            if (_station.IsSuspended || _station.IsClosed)
            {
                failure = OutingAssemblyFailure.StationRejected;
                return false;
            }

            if (_station.IsFull)
            {
                failure = OutingAssemblyFailure.StationFull;
                return false;
            }

            if (_clock.IsSuspended || _clock.IsClosed)
            {
                failure = OutingAssemblyFailure.TimeRejected;
                return false;
            }

            var expectedVersion = new WorldTimeVersion(_worldSessionId, _session.TimeMark.EnteredMinutes);
            if (expectedVersion.SessionId != _worldSessionId ||
                expectedVersion.ElapsedMinutes != _clock.Current.Version.ElapsedMinutes)
            {
                failure = OutingAssemblyFailure.TimeVersionStale;
                return false;
            }

            // Commit: session seal is the first irreversible gate, then downstream modules.
            if (!_session.TrySettle(sessionId, out var settleFailure))
            {
                failure = MapOutingFailure(settleFailure);
                return false;
            }

            if (!_requestPool.TryTransition(_session.AcceptedRequestId, RequestSignal.Resolve, out var resolveFailure))
            {
                failure = MapOutingFailure(resolveFailure);
                return false;
            }

            if (!_station.TryReceive(_stationSessionId, arrival, out _, out var stationFailure))
            {
                failure = MapStationFailure(stationFailure);
                return false;
            }

            if (!_clock.TryAdvance(expectedVersion, durationMinutes, out _, out var timeFailure))
            {
                failure = MapTimeFailure(timeFailure);
                return false;
            }

            if (!_flow.TryAdvance(RescueSignal.ReturnToStation, out var returnFailure) ||
                !_flow.TryAdvance(RescueSignal.CareCompleted, out var careFailure))
            {
                failure = OutingAssemblyFailure.InternalInconsistent;
                return false;
            }

            summary = new OutingSummary(sessionId, _session.SelectedPointId,
                _session.AcceptedRequestId, "rescued", durationMinutes);
            _lastSummary = summary;
            return true;
        }

        private bool TryRequireActiveSession(Guid sessionId, out OutingAssemblyFailure failure)
        {
            failure = OutingAssemblyFailure.None;
            if (_session == null)
            {
                failure = OutingAssemblyFailure.NoActiveSession;
                return false;
            }

            if (_session.IsSettled)
            {
                failure = OutingAssemblyFailure.SessionAlreadySettled;
                return false;
            }

            if (sessionId != _session.SessionId)
            {
                failure = OutingAssemblyFailure.WrongSession;
                return false;
            }

            return true;
        }

        private static OutingAssemblyFailure MapOutingFailure(OutingFailure failure)
        {
            switch (failure)
            {
                case OutingFailure.WrongSession: return OutingAssemblyFailure.WrongSession;
                case OutingFailure.Suspended: return OutingAssemblyFailure.Suspended;
                case OutingFailure.InvalidPoint: return OutingAssemblyFailure.InvalidPoint;
                case OutingFailure.PointLocked: return OutingAssemblyFailure.PointLocked;
                case OutingFailure.PointHasNoRequest: return OutingAssemblyFailure.PointHasNoRequest;
                case OutingFailure.RequestNotAvailable: return OutingAssemblyFailure.RequestNotAvailable;
                case OutingFailure.InvalidRequest: return OutingAssemblyFailure.RequestNotAvailable;
                case OutingFailure.AlreadyDeparted: return OutingAssemblyFailure.AlreadyDeparted;
                case OutingFailure.AlreadySettled: return OutingAssemblyFailure.SessionAlreadySettled;
                case OutingFailure.NotDeparted: return OutingAssemblyFailure.NotDeparted;
                default: return OutingAssemblyFailure.InternalInconsistent;
            }
        }

        private static OutingAssemblyFailure MapStationFailure(StationFailure failure)
        {
            switch (failure)
            {
                case StationFailure.TemporaryCapacityFull: return OutingAssemblyFailure.StationFull;
                case StationFailure.DuplicateOuting: return OutingAssemblyFailure.SessionAlreadySettled;
                case StationFailure.InvalidArrival: return OutingAssemblyFailure.InvalidArrival;
                case StationFailure.WrongSession: return OutingAssemblyFailure.WrongSession;
                default: return OutingAssemblyFailure.StationRejected;
            }
        }

        private static OutingAssemblyFailure MapTimeFailure(WorldTimeFailure failure)
        {
            switch (failure)
            {
                case WorldTimeFailure.StaleVersion: return OutingAssemblyFailure.TimeVersionStale;
                case WorldTimeFailure.WrongSession: return OutingAssemblyFailure.WrongSession;
                default: return OutingAssemblyFailure.TimeRejected;
            }
        }

        private static OutingAssemblyFailure MapFlowFailure(TransitionFailure failure)
        {
            switch (failure)
            {
                case TransitionFailure.Suspended: return OutingAssemblyFailure.Suspended;
                case TransitionFailure.UnexpectedSignal: return OutingAssemblyFailure.WrongStage;
                default: return OutingAssemblyFailure.InternalInconsistent;
            }
        }
    }
}
