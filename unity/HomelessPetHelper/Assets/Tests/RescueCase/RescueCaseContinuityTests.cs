using System;
using System.Linq;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.RescueCase
{
    public sealed class RescueCaseContinuityTests
    {
        [Test]
        public void StressStartKeepsFeedingFactAndRequiresSpaceBeforeRecovery()
        {
            using (var trial = Offering(CaseStart.StressRecovery, "wait", "give-space"))
            {
                CaseAnimalSnapshot initial = trial.Animal;
                Assert.That(initial.IsHungry, Is.False);
                Assert.That(initial.Stress, Is.EqualTo(CaseStress.Acute));
                long space = trial.Options.Single(option => option.Definition.Id == "give-space").InstanceId;
                Assert.That(trial.TrySetLocked(trial.CurrentTurn, space, true, out _), Is.True);
                Submit(trial, "wait");
                AssertAnimal(trial.Animal, initial);
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.Waiting));
                Continue(trial);
                AssertAnimal(trial.Animal, initial);
                Submit(trial, "give-space");
                Assert.That(trial.Animal.Stress, Is.EqualTo(CaseStress.Acute));
                Assert.That(trial.Animal.HasSpace, Is.True);
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.SpaceGivenUnderStress));
                Continue(trial);
                UseWhenOffered(trial, "wait");
                Assert.That(trial.Animal.Stress, Is.EqualTo(CaseStress.None));
                Assert.That(trial.Animal.Emotion, Is.EqualTo(CaseEmotion.Watchful));
                Assert.That(trial.Animal.IsHungry, Is.False);
                Assert.That(trial.Animal.AnimalId, Is.EqualTo(initial.AnimalId));
                Assert.That(trial.FoodRemaining, Is.EqualTo(2));
                Assert.That(initial.Stress, Is.EqualTo(CaseStress.Acute), "Old snapshots must not mutate.");
                Assert.That(initial.HasSpace, Is.False);
            }
        }

        [Test]
        public void OrdinaryMistakeAfterEatingPreservesFoodAndDoesNotInventAcuteStress()
        {
            using (var trial = Offering(CaseStart.Normal, "give-space", "food"))
            {
                Submit(trial, "give-space");
                Continue(trial);
                UseWhenOffered(trial, "food");
                UseWhenOffered(trial, "wait");
                Assert.That(trial.Animal.IsHungry, Is.False);
                CaseAnimalSnapshot fed = UseWhenOffered(trial, "approach", "food");
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.Retreated));
                Assert.That(trial.Animal.IsHungry, Is.False);
                Assert.That(trial.Animal.HasFoodOnGround, Is.EqualTo(fed.HasFoodOnGround));
                Assert.That(trial.Animal.Stress, Is.EqualTo(CaseStress.None));
                Assert.That(trial.Animal.AnimalId, Is.EqualTo(fed.AnimalId));
                UseWhenOffered(trial, "give-space");
                Assert.That(trial.Animal.IsHungry, Is.False);
            }
        }

        [Test]
        public void RecoveredAnimalContinuesIntoSummaryAndStationWithoutReinitializing()
        {
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), 1731, 0, CaseStart.StressRecovery))
            {
                CaseAnimalSnapshot initial = trial.Animal;
                for (int index = 0; index < 30 && trial.Stage == CaseStage.Choosing; index++)
                {
                    string action = !trial.Animal.HasSpace ? "give-space" : "wait";
                    UseWhenOffered(trial, action);
                }

                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Approachable));
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.CameCloser));
                CaseAnimalSnapshot recovered = trial.Animal;
                Assert.That(recovered.Stress, Is.EqualTo(CaseStress.None));
                Assert.That(recovered.IsHungry, Is.False);
                Assert.That(trial.TryPrepareRescue(trial.CurrentTurn, out _), Is.True);
                AssertAnimal(trial.Animal, recovered);
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.True);
                AssertAnimal(trial.Animal, recovered);
                Assert.That(trial.TryReturnToStation(trial.CurrentTurn, out _), Is.True);
                AssertAnimal(trial.Animal, recovered);
                Assert.That(trial.TryInteract(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.Animal.AnimalId, Is.EqualTo(initial.AnimalId));
                Assert.That(trial.Animal.IsHungry, Is.False);
                Assert.That(trial.Animal.Stress, Is.EqualTo(CaseStress.None));
                Assert.That(recovered.Behavior, Is.EqualTo(CaseBehavior.Approaching));
                Assert.That(trial.Animal.Behavior, Is.EqualTo(CaseBehavior.Settled));
            }
        }

        [Test]
        public void PausedRejectedAndTerminalCommandsCannotResetLatestAnimal()
        {
            using (var trial = Offering(CaseStart.StressRecovery, "give-space"))
            {
                Submit(trial, "give-space");
                CaseAnimalSnapshot latest = trial.Animal;
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { 999L }, out _), Is.False);
                Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.False);
                trial.SetSuspended(true);
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.False);
                AssertAnimal(trial.Animal, latest);
                trial.SetSuspended(false);
                Continue(trial);
                AssertAnimal(trial.Animal, latest);
                Assert.That(trial.TryRequestAssistance(trial.CurrentTurn, out _), Is.True);
                AssertAnimal(trial.Animal, latest);
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.False);
                AssertAnimal(trial.Animal, latest);
            }
        }

        [Test]
        public void RestartCreatesIndependentAnimalAndOldSessionCannotChangeIt()
        {
            using (var old = new RescueCaseTrial(Guid.NewGuid(), 1731, start: CaseStart.StressRecovery))
            using (var next = new RescueCaseTrial(Guid.NewGuid(), 1731))
            {
                OptionTurnId oldTurn = old.CurrentTurn;
                old.Dispose();
                Assert.That(next.Animal.AnimalId, Is.Not.EqualTo(old.Animal.AnimalId));
                Assert.That(next.Animal.IsHungry, Is.True);
                Assert.That(next.Animal.Stress, Is.EqualTo(CaseStress.None));
                CaseAnimalSnapshot before = next.Animal;
                Assert.That(next.TrySubmit(oldTurn, new[] { next.Options[0].InstanceId }, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(CaseFailure.WrongSession));
                AssertAnimal(next.Animal, before);
            }
        }

        [Test]
        public void UnknownStartingPresetIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new RescueCaseTrial(Guid.NewGuid(), 1, start: (CaseStart)99));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(1731)]
        [TestCase(int.MaxValue)]
        public void StressPresetStillHasSafeChoiceWithTwoRetainedOptionsAndNoFood(int seed)
        {
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), seed, 0, CaseStart.StressRecovery))
            {
                CaseAnimalSnapshot before = trial.Animal;
                for (int index = 0; index < 20; index++)
                {
                    foreach (var option in trial.Options.Where(item => !item.IsLocked).Take(2).ToArray())
                    {
                        if (trial.Options.Count(item => item.IsLocked) < 2)
                        {
                            Assert.That(trial.TrySetLocked(trial.CurrentTurn, option.InstanceId, true, out _), Is.True);
                        }
                    }

                    Assert.That(trial.Options.Any(option => option.Definition.Id == "food"), Is.False);
                    var safe = trial.Options.First(option => option.Definition.Id == "wait" ||
                        option.Definition.Id == "toy");
                    Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { safe.InstanceId }, out _), Is.True);
                    AssertAnimal(trial.Animal, before);
                    Continue(trial);
                    AssertAnimal(trial.Animal, before);
                }

                Assert.That(trial.TryRequestAssistance(trial.CurrentTurn, out _), Is.True);
            }
        }

        private static RescueCaseTrial Offering(CaseStart start, params string[] actions)
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var trial = new RescueCaseTrial(Guid.NewGuid(), seed, start: start);
                if (actions.All(id => trial.Options.Any(option => option.Definition.Id == id)))
                {
                    return trial;
                }

                trial.Dispose();
            }

            throw new InvalidOperationException("No fixture offer contained the requested actions.");
        }

        private static CaseAnimalSnapshot UseWhenOffered(RescueCaseTrial trial, string action,
            string filler = "give-space")
        {
            for (int index = 0; index < 50; index++)
            {
                if (trial.Options.Any(option => option.Definition.Id == action))
                {
                    CaseAnimalSnapshot before = trial.Animal;
                    Submit(trial, action);
                    if (trial.Stage == CaseStage.Responding)
                    {
                        Continue(trial);
                    }

                    return before;
                }

                string availableFiller = new[] { filler, "give-space", "toy" }.First(id =>
                    trial.Options.Any(option => option.Definition.Id == id));
                Submit(trial, availableFiller);
                Continue(trial);
            }

            Assert.Fail("Requested fixture action never appeared: " + action);
            return null;
        }

        private static void Submit(RescueCaseTrial trial, string action)
        {
            var option = trial.Options.Single(item => item.Definition.Id == action);
            Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { option.InstanceId }, out var failure),
                Is.True, failure.ToString());
        }

        private static void Continue(RescueCaseTrial trial)
        {
            Assert.That(trial.TryContinue(trial.CurrentTurn, out var failure), Is.True, failure.ToString());
        }

        private static void AssertAnimal(CaseAnimalSnapshot actual, CaseAnimalSnapshot expected)
        {
            Assert.That(actual.AnimalId, Is.EqualTo(expected.AnimalId));
            Assert.That(actual.IsHungry, Is.EqualTo(expected.IsHungry));
            Assert.That(actual.HasSpace, Is.EqualTo(expected.HasSpace));
            Assert.That(actual.HasFoodOnGround, Is.EqualTo(expected.HasFoodOnGround));
            Assert.That(actual.Emotion, Is.EqualTo(expected.Emotion));
            Assert.That(actual.Stress, Is.EqualTo(expected.Stress));
            Assert.That(actual.Behavior, Is.EqualTo(expected.Behavior));
        }
    }
}
