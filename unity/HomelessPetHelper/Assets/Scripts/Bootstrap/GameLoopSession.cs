using System;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.WorldTime;

namespace HomelessPetHelper.Bootstrap
{
    /// <summary>
    /// In-memory owner of one station, clock, map and replaceable rescue activity. No sample rules,
    /// UI, scene loading or persistence. Only this owner closes the shared business session.
    /// </summary>
    public sealed class GameLoopSession : IDisposable
    {
        private readonly OutingTimePolicy _timePolicy;
        public Guid StationSessionId { get; } = Guid.NewGuid();
        public RescueFlow Flow { get; } = new RescueFlow();
        public RescueStation Station { get; }
        public ActionWorldClock Clock { get; }
        public CityMapRules Map { get; }
        public RequestPoolState Requests { get; }
        public OutingCoordinator Outing { get; }
        public IRescueActivity Activity { get; private set; }
        public StationReturnSummary LastArrival { get; private set; }
        public bool IsClosed { get; private set; }

        public GameLoopSession(int capacity, int minutesPerDay, CityMapRules map,
            RequestPoolState requests, OutingTimePolicy timePolicy)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _timePolicy = timePolicy ?? throw new ArgumentNullException(nameof(timePolicy));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            Requests = requests;
            Station = new RescueStation(StationSessionId, capacity);
            Guid worldId = Guid.NewGuid();
            Clock = new ActionWorldClock(worldId, minutesPerDay);
            Outing = new OutingCoordinator(Flow, Station, StationSessionId, Clock, worldId, Map, requests, timePolicy);
        }

        public bool TryOpenMap(Guid outingId, int seed, out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            if (!Outing.TryBeginOuting(outingId, seed, out failure)) return false;
            Activity?.Dispose();
            Activity = null;
            return true;
        }

        /// <summary>
        /// Only for an encounter whose entry requires no route activity. Ownership transfers on success;
        /// callers dispose rejected activities. This does not certify a real opening-route objective.
        /// </summary>
        public bool TryEnterRouteFreeRescue(IRescueActivity activity, out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            OutingSession outing = Outing.Session;
            if (outing == null || Flow.Stage != RescueStage.Approach || outing.IsSettled || Activity != null)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }
            if (Flow.IsSuspended || outing.IsSuspended)
            {
                failure = OutingAssemblyFailure.Suspended;
                return false;
            }
            if (activity == null || activity.SessionId != outing.SessionId || activity.Individual == null ||
                activity.IsRescueComplete || activity.IsAssistanceRequested ||
                activity.IsClosed || activity.IsSuspended)
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest || outing.HasOpenedRoute)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }
            if (!Outing.TryAdvanceField(outing.SessionId, RescueSignal.ApproachCompleted, out failure)) return false;
            if (!Outing.TryBindRescueAnimal(outing.SessionId, activity, out _))
                throw new InvalidOperationException("Validated rescue binding failed.");
            Activity = activity;
            return true;
        }

        public bool TryConfirmRescue(out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            if (Activity == null)
            {
                failure = OutingAssemblyFailure.NoActiveSession;
                return false;
            }
            return Outing.TryAdvanceField(Activity.SessionId, RescueSignal.NegotiationCompleted, out failure);
        }

        public bool TryReturnRescued(AnimalIntakeCondition condition, out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            if (Activity == null || condition == null)
            {
                failure = OutingAssemblyFailure.InvalidArrival;
                return false;
            }
            OutingSession outing = Outing.Session;
            if (!_timePolicy.TryGetDuration(outing.Progress, outing.HasOpenedRoute, out long minutes, out _))
            {
                failure = OutingAssemblyFailure.InvalidDuration;
                return false;
            }
            var arrival = new StationAnimalIntake(Activity.SessionId, Activity.Individual, condition);
            if (!Outing.TryReturnAndSettle(Activity.SessionId, arrival, minutes, out _, out failure)) return false;
            LastArrival = Outing.LastStationReturn;
            Activity.Dispose();
            Activity = null;
            return true;
        }

        public bool TryReturnUnresolved(out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            if (Outing.Session == null)
            {
                failure = OutingAssemblyFailure.NoActiveSession;
                return false;
            }
            if (!Outing.TryEndUnresolved(Outing.Session.SessionId, out _, out failure)) return false;
            Activity?.Dispose();
            Activity = null;
            return true;
        }

        public void SetSuspended(bool suspended)
        {
            if (IsClosed) return;
            Flow.SetSuspended(suspended);
            Outing.Session?.SetSuspended(suspended);
            Station.SetSuspended(suspended);
            Clock.SetSuspended(suspended);
            Activity?.SetSuspended(suspended);
        }

        public void Dispose()
        {
            if (IsClosed) return;
            IsClosed = true;
            Activity?.Dispose();
            Activity = null;
            Station.Close();
            Clock.Close();
            Flow.SetSuspended(true);
        }

        private bool RequireOpen(out OutingAssemblyFailure failure)
        {
            failure = IsClosed ? OutingAssemblyFailure.StationRejected : OutingAssemblyFailure.None;
            return !IsClosed;
        }
    }
}
