using System;
using HomelessPetHelper.Modules.Animals;

namespace HomelessPetHelper.Editor.RescueCase
{
    // This one healthy-animal trial deliberately does not model injury, death or medical outcomes.
    internal sealed class CaseAnimal
    {
        internal AnimalIndividual Individual { get; }
        internal Guid AnimalId => Individual.Current.AnimalId;
        internal bool IsHungry
        {
            get => Individual.Current.Disposition.IsHungry;
            private set => SetDisposition(isHungry: value);
        }
        internal bool HasSpace { get; private set; }
        internal bool HasFoodOnGround { get; private set; }
        internal bool IsWithinReach { get; private set; }
        internal CasePlacement Placement { get; private set; }
        internal CaseEmotion Emotion
        {
            get => (CaseEmotion)Individual.Current.Disposition.Emotion;
            private set => SetDisposition(emotion: (AnimalEmotion)value);
        }
        internal CaseBehavior Behavior
        {
            get => (CaseBehavior)Individual.Current.Disposition.Behavior;
            private set => SetDisposition(behavior: (AnimalBehavior)value);
        }
        internal CaseStress Stress
        {
            get => (CaseStress)Individual.Current.Disposition.Stress;
            private set => SetDisposition(stress: (AnimalStress)value);
        }

        internal CaseIntent Intent => !HasSpace || Emotion == CaseEmotion.Startled ? CaseIntent.SeekSpace :
            Stress == CaseStress.Acute ? CaseIntent.Recover :
            IsHungry && HasFoodOnGround ? CaseIntent.WatchFood :
            Placement == CasePlacement.Inspecting ? CaseIntent.InspectPlacement :
            Emotion == CaseEmotion.Relaxed ? CaseIntent.Explore : CaseIntent.Observe;

        internal CaseAnimal(CaseStart start)
        {
            Individual = new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(true, AnimalEmotion.Nervous, AnimalStress.None, AnimalBehavior.Watching),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()));
            if (start == CaseStart.StressRecovery)
            {
                // This fixture represents an already-fed animal startled before this trial begins.
                IsHungry = false;
                Emotion = CaseEmotion.Startled;
                Behavior = CaseBehavior.Retreating;
                Stress = CaseStress.Acute;
            }
        }

        private void SetDisposition(bool? isHungry = null, AnimalEmotion? emotion = null,
            AnimalStress? stress = null, AnimalBehavior? behavior = null)
        {
            AnimalSnapshot current = Individual.Current;
            AnimalDisposition previous = current.Disposition;
            var next = new AnimalDisposition(isHungry ?? previous.IsHungry, emotion ?? previous.Emotion,
                stress ?? previous.Stress, behavior ?? previous.Behavior);
            if (!Individual.TryApplyDisposition(current, next))
            {
                throw new InvalidOperationException("Case disposition changed during synchronous rule execution.");
            }
        }

        internal bool IsStabilizing(CaseAction action)
        {
            return action != CaseAction.Approach || CanApproach();
        }

        internal CaseResponse Respond(CaseAction action)
        {
            switch (action)
            {
                case CaseAction.GiveSpace:
                    bool alreadyHasSpace = HasSpace;
                    HasSpace = true;
                    Emotion = Emotion == CaseEmotion.Startled ? CaseEmotion.Nervous : Emotion;
                    if (TryAcceptPlacement())
                    {
                        return CaseResponse.AcceptedPreparation;
                    }

                    Behavior = CaseBehavior.Watching;
                    if (alreadyHasSpace)
                    {
                        return Stress == CaseStress.Acute ?
                            CaseResponse.SpaceMaintainedUnderStress : CaseResponse.SpaceMaintained;
                    }

                    return Stress == CaseStress.Acute ? CaseResponse.SpaceGivenUnderStress : CaseResponse.SpaceGiven;
                case CaseAction.Wait:
                    return Wait();
                case CaseAction.Food:
                    HasFoodOnGround = true;
                    Behavior = HasSpace && Stress == CaseStress.None ? CaseBehavior.Sniffing : CaseBehavior.Watching;
                    return CaseResponse.FoodPlaced;
                case CaseAction.Toy:
                    return Play();
                case CaseAction.Approach:
                    return Approach();
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        internal void Settle()
        {
            Behavior = CaseBehavior.Settled;
        }

        internal CaseResponse RespondToQuietAction()
        {
            // Ordinary feeding can continue; a phone prompt is not a shortcut to calming or placement.
            return HasSpace && Stress == CaseStress.None && IsHungry && HasFoodOnGround ?
                EatPlacedFood() : CaseResponse.NoNewResponse;
        }

        internal CaseResponse PreparePlacement()
        {
            Placement = CasePlacement.Inspecting;
            Behavior = CaseBehavior.InspectingPlacement;
            return CaseResponse.InspectingPlacement;
        }

        private CaseResponse Wait()
        {
            if (!HasSpace)
            {
                return CaseResponse.Waiting;
            }

            if (Stress == CaseStress.Acute)
            {
                Stress = CaseStress.None;
                Emotion = CaseEmotion.Watchful;
                Behavior = CaseBehavior.Watching;
                return CaseResponse.StressEased;
            }

            if (IsHungry && HasFoodOnGround)
            {
                return EatPlacedFood();
            }

            if (IsHungry)
            {
                return CaseResponse.Waiting;
            }

            if (TryAcceptPlacement())
            {
                return CaseResponse.AcceptedPreparation;
            }

            if (Emotion == CaseEmotion.Relaxed)
            {
                IsWithinReach = true;
                Behavior = CaseBehavior.Approaching;
                return CaseResponse.CameCloser;
            }

            Emotion = Emotion == CaseEmotion.Nervous ? CaseEmotion.Watchful : CaseEmotion.Relaxed;
            Behavior = CaseBehavior.Watching;
            return CaseResponse.Calming;
        }

        private CaseResponse EatPlacedFood()
        {
            IsHungry = false;
            HasFoodOnGround = false;
            Emotion = CaseEmotion.Watchful;
            Behavior = CaseBehavior.Eating;
            return CaseResponse.Ate;
        }

        private CaseResponse Play()
        {
            if (!HasSpace || IsHungry || Emotion == CaseEmotion.Startled || Stress != CaseStress.None)
            {
                return CaseResponse.NotInterested;
            }

            Emotion = CaseEmotion.Relaxed;
            Behavior = CaseBehavior.Playing;
            return CaseResponse.Played;
        }

        private CaseResponse Approach()
        {
            if (CanApproach())
            {
                IsWithinReach = true;
                Behavior = CaseBehavior.PermittingApproach;
                return CaseResponse.Accepted;
            }

            HasSpace = false;
            IsWithinReach = false;
            Emotion = CaseEmotion.Startled;
            Behavior = CaseBehavior.Retreating;
            return CaseResponse.Retreated;
        }

        private bool CanApproach()
        {
            return HasSpace && !IsHungry && Emotion == CaseEmotion.Relaxed && Stress == CaseStress.None;
        }

        private bool TryAcceptPlacement()
        {
            // This cautious-cat fixture accepts a quiet, open placement, not proximity alone.
            if (Placement != CasePlacement.Inspecting || !IsWithinReach || !CanApproach())
            {
                return false;
            }

            Placement = CasePlacement.Accepted;
            Behavior = CaseBehavior.SettlingInPlacement;
            return true;
        }
    }
}
