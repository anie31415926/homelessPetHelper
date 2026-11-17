using System;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    public readonly struct OptionTurnId
    {
        public Guid SessionId { get; }

        public int TurnNumber { get; }

        public OptionTurnId(Guid sessionId, int turnNumber)
        {
            SessionId = sessionId;
            TurnNumber = turnNumber;
        }
    }
}
