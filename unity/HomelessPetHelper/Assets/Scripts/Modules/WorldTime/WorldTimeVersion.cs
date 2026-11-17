using System;

namespace HomelessPetHelper.Modules.WorldTime
{
    public readonly struct WorldTimeVersion
    {
        public Guid SessionId { get; }

        public long ElapsedMinutes { get; }

        public WorldTimeVersion(Guid sessionId, long elapsedMinutes)
        {
            SessionId = sessionId;
            ElapsedMinutes = elapsedMinutes;
        }
    }
}
