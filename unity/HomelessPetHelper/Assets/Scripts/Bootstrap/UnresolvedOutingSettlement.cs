using System;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.WorldTime;

namespace HomelessPetHelper.Bootstrap
{
    /// <summary>Atomic synchronous preflight/commit for map cancellation and unresolved field return.</summary>
    internal static class UnresolvedOutingSettlement
    {
        internal static bool TryEnd(OutingSession session, RescueFlow flow, RequestPoolState pool,
            ActionWorldClock clock, OutingTimePolicy policy, out OutingSummary summary,
            out OutingAssemblyFailure failure)
        {
            summary = null;
            failure = OutingAssemblyFailure.None;
            if (session.IsSuspended || flow.IsSuspended)
            {
                failure = OutingAssemblyFailure.Suspended;
                return false;
            }

            if (!flow.CanEndUnresolved(out _) || !session.CanEndUnresolved(session.SessionId, out _) ||
                (session.HasDeparted ? flow.Stage == RescueStage.Map : flow.Stage != RescueStage.Map))
            {
                failure = OutingAssemblyFailure.WrongStage;
                return false;
            }

            long minutes = 0;
            if (session.HasDeparted && (policy == null ||
                !policy.TryGetDuration(session.Progress, session.HasOpenedRoute, out minutes, out _) || minutes <= 0))
            {
                failure = OutingAssemblyFailure.DurationPolicyRequired;
                return false;
            }

            var expected = new WorldTimeVersion(session.TimeMark.TimeSessionId, session.TimeMark.EnteredMinutes);
            if (minutes > 0 && !clock.CanAdvance(expected, minutes, out var timeFailure))
            {
                failure = timeFailure == WorldTimeFailure.StaleVersion ? OutingAssemblyFailure.TimeVersionStale :
                    timeFailure == WorldTimeFailure.WrongSession ? OutingAssemblyFailure.WrongSession :
                    OutingAssemblyFailure.TimeRejected;
                return false;
            }

            Guid requestId = session.AcceptedRequestId;
            if (requestId != Guid.Empty &&
                (!pool.TryGet(requestId, out var request) || request.PointId != session.SelectedPointId ||
                    !pool.CanTransition(requestId, RequestSignal.ReleaseAfterReturn, out _)))
            {
                failure = OutingAssemblyFailure.RequestNotAvailable;
                return false;
            }

            // No callback or asynchronous work may interleave the validated participants.
            if (!session.TryEndUnresolved(session.SessionId, out _) ||
                (requestId != Guid.Empty && !pool.TryTransition(requestId, RequestSignal.ReleaseAfterReturn, out _)) ||
                (minutes > 0 && !clock.TryAdvance(expected, minutes, out _, out _)) ||
                !flow.TryEndUnresolved(out _))
                throw new InvalidOperationException("Validated unresolved return commit failed.");

            summary = new OutingSummary(session.SessionId, session.SelectedPointId, requestId,
                session.HasDeparted ? "returned-unresolved" : "cancelled-before-departure", minutes);
            return true;
        }
    }
}
