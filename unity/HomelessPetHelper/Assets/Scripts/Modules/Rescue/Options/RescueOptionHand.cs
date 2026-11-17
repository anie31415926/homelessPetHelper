using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    /// <summary>
    /// Owns option bookkeeping only, on the calling gameplay thread. It does not draw randomly,
    /// evaluate animal safety, spend supplies, apply actions or certify rescue completion.
    /// The session owner provides a unique session ID and closes this hand when that session ends.
    /// </summary>
    public sealed class RescueOptionHand
    {
        private readonly Guid _sessionId;
        private readonly RescueOptionRules _rules;
        private ReadOnlyCollection<RescueOption> _options = Array.AsReadOnly(Array.Empty<RescueOption>());
        private int _turnNumber;
        private long _lastInstanceId;

        public OptionTurnId CurrentTurn => new OptionTurnId(_sessionId, _turnNumber);

        public OptionHandPhase Phase { get; private set; }

        public bool IsSuspended { get; private set; }

        public IReadOnlyList<RescueOption> Options => _options;

        public RescueOptionHand(Guid sessionId, RescueOptionRules rules)
        {
            if (sessionId == Guid.Empty)
            {
                throw new ArgumentException("A nonempty, session-owned ID is required.", nameof(sessionId));
            }

            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _sessionId = sessionId;
            Phase = OptionHandPhase.AwaitingInitialOffer;
        }

        public void SetSuspended(bool isSuspended)
        {
            IsSuspended = isSuspended;
        }

        public void Close()
        {
            _options = Array.AsReadOnly(Array.Empty<RescueOption>());
            Phase = OptionHandPhase.Closed;
        }

        /// <summary>
        /// Accepts exactly enough pre-drawn definitions to fill all unlocked/empty positions.
        /// The caller must first finish the animal response, then classify stabilizing action IDs
        /// for the upcoming whole turn, including supplies and that animal's intent. This method
        /// checks that retained/new options include one such ID; it cannot verify that classification.
        /// Rejection preserves the hand, turn, locks and instance counter. No manual reroll is allowed.
        /// </summary>
        public bool TryRefresh(OptionTurnId nextTurn, IReadOnlyList<RescueActionDefinition> drawnOptions,
            IReadOnlyCollection<string> stabilizingActionIds, out OptionFailure failure)
        {
            if (_turnNumber == int.MaxValue)
            {
                failure = OptionFailure.CapacityExhausted;
                return false;
            }

            if (!ValidateContext(nextTurn, _turnNumber + 1, out failure))
            {
                return false;
            }

            if (Phase == OptionHandPhase.Choosing)
            {
                failure = OptionFailure.UnexpectedPhase;
                return false;
            }

            if (!TryPrepareOffer(drawnOptions, stabilizingActionIds, out var nextOptions, out failure))
            {
                return false;
            }

            _lastInstanceId += drawnOptions.Count;
            _options = nextOptions.AsReadOnly();
            _turnNumber = nextTurn.TurnNumber;
            Phase = OptionHandPhase.Choosing;
            return true;
        }

        public bool TrySetLocked(OptionTurnId turn, long instanceId, bool isLocked, out OptionFailure failure)
        {
            if (!ValidateChoiceContext(turn, out failure))
            {
                return false;
            }

            int index = FindOptionIndex(instanceId);
            if (index < 0)
            {
                failure = OptionFailure.UnknownOption;
                return false;
            }

            RescueOption option = _options[index];
            if (option.IsLocked == isLocked)
            {
                return true;
            }

            if (isLocked && CountLockedOptions() >= _rules.LockLimit)
            {
                failure = OptionFailure.LockLimitReached;
                return false;
            }

            var nextOptions = new List<RescueOption>(_options);
            nextOptions[index] = new RescueOption(option.InstanceId, option.Definition, isLocked);
            _options = nextOptions.AsReadOnly();
            return true;
        }

        /// <summary>
        /// Consumes selected option instances only. Before calling, the owning rescue rule must
        /// validate all action prerequisites and supply costs; this method does not execute them.
        /// A successful use leaves animal-response/next-turn orchestration to that owner.
        /// Selection is collection-shaped, but current rules permit exactly one item.
        /// Rejection returns an empty result and changes no state. Results/snapshots are immutable.
        /// </summary>
        public bool TryConsumeSelection(OptionTurnId turn, IReadOnlyList<long> selectedInstanceIds,
            out IReadOnlyList<RescueOption> consumedOptions, out OptionFailure failure)
        {
            consumedOptions = Array.Empty<RescueOption>();
            if (!ValidateChoiceContext(turn, out failure))
            {
                return false;
            }

            if (!TryResolveSelection(selectedInstanceIds, out var selectedOptions, out failure))
            {
                return false;
            }

            var selectedIds = new HashSet<long>(selectedInstanceIds);
            var remainingOptions = new List<RescueOption>();
            foreach (RescueOption option in _options)
            {
                if (!selectedIds.Contains(option.InstanceId))
                {
                    remainingOptions.Add(option);
                }
            }

            _options = remainingOptions.AsReadOnly();
            consumedOptions = selectedOptions.AsReadOnly();
            Phase = OptionHandPhase.AwaitingAnimalResponse;
            return true;
        }

        private bool ValidateContext(OptionTurnId turn, int expectedTurn, out OptionFailure failure)
        {
            failure = OptionFailure.None;
            if (Phase == OptionHandPhase.Closed)
            {
                failure = OptionFailure.Closed;
            }
            else if (IsSuspended)
            {
                failure = OptionFailure.Suspended;
            }
            else if (turn.SessionId != _sessionId)
            {
                failure = OptionFailure.WrongSession;
            }
            else if (turn.TurnNumber != expectedTurn)
            {
                failure = OptionFailure.WrongTurn;
            }

            return failure == OptionFailure.None;
        }

        private bool ValidateChoiceContext(OptionTurnId turn, out OptionFailure failure)
        {
            if (!ValidateContext(turn, _turnNumber, out failure))
            {
                return false;
            }

            if (Phase != OptionHandPhase.Choosing)
            {
                failure = OptionFailure.UnexpectedPhase;
                return false;
            }

            return true;
        }

        private bool TryPrepareOffer(IReadOnlyList<RescueActionDefinition> drawnOptions,
            IReadOnlyCollection<string> stabilizingActionIds, out List<RescueOption> nextOptions,
            out OptionFailure failure)
        {
            nextOptions = new List<RescueOption>();
            failure = OptionFailure.InvalidOffer;
            if (drawnOptions == null || stabilizingActionIds == null)
            {
                return false;
            }

            foreach (RescueOption option in _options)
            {
                if (option.IsLocked)
                {
                    nextOptions.Add(option);
                }
            }

            if (drawnOptions.Count != _rules.OfferCount - nextOptions.Count)
            {
                return false;
            }

            if (_lastInstanceId > long.MaxValue - drawnOptions.Count)
            {
                failure = OptionFailure.CapacityExhausted;
                return false;
            }

            if (!TryAppendDrawnOptions(drawnOptions, nextOptions))
            {
                return false;
            }

            return ValidateStabilizingChoice(nextOptions, stabilizingActionIds, out failure);
        }

        private bool TryAppendDrawnOptions(IReadOnlyList<RescueActionDefinition> drawnOptions,
            List<RescueOption> nextOptions)
        {
            for (int index = 0; index < drawnOptions.Count; index++)
            {
                RescueActionDefinition definition = drawnOptions[index];
                if (definition == null)
                {
                    return false;
                }

                nextOptions.Add(new RescueOption(_lastInstanceId + index + 1, definition, false));
            }

            return true;
        }

        private static bool ValidateStabilizingChoice(IReadOnlyList<RescueOption> options,
            IReadOnlyCollection<string> stabilizingActionIds, out OptionFailure failure)
        {
            failure = OptionFailure.InvalidOffer;
            var stabilizingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in stabilizingActionIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
                {
                    return false;
                }

                stabilizingIds.Add(id);
            }

            foreach (RescueOption option in options)
            {
                if (stabilizingIds.Contains(option.Definition.Id))
                {
                    failure = OptionFailure.None;
                    return true;
                }
            }

            failure = OptionFailure.NoStabilizingChoice;
            return false;
        }

        private bool TryResolveSelection(IReadOnlyList<long> selectedInstanceIds,
            out List<RescueOption> selectedOptions, out OptionFailure failure)
        {
            selectedOptions = new List<RescueOption>();
            failure = OptionFailure.InvalidSelection;
            if (selectedInstanceIds == null || selectedInstanceIds.Count == 0)
            {
                return false;
            }

            var uniqueIds = new HashSet<long>();
            foreach (long id in selectedInstanceIds)
            {
                if (!uniqueIds.Add(id))
                {
                    failure = OptionFailure.DuplicateSelection;
                    return false;
                }
            }

            if (selectedInstanceIds.Count > _rules.UseLimit)
            {
                failure = OptionFailure.UseLimitExceeded;
                return false;
            }

            foreach (long id in selectedInstanceIds)
            {
                int index = FindOptionIndex(id);
                if (index < 0)
                {
                    failure = OptionFailure.UnknownOption;
                    return false;
                }

                selectedOptions.Add(_options[index]);
            }

            failure = OptionFailure.None;
            return true;
        }

        private int FindOptionIndex(long instanceId)
        {
            for (int index = 0; index < _options.Count; index++)
            {
                if (_options[index].InstanceId == instanceId)
                {
                    return index;
                }
            }

            return -1;
        }

        private int CountLockedOptions()
        {
            int count = 0;
            foreach (RescueOption option in _options)
            {
                if (option.IsLocked)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
