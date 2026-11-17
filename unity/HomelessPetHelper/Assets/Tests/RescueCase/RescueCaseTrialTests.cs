using System;
using System.Linq;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.RescueCase
{
    public sealed class RescueCaseTrialTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void DifferentPathsReachSummaryThenStationWithSameAnimal(bool useToy)
        {
            using (var trial = NewTrial())
            {
                Guid animal = trial.Animal.AnimalId;
                if (useToy)
                {
                    Use(trial, "food");
                    Use(trial, "give-space");
                }
                else
                {
                    Use(trial, "give-space");
                    Use(trial, "food");
                }

                Use(trial, "wait");
                Use(trial, useToy ? "toy" : "wait");
                Use(trial, useToy && trial.Options.Any(option => option.Definition.Id == "approach") ?
                    "approach" : "wait");
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Approachable));
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.False);
                Assert.That(trial.TryPrepareRescue(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Animal.Placement, Is.EqualTo(CasePlacement.Inspecting));
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.InspectingPlacement));
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.False);
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
                Use(trial, "wait");
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.ReadyForRescue));
                Assert.That(trial.Options, Is.Empty);
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Summary));
                Assert.That(trial.TryInteract(trial.CurrentTurn, out _), Is.False);
                Assert.That(trial.TryReturnToStation(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Animal.AnimalId, Is.EqualTo(animal));
                Assert.That(trial.Animal.IsHungry, Is.False);
                Assert.That(trial.TryInteract(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Animal.Behavior, Is.EqualTo(CaseBehavior.Settled));
                Assert.That(trial.TryInteract(trial.CurrentTurn, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(CaseFailure.AlreadyInteracted));
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.False);
            }
        }

        [Test]
        public void PrematureApproachRetreatsWithoutErasingNeedsAndCanRecover()
        {
            using (var trial = NewTrial())
            {
                Use(trial, "food");
                Use(trial, "approach");
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.Retreated));
                Assert.That(trial.Animal.Emotion, Is.EqualTo(CaseEmotion.Startled));
                Assert.That(trial.Animal.IsHungry, Is.True);
                Assert.That(trial.Animal.HasFoodOnGround, Is.True);
                Use(trial, "wait");
                Assert.That(trial.Animal.Emotion, Is.EqualTo(CaseEmotion.Startled));
                Use(trial, "give-space");
                Use(trial, "wait");
                Use(trial, trial.Options.Any(option => option.Definition.Id == "toy") ? "toy" : "wait");
                Use(trial, "wait");
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Approachable));
                Assert.That(trial.TryPrepareRescue(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Responding));
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
                Use(trial, "give-space");
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.ReadyForRescue));
            }
        }

        [Test]
        public void ExhaustedSuppliesLeaveExistingFoodAndAssistanceAvailable()
        {
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), 1731, 1))
            {
                Find(trial, "food");
                RescueOption food = trial.Options.Single(option => option.Definition.Id == "food");
                Assert.That(trial.TrySetLocked(trial.CurrentTurn, food.InstanceId, true, out _), Is.True);
                Use(trial, "food");
                Assert.That(trial.FoodRemaining, Is.Zero);
                Assert.That(trial.Animal.HasFoodOnGround, Is.True);
                Assert.That(trial.Options.Any(option => option.Definition.Id == "food"), Is.False);
                Use(trial, "give-space");
                Use(trial, "wait");
                Assert.That(trial.Animal.IsHungry, Is.False);
                Assert.That(trial.TryRequestAssistance(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.AssistanceRequested));
            }
        }

        [Test]
        public void ExistingFoodStopsFurtherOffersBeforeAndAfterEating()
        {
            using (var trial = NewTrial())
            {
                Use(trial, "food");
                int remaining = trial.FoodRemaining;
                for (int index = 0; index < 12; index++)
                {
                    Assert.That(trial.Options.Any(option => option.Definition.Id == "food"), Is.False);
                    Use(trial, "toy");
                    Assert.That(trial.Animal.HasFoodOnGround, Is.True);
                    Assert.That(trial.FoodRemaining, Is.EqualTo(remaining));
                }

                Use(trial, "give-space");
                Use(trial, "wait");
                Assert.That(trial.Animal.IsHungry, Is.False);
                for (int index = 0; index < 12; index++)
                {
                    Assert.That(trial.Options.Any(option => option.Definition.Id == "food"), Is.False);
                    Assert.That(trial.FoodRemaining, Is.EqualTo(remaining));
                    Use(trial, "give-space");
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1731)]
        [TestCase(int.MaxValue)]
        public void ObsoleteLockedApproachStaysRejectedAndUnlockableDuringInspection(int seed)
        {
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), seed, start: CaseStart.StressRecovery))
            {
                Find(trial, "approach");
                long approach = trial.Options.Single(option => option.Definition.Id == "approach").InstanceId;
                Assert.That(trial.TrySetLocked(trial.CurrentTurn, approach, true, out _), Is.True);
                Find(trial, "toy");
                long toy = trial.Options.Single(option => option.Definition.Id == "toy").InstanceId;
                Assert.That(trial.TrySetLocked(trial.CurrentTurn, toy, true, out _), Is.True);
                if (!trial.Animal.HasSpace)
                {
                    Use(trial, "give-space");
                }
                for (int index = 0; index < 30 && trial.Stage == CaseStage.Choosing; index++)
                {
                    AssertOffer(trial);
                    Use(trial, "wait");
                }

                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Approachable));
                Assert.That(trial.Animal.IsWithinReach, Is.True);
                Assert.That(trial.Animal.Placement, Is.EqualTo(CasePlacement.NotOffered));
                var beforePlacement = trial.Animal;
                string hand = string.Join(",", trial.Options.Select(option => option.InstanceId));
                Assert.That(trial.TryPrepareRescue(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Animal.Behavior, Is.EqualTo(CaseBehavior.InspectingPlacement));
                Assert.That(beforePlacement.Placement, Is.EqualTo(CasePlacement.NotOffered));
                Assert.That(string.Join(",", trial.Options.Select(option => option.InstanceId)), Is.EqualTo(hand));
                string before = Fingerprint(trial);
                trial.SetSuspended(true);
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.False);
                Assert.That(trial.TryPrepareRescue(trial.CurrentTurn, out _), Is.False);
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
                trial.SetSuspended(false);
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
                AssertOffer(trial);
                Assert.That(trial.Options.Single(option => option.InstanceId == approach).IsLocked, Is.True);
                Assert.That(trial.GetOptionFailure(approach), Is.EqualTo(CaseFailure.AlreadyWithinReach));
                before = Fingerprint(trial);
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { approach }, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(CaseFailure.AlreadyWithinReach));
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
                Assert.That(trial.TrySetLocked(trial.CurrentTurn, approach, false, out _), Is.True);
                Assert.That(trial.Options.Any(option => option.InstanceId == approach), Is.True);
                Use(trial, "toy");
                for (int index = 0; index < 8; index++)
                {
                    AssertOffer(trial);
                    Assert.That(trial.Options.Any(option => option.Definition.Id == "approach"), Is.False);
                    Assert.That(trial.Animal.Placement, Is.EqualTo(CasePlacement.Inspecting));
                    Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.False);
                    Use(trial, "toy");
                }

                Use(trial, "wait");
                Assert.That(trial.Animal.Placement, Is.EqualTo(CasePlacement.Accepted));
                Assert.That(trial.Animal.Behavior, Is.EqualTo(CaseBehavior.SettlingInPlacement));
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.ReadyForRescue));
            }
        }

        [Test]
        public void ConsumingLockedFoodAndRejectingRepeatInputIsAtomic()
        {
            // Spend while the selected instance is retained; it must still disappear.
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), 1, 1))
            {
                Find(trial, "food");
                RescueOption food = trial.Options.Single(option => option.Definition.Id == "food");
                trial.TrySetLocked(trial.CurrentTurn, food.InstanceId, true, out _);
                OptionTurnId turn = trial.CurrentTurn;
                Assert.That(trial.TrySubmit(turn, new[] { food.InstanceId }, out _), Is.True);
                string before = Fingerprint(trial);
                Assert.That(trial.TrySubmit(turn, new[] { food.InstanceId }, out _), Is.False);
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
                Assert.That(trial.Options.All(option => option.InstanceId != food.InstanceId), Is.True);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1731)]
        [TestCase(int.MaxValue)]
        public void EveryOfferHasExecutableNonWorseningChoiceWithTwoLocksAndNoFood(int seed)
        {
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), seed, 0))
            {
                for (int index = 0; index < 50; index++)
                {
                    var options = trial.Options;
                    foreach (RescueOption option in options.Take(2))
                    {
                        trial.TrySetLocked(trial.CurrentTurn, option.InstanceId, true, out _);
                    }

                    Assert.That(trial.Options.Count, Is.EqualTo(3));
                    Assert.That(trial.Options.Select(option => option.Definition.Id).Distinct().Count(), Is.EqualTo(3));
                    Assert.That(trial.Options.All(option => option.Definition.Id != "food"), Is.True);
                    RescueOption safe = trial.Options.First(option => option.Definition.Id != "approach");
                    bool hungry = trial.Animal.IsHungry;
                    Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { safe.InstanceId }, out _), Is.True);
                    Assert.That(trial.LastResponse, Is.Not.EqualTo(CaseResponse.Retreated));
                    Assert.That(trial.Animal.IsHungry, Is.EqualTo(hungry));
                    Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
                }

                Assert.That(trial.TryRequestAssistance(trial.CurrentTurn, out _), Is.True);
            }
        }

        [Test]
        public void InvalidStalePausedAndDuplicateInputsPreserveWholeState()
        {
            using (var trial = NewTrial())
            {
                OptionTurnId oldTurn = trial.CurrentTurn;
                string before = Fingerprint(trial);
                Assert.That(trial.TrySubmit(oldTurn, null, out _), Is.False);
                Assert.That(trial.TrySubmit(oldTurn, new long[] { -1 }, out _), Is.False);
                Assert.That(trial.TrySubmit(oldTurn, new long[] { 1, 1 }, out _), Is.False);
                var wrong = new OptionTurnId(Guid.NewGuid(), oldTurn.TurnNumber);
                Assert.That(trial.TrySubmit(wrong, new[] { trial.Options[0].InstanceId }, out _), Is.False);
                Assert.That(trial.TryReturnToStation(oldTurn, out _), Is.False);
                Assert.That(trial.TryPrepareRescue(oldTurn, out _), Is.False);
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
                trial.SetSuspended(true);
                Assert.That(trial.TrySubmit(oldTurn, new[] { trial.Options[0].InstanceId }, out _), Is.False);
                Assert.That(trial.TrySetLocked(oldTurn, trial.Options[0].InstanceId, true, out _), Is.False);
                Assert.That(trial.TryRequestAssistance(oldTurn, out _), Is.False);
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
                trial.SetSuspended(false);
                Use(trial, "wait");
                before = Fingerprint(trial);
                Assert.That(trial.TrySubmit(oldTurn, new[] { trial.Options[0].InstanceId }, out _), Is.False);
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
                trial.Dispose();
                Assert.That(trial.TryContinue(trial.CurrentTurn, out var error), Is.False);
                Assert.That(error, Is.EqualTo(CaseFailure.Closed));
            }
        }

        [Test]
        public void AssistanceIsNotSuccessAndCannotReturnOrConsumeMore()
        {
            using (var trial = NewTrial())
            {
                Assert.That(trial.TryRequestAssistance(trial.CurrentTurn, out _), Is.True);
                string before = Fingerprint(trial);
                Assert.That(trial.TryRequestAssistance(trial.CurrentTurn, out _), Is.False);
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.False);
                Assert.That(trial.TryReturnToStation(trial.CurrentTurn, out _), Is.False);
                Assert.That(Fingerprint(trial), Is.EqualTo(before));
            }
        }

        [Test]
        public void SameSeedAndInputsProduceSameSupplyAndResponses()
        {
            using (var first = NewTrial())
            using (var second = NewTrial())
            {
                for (int index = 0; index < 30; index++)
                {
                    Assert.That(Fingerprint(first), Is.EqualTo(Fingerprint(second)));
                    if (first.Stage != CaseStage.Choosing)
                    {
                        break;
                    }

                    var action = first.Options.First(option => option.Definition.Id != "approach");
                    first.TrySubmit(first.CurrentTurn, new[] { action.InstanceId }, out _);
                    second.TrySubmit(second.CurrentTurn, new[] { action.InstanceId }, out _);
                    first.TryContinue(first.CurrentTurn, out _);
                    second.TryContinue(second.CurrentTurn, out _);
                }
            }
        }

        private static RescueCaseTrial NewTrial()
        {
            return new RescueCaseTrial(Guid.NewGuid(), 1731);
        }

        private static void Use(RescueCaseTrial trial, string action)
        {
            Find(trial, action);
            RescueOption option = trial.Options.First(item => item.Definition.Id == action);
            Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { option.InstanceId }, out var failure),
                Is.True, failure.ToString());
            if (trial.Stage == CaseStage.Responding)
            {
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
            }
        }

        private static void Find(RescueCaseTrial trial, string action)
        {
            for (int index = 0; index < 100; index++)
            {
                if (trial.Options.Any(option => option.Definition.Id == action))
                {
                    return;
                }

                RescueOption safe = new[] { "give-space", "toy", "wait" }
                    .Select(id => trial.Options.FirstOrDefault(option => option.Definition.Id == id))
                    .First(option => option != null && trial.GetOptionFailure(option.InstanceId) == CaseFailure.None);
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { safe.InstanceId }, out _), Is.True);
                trial.TryContinue(trial.CurrentTurn, out _);
            }

            Assert.Fail("Trial action did not appear: " + action);
        }

        private static string Fingerprint(RescueCaseTrial trial)
        {
            CaseAnimalSnapshot animal = trial.Animal;
            return string.Join("/", trial.Stage, trial.CurrentTurn.TurnNumber, trial.FoodRemaining,
                animal.IsHungry, animal.HasSpace, animal.HasFoodOnGround,
                animal.Emotion, animal.Behavior, animal.Stress, animal.IsWithinReach, animal.Placement,
                trial.LastResponse, trial.HasInteracted,
                string.Join(",", trial.Options.Select(option =>
                    option.InstanceId + ":" + option.Definition.Id + ":" + option.IsLocked)));
        }

        private static void AssertOffer(RescueCaseTrial trial)
        {
            Assert.That(trial.Options.Count, Is.EqualTo(3));
            Assert.That(trial.Options.Select(option => option.Definition.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(trial.Options.Any(option => trial.GetOptionFailure(option.InstanceId) == CaseFailure.None &&
                (option.Definition.Id == "give-space" || option.Definition.Id == "wait")), Is.True);
            Assert.That(trial.Options.Where(option => !option.IsLocked)
                .All(option => trial.GetOptionFailure(option.InstanceId) == CaseFailure.None), Is.True);
        }
    }
}
