using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable outcome of one outing for the summary panel. Carries only presentation facts
    /// and identity references; no care operations. The outcome kind reflects only what the
    /// rules actually recorded (success/requested-help/ended-early) and makes no claim about
    /// unconfirmed penalty branches.
    /// </summary>
    public sealed class OutingSummary
    {
        public Guid SessionId { get; }

        public string SelectedPointId { get; }

        public Guid RequestId { get; }

        public string OutcomeKey { get; }

        public long ElapsedMinutes { get; }

        public OutingSummary(Guid sessionId, string selectedPointId, Guid requestId,
            string outcomeKey, long elapsedMinutes)
        {
            SessionId = sessionId;
            SelectedPointId = selectedPointId;
            RequestId = requestId;
            OutcomeKey = outcomeKey;
            ElapsedMinutes = elapsedMinutes;
        }
    }
}
