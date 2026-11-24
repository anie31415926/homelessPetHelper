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
        public IRouteOpeningActivity OpeningRoute { get; private set; }
        public StationReturnSummary LastArrival { get; private set; }
        public bool IsClosed { get; private set; }

        public GameLoopSession(int capacity, int minutesPerDay, CityMapRules map,
            RequestPoolState requests, OutingTimePolicy timePolicy, long initialElapsedMinutes = 0)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            _timePolicy = timePolicy ?? throw new ArgumentNullException(nameof(timePolicy));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            Requests = requests;
            Station = new RescueStation(StationSessionId, capacity);
            Guid worldId = Guid.NewGuid();
            Clock = new ActionWorldClock(worldId, minutesPerDay, initialElapsedMinutes);
            Outing = new OutingCoordinator(Flow, Station, StationSessionId, Clock, worldId, Map, requests, timePolicy);
        }

        public bool TryOpenMap(Guid outingId, int seed, out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            if (Station.HasAcceptedOuting(outingId))
            {
                failure = OutingAssemblyFailure.SessionIdReused;
                return false;
            }
            // Restored stations start without a live outing. Keep the necessary-care gate without
            // replacing the coordinator's existing stage, pause or infrastructure refusal precedence.
            if (Flow.Stage == RescueStage.Station && !Flow.IsSuspended && !Station.IsSuspended &&
                !Station.IsClosed && !Clock.IsSuspended && !Clock.IsClosed)
            {
                foreach (var animal in Station.Animals)
                {
                    if (Station.CanCompleteNecessaryCare(StationSessionId, animal.AnimalId, out var careFailure))
                        continue;
                    failure = careFailure == StationFailure.BasicAssessmentRequired ?
                        OutingAssemblyFailure.BasicAssessmentRequired : OutingAssemblyFailure.NecessaryCarePending;
                    return false;
                }
            }
            if (!Outing.TryBeginOuting(outingId, seed, out failure)) return false;
            Activity?.Dispose();
            Activity = null;
            OpeningRoute?.Dispose();
            OpeningRoute = null;
            return true;
        }

        /// <summary>Transfers ownership of the actual route objective, including a retained safe route.</summary>
        public bool TryStartOpeningRoute(IRouteOpeningActivity opening, out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            var outing = Outing.Session;
            if (outing == null || OpeningRoute != null || Activity != null ||
                outing.DeparturePlan?.EntryKind != MapEntryKind.HelpRequest)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }
            if (opening == null || opening.SessionId != outing.SessionId || opening.IsClosed ||
                opening.IsSuspended)
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }
            if (!Outing.TryStartOpeningRoute(outing.SessionId, out failure)) return false;
            OpeningRoute = opening;
            return true;
        }

        /// <summary>Only the currently owned, live route can certify entry; rejection changes no flow state.</summary>
        public bool TryEnterRescueAfterOpening(IRescueActivity activity, out OutingAssemblyFailure failure)
        {
            if (!RequireOpen(out failure)) return false;
            var outing = Outing.Session;
            if (outing == null || Flow.Stage != RescueStage.Approach || outing.IsSettled || Activity != null ||
                OpeningRoute == null || !outing.HasOpenedRoute)
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }
            if (Flow.IsSuspended || outing.IsSuspended || OpeningRoute.IsSuspended)
            {
                failure = OutingAssemblyFailure.Suspended;
                return false;
            }
            if (OpeningRoute.IsClosed || OpeningRoute.SessionId != outing.SessionId ||
                activity == null || activity.SessionId != outing.SessionId || activity.Individual == null ||
                activity.IsRescueComplete || activity.IsAssistanceRequested || activity.IsClosed ||
                    activity.IsSuspended)
            {
                failure = OutingAssemblyFailure.IdentityMismatch;
                return false;
            }
            if (!OpeningRoute.IsComplete)
            {
                failure = OutingAssemblyFailure.RouteNotComplete;
                return false;
            }
            if (!Outing.TryAdvanceField(outing.SessionId, RescueSignal.ApproachCompleted, out failure)) return false;
            if (!Outing.TryBindRescueAnimal(outing.SessionId, activity, out _))
                throw new InvalidOperationException("Validated rescue binding failed.");
            Activity = activity;
            OpeningRoute.Dispose();
            OpeningRoute = null;
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
            if (outing.DeparturePlan.EntryKind != MapEntryKind.HelpRequest || outing.HasOpenedRoute ||
                OpeningRoute != null)
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
            OpeningRoute?.Dispose();
            OpeningRoute = null;
            return true;
        }

        /// <summary>Restores the last frozen arrival only on a fresh, staged station session.</summary>
        public void ForgetDepartedResident(Guid animalId)
        {
            Outing.ForgetDepartedResident(animalId);
            if (LastArrival?.Arrival.AnimalId != animalId) return;
            LastArrival = Station.Animals.Count == 0 ? null :
                Station.GetHistoricalReturn(Station.Animals[Station.Animals.Count - 1].AnimalId);
        }

        public void RestoreLastArrival()
        {
            if (Outing.Session != null || LastArrival != null) throw new InvalidOperationException("Session is live.");
            if (Station.Animals.Count > 0)
                LastArrival = Station.GetHistoricalReturn(Station.Animals[Station.Animals.Count - 1].AnimalId);
        }

        public void SetSuspended(bool suspended)
        {
            if (IsClosed) return;
            Flow.SetSuspended(suspended);
            Outing.Session?.SetSuspended(suspended);
            Station.SetSuspended(suspended);
            Clock.SetSuspended(suspended);
            Activity?.SetSuspended(suspended);
            OpeningRoute?.SetSuspended(suspended);
        }

        public void Dispose()
        {
            if (IsClosed) return;
            IsClosed = true;
            Activity?.Dispose();
            Activity = null;
            OpeningRoute?.Dispose();
            OpeningRoute = null;
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
