using System;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable mark of the gameplay-time version captured when an outing starts.
    /// The assembly layer passes it in; the map layer never reads a real clock.
    /// A later time commit must reuse this exact mark so a stale outing cannot advance time.
    /// </summary>
    public readonly struct OutingTimeMark
    {
        public Guid TimeSessionId { get; }

        public long EnteredMinutes { get; }

        public OutingTimeMark(Guid timeSessionId, long enteredMinutes)
        {
            TimeSessionId = timeSessionId;
            EnteredMinutes = enteredMinutes;
        }
    }
}
