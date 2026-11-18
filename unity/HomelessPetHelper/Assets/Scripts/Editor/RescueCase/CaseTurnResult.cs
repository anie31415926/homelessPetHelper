using HomelessPetHelper.Modules.Rescue.Options;

namespace HomelessPetHelper.Editor.RescueCase
{
    public sealed class CaseTurnResult
    {
        public OptionTurnId Turn { get; }
        public string ActionId { get; }
        public string FeedbackKey { get; }
        public CaseResponse AnimalResponse { get; }
        public int ConsecutiveUses { get; }

        internal CaseTurnResult(OptionTurnId turn, RescueOptionContent content, CaseResponse response,
            int consecutiveUses)
        {
            Turn = turn;
            ActionId = content.Id;
            FeedbackKey = content.FeedbackKey;
            AnimalResponse = response;
            ConsecutiveUses = consecutiveUses;
        }
    }
}
