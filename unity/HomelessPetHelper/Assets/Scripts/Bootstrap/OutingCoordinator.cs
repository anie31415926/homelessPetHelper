using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Animals;
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
    /// success is fabricated. Explicit unresolved return releases the accepted request, but
    /// unconfirmed event scheduling, owner handoff and rewards carry no default behaviour.
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
        private readonly OutingTimePolicy _timePolicy;
        private readonly HashSet<Guid> _usedSessionIds = new HashSet<Guid>();

        private OutingSession _session;
        private OutingSummary _lastSummary;
        private Guid _returnedAnimalId;
        private IRescueAnimalSource _animalSource;
        private AnimalIndividual _boundIndividual;

        public OutingSession Session => _session;
        public OutingSummary LastSummary => _lastSummary;
        public StationReturnSummary LastStationReturn { get; private set; }

        public OutingCoordinator(RescueFlow flow, RescueStation station, Guid stationSessionId,
            ActionWorldClock clock, Guid worldSessionId, CityMapRules mapRules,
            RequestPoolState requestPool, OutingTimePolicy timePolicy = null)
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
            _timePolicy = timePolicy;
            _stationSessionId = stationSessionId;
            _worldSessionId = worldSessionId;
        }

        /// <summary>
        /// Opens the map without departing or charging time. Capacity is rechecked for the
        /// selected destination at actual departure, so a full station can still view reunions.
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

            if (_usedSessionIds.Contains(sessionId))
            {
                failure = OutingAssemblyFailure.SessionIdReused;
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

            if (_station.IsSuspended || _station.IsClosed)
            {
                failure = OutingAssemblyFailure.StationRejected;
                return false;
            }

            if (_clock.IsSuspended || _clock.IsClosed)
            {
                failure = OutingAssemblyFailure.TimeRejected;
                return false;
            }

            if (_clock.Current.Version.SessionId != _worldSessionId)
            {
                failure = OutingAssemblyFailure.WrongSession;
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
            _usedSessionIds.Add(sessionId);
            _lastSummary = null;
            _returnedAnimalId = Guid.Empty;
            _animalSource = null;
            _boundIndividual = null;
            return true;
        }

        /// <summary>Binds the real rule owner during negotiation, before rescue completion.</summary>
        public bool TryBindRescueAnimal(Guid sessionId, IRescueAnimalSource source,
            out OutingAssemblyFailure failure)
        {
            if (!TryRequireActiveSession(sessionId, out failure))
            {
                return false;
            }

            if (_session.IsSuspended || _flow.IsSuspended)
            {
                failure = OutingAssemblyFailure.Suspended;
                return false;
            }

            if (_flow.Stage != RescueStage.Negotiation || _session.IsSettled)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }

            if (source == null || source.SessionId != sessionId || source.Individual == null ||
                source.IsRescueComplete || _animalSource != null)
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }

            _animalSource = source;
            _boundIndividual = source.Individual;
            return true;
        }

        /// <summary>
        /// Revalidates the destination and capacity at departure; accepts a request only if
        /// present. Exploration has no fabricated request or guaranteed animal.
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
            if (_station.IsSuspended || _station.IsClosed)
            {
                failure = OutingAssemblyFailure.StationRejected;
                return false;
            }

            if (!_mapRules.TryPlanDeparture(pointId, _station.AvailableTemporaryPlaces,
                out var plan, out var selectFailure))
            {
                failure = MapOutingFailure(selectFailure);
                return false;
            }

            if (plan.RequestId != Guid.Empty &&
                (!_mapRules.TrySelectPoint(pointId, out var request, out _) ||
                    !_requestPool.TryGet(plan.RequestId, out var ownedRequest) ||
                    !ReferenceEquals(ownedRequest, request) ||
                    !_requestPool.CanTransition(plan.RequestId, RequestSignal.Accept, out _)))
            {
                failure = OutingAssemblyFailure.RequestNotAvailable;
                return false;
            }

            if (!_session.CanDepart(sessionId, plan, out var acceptFailure))
            {
                failure = MapOutingFailure(acceptFailure);
                return false;
            }

            // No callbacks or interleaving between validation and commit on the gameplay thread.
            if ((plan.RequestId != Guid.Empty &&
                    !_requestPool.TryTransition(plan.RequestId, RequestSignal.Accept, out _)) ||
                !_session.TryDepart(sessionId, plan, out _) ||
                !_flow.TryAdvance(RescueSignal.Depart, out _))
            {
                throw new InvalidOperationException("Validated departure commit failed.");
            }

            return true;
        }

        /// <summary>Called only when the actual route activity starts, not when viewing its entry.</summary>
        public bool TryStartOpeningRoute(Guid sessionId, out OutingAssemblyFailure failure)
        {
            if (!TryRequireActiveSession(sessionId, out failure)) return false;
            if (_flow.Stage != RescueStage.Approach || _flow.IsSuspended)
            {
                failure = _flow.IsSuspended ? OutingAssemblyFailure.Suspended : OutingAssemblyFailure.WrongStage;
                return false;
            }

            if (!_session.TryRecordProgress(sessionId, OutingReturnProgress.OpeningRoute, out var rejected))
            {
                failure = MapOutingFailure(rejected);
                return false;
            }

            return true;
        }

        public bool TryEndUnresolved(Guid sessionId, out OutingSummary summary, out OutingAssemblyFailure failure)
        {
            summary = null;
            if (!TryRequireActiveSession(sessionId, out failure)) return false;
            if (!UnresolvedOutingSettlement.TryEnd(_session, _flow, _requestPool, _clock, _timePolicy,
                out summary, out failure)) return false;
            _lastSummary = summary;
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

            if (signal == RescueSignal.NegotiationCompleted && _animalSource != null)
            {
                if (!HasValidAnimalSource())
                {
                    failure = OutingAssemblyFailure.IdentityMismatch;
                    return false;
                }

                if (!_animalSource.IsRescueComplete)
                {
                    failure = OutingAssemblyFailure.RescueNotComplete;
                    return false;
                }
            }

            var progress = signal == RescueSignal.ApproachCompleted ?
                OutingReturnProgress.Negotiating : OutingReturnProgress.HandingOver;
            if (!_session.CanRecordProgress(sessionId, progress, out var progressFailure))
            {
                failure = MapOutingFailure(progressFailure);
                return false;
            }

            if (_flow.TryAdvance(signal, out var flowFailure))
            {
                if (!_session.TryRecordProgress(sessionId, progress, out _))
                    throw new InvalidOperationException("Validated field progress commit failed.");
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

            if (_session.DeparturePlan?.EntryKind == MapEntryKind.OwnerReunion)
            {
                failure = OutingAssemblyFailure.DirectHandoffRequired;
                return false;
            }

            if (arrival.OutingId != sessionId)
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }

            if ((_animalSource == null && arrival.AnimalAtArrival != null) ||
                (_animalSource != null && (!HasValidAnimalSource() || !_animalSource.IsRescueComplete ||
                    !arrival.MatchesCurrentIndividual(_boundIndividual))))
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }

            if (durationMinutes <= 0)
            {
                failure = OutingAssemblyFailure.InvalidDuration;
                return false;
            }

            // Module-owned gates include duplicate identities, actual session IDs and overflow.
            if (!_station.CanReceive(_stationSessionId, arrival, out var stationFailure))
            {
                failure = MapStationFailure(stationFailure);
                return false;
            }

            var mark = _session.TimeMark;
            var expectedVersion = new WorldTimeVersion(mark.TimeSessionId, mark.EnteredMinutes);
            if (!_clock.CanAdvance(expectedVersion, durationMinutes, out var timeFailure))
            {
                failure = MapTimeFailure(timeFailure);
                return false;
            }

            if (_session.AcceptedRequestId != Guid.Empty &&
                (!_requestPool.TryGet(_session.AcceptedRequestId, out var request) ||
                    request.PointId != _session.SelectedPointId ||
                    !_requestPool.CanTransition(request.RequestId, RequestSignal.Resolve, out _)))
            {
                failure = OutingAssemblyFailure.RequestNotAvailable;
                return false;
            }

            if (!_session.CanSettle(sessionId, out var settleFailure))
            {
                failure = MapOutingFailure(settleFailure);
                return false;
            }

            // Synchronous commit only. An invariant violation is a technical error, not a clean refusal.
            if (!_session.TrySettle(sessionId, out _) ||
                (_session.AcceptedRequestId != Guid.Empty &&
                    !_requestPool.TryTransition(_session.AcceptedRequestId, RequestSignal.Resolve, out _)) ||
                !_station.TryReceive(_stationSessionId, arrival, out var stationReturn, out _) ||
                !_clock.TryAdvance(expectedVersion, durationMinutes, out _, out _) ||
                !_flow.TryAdvance(RescueSignal.ReturnToStation, out _))
            {
                throw new InvalidOperationException("Validated return settlement commit failed.");
            }

            summary = new OutingSummary(sessionId, _session.SelectedPointId,
                _session.AcceptedRequestId, "rescued", durationMinutes);
            _returnedAnimalId = arrival.AnimalId;
            LastStationReturn = stationReturn;
            _lastSummary = summary;
            return true;
        }

        /// <summary>
        /// Completes initial care only after the returned animal's assistant assessment and
        /// necessary safe handoffs. Returning, closing a panel or optional grooming cannot replace them.
        /// </summary>
        public bool TryCompleteCare(Guid sessionId, out OutingAssemblyFailure failure)
        {
            failure = OutingAssemblyFailure.None;
            if (_session == null)
            {
                failure = OutingAssemblyFailure.NoActiveSession;
                return false;
            }

            if (sessionId != _session.SessionId)
            {
                failure = OutingAssemblyFailure.WrongSession;
                return false;
            }

            if (_session.IsSuspended || _flow.IsSuspended)
            {
                failure = OutingAssemblyFailure.Suspended;
                return false;
            }

            if (!_session.IsSettled || _lastSummary == null || _flow.Stage != RescueStage.Care)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }

            if (!_station.CanCompleteNecessaryCare(_stationSessionId, _returnedAnimalId, out var careFailure))
            {
                failure = MapStationFailure(careFailure);
                return false;
            }

            if (!_flow.TryAdvance(RescueSignal.CareCompleted, out var flowFailure))
            {
                failure = MapFlowFailure(flowFailure);
                return false;
            }

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

        private bool HasValidAnimalSource()
        {
            return _animalSource.SessionId == _session.SessionId &&
                ReferenceEquals(_animalSource.Individual, _boundIndividual);
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
                case OutingFailure.CapacityNotAvailable: return OutingAssemblyFailure.StationFull;
                case OutingFailure.InvalidProgress: return OutingAssemblyFailure.WrongStage;
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
                case StationFailure.BasicAssessmentRequired: return OutingAssemblyFailure.BasicAssessmentRequired;
                case StationFailure.NecessaryCarePending: return OutingAssemblyFailure.NecessaryCarePending;
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
