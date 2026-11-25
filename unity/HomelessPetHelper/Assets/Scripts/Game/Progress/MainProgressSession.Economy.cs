using System;
using HomelessPetHelper.Modules.Rescue;

namespace HomelessPetHelper.Game.Progress
{
    public sealed partial class MainProgressSession
    {
        private readonly StationEconomyRules _economyRules;
        private StationEconomyData _economy;
        private int _pendingRescueAid;
        private bool _pendingAssessmentAid;
        private int _lastRescueAid;
        private Guid _lastPaidOuting;
        internal int RescueAidFor(Guid outing) => outing == _lastPaidOuting ? _lastRescueAid : 0;
        internal int AidBalance => _economy.Balance;
        internal bool HasCleaningUpgrade => _economy.Cleaning;
        internal bool ClipEquipped => _economy.ClipEquipped;
        internal StationEconomyRules EconomyRules => _economyRules;

        internal bool Owns(StationPurchase item) => item switch
        {
            StationPurchase.Beds => _economy.Beds,
            StationPurchase.Cleaning => _economy.Cleaning,
            StationPurchase.Clip => _economy.Clip,
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };

        internal void RecordFirstAssessment() => _pendingAssessmentAid =
            _economyRules != null && !_economy.AssessmentRewarded;

        private StationEconomyData CaptureEconomy()
        {
            var draft = _economy.Copy();
            draft.Balance = checked(draft.Balance + _pendingRescueAid);
            if (_pendingAssessmentAid)
            {
                draft.Balance = checked(draft.Balance + _economyRules.assessmentAid);
                draft.AssessmentRewarded = true;
            }
            return draft;
        }

        private void PublishEconomy(StationEconomyData economy)
        {
            _economy = economy;
            Fields.ClipEquipped = economy.ClipEquipped;
            if (_pendingRescueAid > 0)
            {
                _lastRescueAid = _pendingRescueAid;
                _lastPaidOuting = Session.LastArrival.Arrival.OutingId;
            }
            _pendingRescueAid = 0;
            _pendingAssessmentAid = false;
        }

        private bool CanSpend => _economyRules != null && CanContinue && !Fields.HasActive &&
            !Session.Flow.IsSuspended && !Session.Station.IsSuspended &&
            (Session.Flow.Stage == RescueStage.Station || Session.Flow.Stage == RescueStage.Care);

        internal bool TryPurchase(StationPurchase item)
        {
            if (!CanSpend || Owns(item)) return false;
            int price = _economyRules.Price(item);
            if (AidBalance < price) return false;
            var draft = Capture();
            draft.Economy.Balance -= price;
            switch (item)
            {
                case StationPurchase.Beds:
                    if (draft.Capacity > int.MaxValue - _economyRules.extraBeds) return false;
                    draft.Capacity += _economyRules.extraBeds;
                    draft.Economy.Beds = true;
                    break;
                case StationPurchase.Cleaning: draft.Economy.Cleaning = true; break;
                case StationPurchase.Clip: draft.Economy.Clip = true; break;
            }
            if (!TryCommitStationDraft(draft)) return false;
            if (item == StationPurchase.Beds && !Session.Station.TryExpandTemporaryCapacity(
                Session.StationSessionId, draft.Capacity, out _))
                throw new InvalidOperationException("Committed expansion could not be applied.");
            PublishEconomy(draft.Economy);
            return true;
        }

        internal bool TrySetClipEquipped(bool equipped)
        {
            if (!CanSpend || !_economy.Clip || equipped == ClipEquipped) return false;
            var draft = Capture();
            draft.Economy.ClipEquipped = equipped;
            if (!TryCommitStationDraft(draft)) return false;
            PublishEconomy(draft.Economy);
            return true;
        }
    }
}
