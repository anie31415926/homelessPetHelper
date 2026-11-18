using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;

namespace HomelessPetHelper.Editor.RescueCase
{
    /// <summary>
    /// Editor-only trial, not production rescue rules. All commands run synchronously on the owner thread.
    /// Rejected commands preserve animal, supplies, hand and stage. No saves, rewards or world-time effects.
    /// </summary>
    public sealed class RescueCaseTrial : IDisposable, IRescueAnimalSource
    {
        // Field context and rules refer to one shared individual, including summary and station.
        private readonly CaseAnimal _animal;
        private readonly RescueOptionRules _rules = new RescueOptionRules(3, 1, 2);
        private readonly RescueOptionHand _hand;
        private readonly Random _random;
        private readonly RescueStation _station;
        private readonly Guid _stationSessionId = Guid.NewGuid();
        private readonly HashSet<string> _usedFlavorIds = new HashSet<string>(StringComparer.Ordinal);

        public CaseStage Stage { get; private set; } = CaseStage.Choosing;
        public CaseResponse LastResponse { get; private set; }
        public RescueOptionCatalog Catalog { get; }
        public CaseTurnResult LastTurnResult { get; private set; }
        public CaseIntent Intent => _animal.Intent;
        public CaseAnimalSnapshot Animal => new CaseAnimalSnapshot(_animal);
        public int FoodRemaining { get; private set; }
        public bool HasInteracted { get; private set; }
        public bool IsSuspended => _hand.IsSuspended;
        public OptionTurnId CurrentTurn => _hand.CurrentTurn;
        public IReadOnlyList<RescueOption> Options => _hand.Options;
        public RescueOptionRules Rules => _rules;
        public CaseStart Start { get; }
        public Guid SessionId => CurrentTurn.SessionId;
        public AnimalIndividual Individual => _animal.Individual;
        public bool IsRescueComplete => !IsSuspended &&
            (Stage == CaseStage.Summary || Stage == CaseStage.Station);
        public StationReturnSummary ReturnSummary { get; private set; }
        public bool CanLeaveStation => Stage == CaseStage.Station &&
            _station.CanCompleteNecessaryCare(_stationSessionId, Individual.Current.AnimalId, out _);
        public StationCareSnapshot StationCare
        {
            get
            {
                _station.TryGetCareSnapshot(Individual.Current.AnimalId, out var care);
                return care;
            }
        }

        public RescueCaseTrial(Guid sessionId, int seed, int foodCount = 2, CaseStart start = CaseStart.Normal,
            RescueOptionCatalog catalog = null)
        {
            if (foodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(foodCount));
            }

            if (start != CaseStart.Normal && start != CaseStart.StressRecovery)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            Catalog = catalog ?? CaseActions.CreateSampleCatalog();
            CaseActions.ValidateCatalog(Catalog);
            _hand = new RescueOptionHand(sessionId, _rules);
            _animal = new CaseAnimal(start);
            _station = new RescueStation(_stationSessionId, 1);
            _random = new Random(seed);
            Start = start;
            FoodRemaining = foodCount;
            SupplyTurn();
        }

        public void SetSuspended(bool suspended)
        {
            _hand.SetSuspended(suspended);
            _station.SetSuspended(suspended);
        }

        public void Dispose()
        {
            _hand.Close();
            _station.Close();
            Stage = CaseStage.Closed;
        }

