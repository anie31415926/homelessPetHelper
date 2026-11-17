using System;

namespace HomelessPetHelper.Editor.RescueCase
{
    // This one healthy-animal trial deliberately does not model injury, death or medical outcomes.
    internal sealed class CaseAnimal
    {
        internal Guid AnimalId { get; } = Guid.NewGuid();
        internal bool IsHungry { get; private set; } = true;
        internal bool HasSpace { get; private set; }
        internal bool HasFoodOnGround { get; private set; }
        internal CaseEmotion Emotion { get; private set; } = CaseEmotion.Nervous;
        internal CaseBehavior Behavior { get; private set; } = CaseBehavior.Watching;
        internal CaseStress Stress { get; private set; }

        internal CaseIntent Intent => !HasSpace || Emotion == CaseEmotion.Startled ? CaseIntent.SeekSpace :
            Stress == CaseStress.Acute ? CaseIntent.Recover :
            IsHungry && HasFoodOnGround ? CaseIntent.WatchFood :
            Emotion == CaseEmotion.Relaxed ? CaseIntent.Explore : CaseIntent.Observe;

        internal CaseAnimal(CaseStart start)
        {
            if (start == CaseStart.StressRecovery)
            {
                // This fixture represents an already-fed animal startled before this trial begins.
                IsHungry = false;
                Emotion = CaseEmotion.Startled;
                Behavior = CaseBehavior.Retreating;
                Stress = CaseStress.Acute;
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
                    HasSpace = true;
                    Emotion = Emotion == CaseEmotion.Startled ? CaseEmotion.Nervous : Emotion;
                    Behavior = CaseBehavior.Watching;
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
                IsHungry = false;
                HasFoodOnGround = false;
                Emotion = CaseEmotion.Watchful;
                Behavior = CaseBehavior.Eating;
                return CaseResponse.Ate;
            }

            if (IsHungry)
            {
                return CaseResponse.Waiting;
            }

            if (Emotion == CaseEmotion.Relaxed)
            {
                Behavior = CaseBehavior.Approaching;
                return CaseResponse.CameCloser;
            }

            Emotion = Emotion == CaseEmotion.Nervous ? CaseEmotion.Watchful : CaseEmotion.Relaxed;
            Behavior = CaseBehavior.Watching;
            return CaseResponse.Calming;
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
                Behavior = CaseBehavior.Approaching;
                return CaseResponse.Accepted;
            }

            HasSpace = false;
            Emotion = CaseEmotion.Startled;
            Behavior = CaseBehavior.Retreating;
            return CaseResponse.Retreated;
        }

        private bool CanApproach()
        {
            return HasSpace && !IsHungry && Emotion == CaseEmotion.Relaxed && Stress == CaseStress.None;
        }
    }
}
