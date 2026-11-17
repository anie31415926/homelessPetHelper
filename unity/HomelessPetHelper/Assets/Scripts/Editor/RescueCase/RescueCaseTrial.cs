using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;

namespace HomelessPetHelper.Editor.RescueCase
{
    /// <summary>
    /// Editor-only trial, not production rescue rules. All commands run synchronously on the owner thread.
    /// Rejected commands preserve animal, supplies, hand and stage. No saves, rewards or world-time effects.
    /// </summary>
    public sealed class RescueCaseTrial : IDisposable
    {
        // This is the sole mutable animal for the entire trial, including summary and station.
        private readonly CaseAnimal _animal;
        private readonly RescueOptionRules _rules = new RescueOptionRules(3, 1, 2);
        private readonly RescueOptionHand _hand;
        private readonly Random _random;

        public CaseStage Stage { get; private set; } = CaseStage.Choosing;
        public CaseResponse LastResponse { get; private set; }
        public CaseIntent Intent => _animal.Intent;
        public CaseAnimalSnapshot Animal => new CaseAnimalSnapshot(_animal);
        public int FoodRemaining { get; private set; }
        public bool HasInteracted { get; private set; }
        public bool IsSuspended => _hand.IsSuspended;
        public OptionTurnId CurrentTurn => _hand.CurrentTurn;
        public IReadOnlyList<RescueOption> Options => _hand.Options;
        public RescueOptionRules Rules => _rules;
        public CaseStart Start { get; }

        public RescueCaseTrial(Guid sessionId, int seed, int foodCount = 2, CaseStart start = CaseStart.Normal)
        {
            if (foodCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(foodCount));
            }

            if (start != CaseStart.Normal && start != CaseStart.StressRecovery)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            _hand = new RescueOptionHand(sessionId, _rules);
            _animal = new CaseAnimal(start);
            _random = new Random(seed);
            Start = start;
            FoodRemaining = foodCount;
            SupplyTurn();
        }

        public void SetSuspended(bool suspended)
        {
            _hand.SetSuspended(suspended);
        }

        public void Dispose()
        {
            _hand.Close();
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
                !ResolveAction(selection, out CaseAction action, out failure))
            {
                return false;
            }

            if (!_hand.TryConsumeSelection(turn, selection, out _, out _))
            {
                failure = CaseFailure.HandRejected;
                return false;
            }

            FoodRemaining -= action == CaseAction.Food ? 1 : 0;
            LastResponse = _animal.Respond(action);
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
            Stage = CaseStage.Responding;
            return true;
        }

        public bool TryReturnToStation(OptionTurnId turn, out CaseFailure failure)
        {
            if (!ValidateStage(turn, CaseStage.Summary, out failure))
            {
                return false;
            }

            LastResponse = CaseResponse.Returned;
            Stage = CaseStage.Station;
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
            LastResponse = CaseResponse.AssistanceRequested;
            Stage = CaseStage.AssistanceRequested;
            return true;
        }

        private bool ResolveAction(IReadOnlyList<long> selection, out CaseAction action, out CaseFailure failure)
        {
            action = default;
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

            action = CaseActions.Parse(option.Definition.Id);
            failure = GetActionFailure(action);
            return failure == CaseFailure.None;
        }

        // Applicability only; command context (stage, pause and turn) is checked on submission.
        public CaseFailure GetOptionFailure(long instanceId)
        {
            RescueOption option = Options.FirstOrDefault(item => item.InstanceId == instanceId);
            return option == null ? CaseFailure.UnknownOption :
                GetActionFailure(CaseActions.Parse(option.Definition.Id));
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
            var retained = Options.Where(option => option.IsLocked).Select(option => option.Definition.Id).ToList();
            var candidates = Enum.GetValues(typeof(CaseAction)).Cast<CaseAction>()
                .Where(action => GetActionFailure(action) == CaseFailure.None).ToList();
            var safe = candidates.Where(_animal.IsStabilizing).Select(CaseActions.Id).ToArray();
            var available = candidates.Select(CaseActions.Id).Where(id => !retained.Contains(id)).ToList();
            for (int index = available.Count - 1; index > 0; index--)
            {
                int swap = _random.Next(index + 1);
                string value = available[index];
                available[index] = available[swap];
                available[swap] = value;
            }

            int count = _rules.OfferCount - retained.Count;
            var drawn = available.Take(count).ToList();
            if (!retained.Concat(drawn).Any(safe.Contains))
            {
                drawn[drawn.Count - 1] = available.First(safe.Contains);
            }

            var turn = new OptionTurnId(CurrentTurn.SessionId, checked(CurrentTurn.TurnNumber + 1));
            var definitions = drawn.Select(id => new RescueActionDefinition(id)).ToArray();
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
