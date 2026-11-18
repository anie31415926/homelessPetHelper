namespace HomelessPetHelper.Modules.Rescue
{
    /// <summary>
    /// Owns in-memory phase order only. Does not certify path, trust or care objectives,
    /// issue rewards, or persist progress. Use on the Unity main thread.
    /// </summary>
    public sealed class RescueFlow
    {
        public RescueStage Stage { get; private set; } = RescueStage.Station;

        public bool IsSuspended { get; private set; }

        public void SetSuspended(bool isSuspended)
        {
            IsSuspended = isSuspended;
        }

        /// <summary>No successful rescue or intake: cancel the map or explicitly return from the field.</summary>
        public bool TryEndUnresolved(out TransitionFailure failure)
        {
            if (!CanEndUnresolved(out failure)) return false;
            Stage = RescueStage.Station;
            return true;
        }

        public bool CanEndUnresolved(out TransitionFailure failure)
        {
            failure = IsSuspended ? TransitionFailure.Suspended :
                Stage == RescueStage.Map || Stage == RescueStage.Approach || Stage == RescueStage.Negotiation ?
                    TransitionFailure.None : TransitionFailure.UnexpectedSignal;
            return failure == TransitionFailure.None;
        }

        /// <summary>
        /// Advances on an already validated gameplay signal. Rejection changes no state.
        /// Repeated completion signals are rejected after the first transition.
        /// </summary>
        public bool TryAdvance(RescueSignal signal, out TransitionFailure failure)
        {
            if (signal < RescueSignal.LeaveStation || signal > RescueSignal.CareCompleted)
            {
                failure = TransitionFailure.InvalidSignal;
                return false;
            }

            if (IsSuspended)
            {
                failure = TransitionFailure.Suspended;
                return false;
            }

            if (!TryGetNextStage(Stage, signal, out RescueStage nextStage))
            {
                failure = TransitionFailure.UnexpectedSignal;
                return false;
            }

            Stage = nextStage;
            failure = TransitionFailure.None;
            return true;
        }

        private static bool TryGetNextStage(RescueStage stage, RescueSignal signal, out RescueStage nextStage)
        {
            nextStage = stage;
            switch (stage)
            {
                case RescueStage.Station when signal == RescueSignal.LeaveStation:
                    nextStage = RescueStage.Map;
                    return true;
                case RescueStage.Map when signal == RescueSignal.Depart:
                    nextStage = RescueStage.Approach;
                    return true;
                case RescueStage.Approach when signal == RescueSignal.ApproachCompleted:
                    nextStage = RescueStage.Negotiation;
                    return true;
                case RescueStage.Negotiation when signal == RescueSignal.NegotiationCompleted:
                    nextStage = RescueStage.Rescued;
                    return true;
                case RescueStage.Rescued when signal == RescueSignal.ReturnToStation:
                    nextStage = RescueStage.Care;
                    return true;
                case RescueStage.Care when signal == RescueSignal.CareCompleted:
                    nextStage = RescueStage.Station;
                    return true;
                default:
                    return false;
            }
        }
    }
}