        public bool TrySetLocked(OptionTurnId turn, long id, bool locked, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Choosing, out failure))
            {
                return false;
            }

            if (!_hand.TrySetLocked(turn, id, locked, out _))
            {
                failure = CaseFailure.HandRejected;
                return false;
            }

            return true;
        }

        public bool TrySubmit(OptionTurnId turn, IReadOnlyList<long> selection, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Choosing, out failure) ||
                !ResolveAction(selection, out RescueOptionContent content, out failure))
            {
                return false;
            }

            if (!_hand.TryConsumeSelection(turn, selection, out _, out _))
            {
                failure = CaseFailure.HandRejected;
                return false;
            }

            ApplyAction(turn, content);
            if (_animal.Placement == CasePlacement.Accepted)
            {
                Stage = CaseStage.ReadyForRescue;
                _hand.Close();
            }
            else
            {
                Stage = _animal.IsWithinReach && _animal.Placement == CasePlacement.NotOffered ?
                    CaseStage.Approachable : CaseStage.Responding;
            }

            return true;
        }

        private void ApplyAction(OptionTurnId turn, RescueOptionContent content)
        {
            if (content.Kind == RescueOptionKind.Flavor)
            {
                _usedFlavorIds.Add(content.Id);
                LastResponse = _animal.RespondToQuietAction();
            }
            else
            {
                CaseAction action = CaseActions.Parse(content.Id);
                FoodRemaining -= action == CaseAction.Food ? 1 : 0;
                LastResponse = _animal.Respond(action);
            }

            int consecutive = LastTurnResult != null && LastTurnResult.ActionId == content.Id ?
                Math.Min(LastTurnResult.ConsecutiveUses, int.MaxValue - 1) + 1 : 1;
            LastTurnResult = new CaseTurnResult(turn, content, LastResponse, consecutive);
        }

        public bool TryContinue(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Responding, out failure))
            {
                return false;
            }

            SupplyTurn();
            Stage = CaseStage.Choosing;
            return true;
        }

        public bool TryFinishRescue(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.ReadyForRescue, out failure))
            {
                return false;
            }

            LastResponse = CaseResponse.Rescued;
            Stage = CaseStage.Summary;
            return true;
        }

        public bool TryPrepareRescue(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Approachable, out failure))
            {
                return false;
            }

            LastResponse = _animal.PreparePlacement();
            LastTurnResult = null;
            Stage = CaseStage.Responding;
            return true;
        }

        public bool TryReturnToStation(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Summary, out failure))
            {
                return false;
            }

            var arrival = new StationAnimalIntake(SessionId, Individual,
                new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 0, Array.Empty<string>()));
            if (!_station.TryReceive(_stationSessionId, arrival, out var summary, out _))
            {
                failure = CaseFailure.StationRejected;
                return false;
            }

            ReturnSummary = summary;
            LastResponse = CaseResponse.Returned;
            Stage = CaseStage.Station;
            return true;
        }

        public bool TryCheckAtStation(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Station, out failure))
            {
                return false;
            }

            if (StationCare.BasicAssessment != StationHealthAssessment.Unchecked)
            {
                return true;
            }

            if (!_station.TryAssessKnownHealthyAnimal(_stationSessionId, Individual.Current.AnimalId, out _))
            {
                failure = CaseFailure.AssessmentUnavailable;
                return false;
            }

            LastResponse = CaseResponse.CheckedAtStation;
            return true;
        }

        public bool TryInteract(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Station, out failure))
            {
                return false;
            }

            if (HasInteracted)
            {
                failure = CaseFailure.AlreadyInteracted;
                return false;
            }

            HasInteracted = true;
            _animal.Settle();
            LastResponse = CaseResponse.Settled;
            return true;
        }

        public bool TryRequestAssistance(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateContext(turn, out failure))
            {
                return false;
            }

            if (Stage != CaseStage.Choosing && Stage != CaseStage.Responding &&
                Stage != CaseStage.Approachable && Stage != CaseStage.ReadyForRescue)
            {
                failure = CaseFailure.UnexpectedStage;
                return false;
            }

            _hand.Close();
            LastTurnResult = null;
            LastResponse = CaseResponse.AssistanceRequested;
            Stage = CaseStage.AssistanceRequested;
            return true;
        }

        private bool ResolveAction(IReadOnlyList<long> selection, out RescueOptionContent content,
            out CaseFailure failure)
        {
            content = null;
            failure = CaseFailure.InvalidSelection;
            if (selection == null || selection.Count != 1)
            {
                return false;
            }

            RescueOption option = Options.FirstOrDefault(item => item.InstanceId == selection[0]);
            if (option == null)
            {
                failure = CaseFailure.UnknownOption;
                return false;
            }

            content = Catalog.Get(option.Definition.Id);
            failure = GetActionFailure(content);
            return failure == CaseFailure.None;
        }

        // Applicability only; command context (stage, pause and turn) is checked on submission.
        public CaseFailure GetOptionFailure(long instanceId)
        {
            RescueOption option = Options.FirstOrDefault(item => item.InstanceId == instanceId);
            return option == null ? CaseFailure.UnknownOption :
                GetActionFailure(Catalog.Get(option.Definition.Id));
        }

        private CaseFailure GetActionFailure(RescueOptionContent content)
        {
            return content.Kind == RescueOptionKind.Flavor ?
                (_usedFlavorIds.Contains(content.Id) ? CaseFailure.FlavorAlreadyUsed : CaseFailure.None) :
                GetActionFailure(CaseActions.Parse(content.Id));
        }

        private CaseFailure GetActionFailure(CaseAction action)
        {
            if (action == CaseAction.Food)
            {
                if (FoodRemaining == 0)
                {
                    return CaseFailure.NoFood;
                }

                if (!_animal.IsHungry)
                {
                    return CaseFailure.AlreadyFed;
                }

                if (_animal.HasFoodOnGround)
                {
                    return CaseFailure.FoodAlreadyPlaced;
                }
            }

            return action == CaseAction.Approach && _animal.IsWithinReach ?
                CaseFailure.AlreadyWithinReach : CaseFailure.None;
        }

        private void SupplyTurn()
        {
            var candidates = Catalog.Entries.Where(entry => GetActionFailure(entry) == CaseFailure.None).ToArray();
            var safe = candidates.Where(entry => entry.Kind == RescueOptionKind.Rescue &&
                _animal.IsStabilizing(CaseActions.Parse(entry.Id))).Select(entry => entry.Id).ToArray();
            var definitions = RescueOptionSupply.Draw(Catalog, Options, candidates.Select(entry => entry.Id).ToArray(),
                safe, _rules.OfferCount, _random);
            var turn = new OptionTurnId(CurrentTurn.SessionId, checked(CurrentTurn.TurnNumber + 1));
            if (!_hand.TryRefresh(turn, definitions, safe, out var error))
            {
                throw new InvalidOperationException("Trial supply invariant failed: " + error);
            }
        }

        private bool ValidateStage(OptionTurnId turn, CaseStage expected, out CaseFailure failure)
        {
            if (!ValidateContext(turn, out failure))
            {
                return false;
            }

            if (Stage != expected)
            {
                failure = CaseFailure.UnexpectedStage;
                return false;
            }

            return true;
        }

        private bool ValidateContext(OptionTurnId turn, out CaseFailure failure)
        {
            failure = CaseFailure.None;
            if (Stage == CaseStage.Closed)
            {
                failure = CaseFailure.Closed;
            }
            else if (IsSuspended)
            {
                failure = CaseFailure.Suspended;
            }
            else if (turn.SessionId != CurrentTurn.SessionId)
            {
                failure = CaseFailure.WrongSession;
            }
            else if (turn.TurnNumber != CurrentTurn.TurnNumber)
            {
                failure = CaseFailure.WrongTurn;
            }

            return failure == CaseFailure.None;
        }
    }
}
