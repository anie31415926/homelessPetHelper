using System;
using System.Linq;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Rescue.Options;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueFlavorTrialTests
    {
        [TestCase("phone.reply", CaseStart.Normal)]
        [TestCase("phone.photo", CaseStart.Normal)]
        [TestCase("phone.reply", CaseStart.StressRecovery)]
        [TestCase("phone.photo", CaseStart.StressRecovery)]
        public void FlavorConsumesLockedChoiceOnceWithoutInventingAnimalProgress(string id, CaseStart start)
        {
            using (var trial = Create(start))
            {
                Find(trial, id);
                RescueOption flavor = trial.Options.Single(option => option.Definition.Id == id);
                Assert.That(trial.TrySetLocked(trial.CurrentTurn, flavor.InstanceId, true, out _), Is.True);
                string before = AnimalAndSupplies(trial);
                var turn = trial.CurrentTurn;
                Assert.That(trial.TrySubmit(turn, new[] { flavor.InstanceId }, out _), Is.True);
                Assert.That(AnimalAndSupplies(trial), Is.EqualTo(before));
                Assert.That(trial.Stage, Is.EqualTo(CaseStage.Responding));
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.NoNewResponse));
                Assert.That(trial.LastTurnResult.ActionId, Is.EqualTo(id));
                Assert.That(trial.LastTurnResult.FeedbackKey, Is.EqualTo("option." + id + ".feedback"));
                Assert.That(trial.Options.Any(option => option.InstanceId == flavor.InstanceId), Is.False);
                var result = trial.LastTurnResult;
                Assert.That(trial.TrySubmit(turn, new[] { flavor.InstanceId }, out _), Is.False);
                Assert.That(trial.LastTurnResult, Is.SameAs(result));
                Assert.That(AnimalAndSupplies(trial), Is.EqualTo(before));
                Assert.That(trial.TryContinue(turn, out _), Is.True);
                for (int index = 0; index < 30; index++)
                {
                    Assert.That(trial.Options.Any(option => option.Definition.Id == id), Is.False);
                    Use(trial, "give-space");
                }
            }

            using (var restarted = Create(start))
            {
                Find(restarted, id);
                Assert.That(restarted.Options.Any(option => option.Definition.Id == id), Is.True);
            }
        }

        [TestCase("phone.reply")]
        [TestCase("phone.photo")]
        public void QuietActionAllowsActualFeedingButNotCalmingOrPlacement(string id)
        {
            using (var trial = Create())
            {
                Use(trial, "give-space");
                Find(trial, id);
                long flavor = trial.Options.Single(option => option.Definition.Id == id).InstanceId;
                trial.TrySetLocked(trial.CurrentTurn, flavor, true, out _);
                Use(trial, "food");
                Assert.That(trial.Animal.HasFoodOnGround, Is.True);
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { flavor }, out _), Is.True);
                Assert.That(trial.LastResponse, Is.EqualTo(CaseResponse.Ate));
                Assert.That(trial.LastTurnResult.AnimalResponse, Is.EqualTo(CaseResponse.Ate));
                Assert.That(trial.Animal.IsHungry, Is.False);
                Assert.That(trial.Animal.HasFoodOnGround, Is.False);
                Assert.That(trial.Animal.Emotion, Is.EqualTo(CaseEmotion.Watchful));
                Assert.That(trial.Animal.IsWithinReach, Is.False);
                Assert.That(trial.Animal.Placement, Is.EqualTo(CasePlacement.NotOffered));
                Assert.That(trial.FoodRemaining, Is.EqualTo(1));
            }
        }

        [Test]
        public void PausedStaleAndInvalidCommandsDoNotRecordOrUseFlavor()
        {
            using (var trial = Create())
            {
                Find(trial, "phone.reply");
                long id = trial.Options.Single(option => option.Definition.Id == "phone.reply").InstanceId;
                var result = trial.LastTurnResult;
                string before = AnimalAndSupplies(trial);
                trial.SetSuspended(true);
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { id }, out _), Is.False);
                trial.SetSuspended(false);
                Assert.That(trial.TrySubmit(new OptionTurnId(Guid.NewGuid(), trial.CurrentTurn.TurnNumber),
                    new[] { id }, out _), Is.False);
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { id, id }, out _), Is.False);
                Assert.That(trial.LastTurnResult, Is.SameAs(result));
                Assert.That(AnimalAndSupplies(trial), Is.EqualTo(before));
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { id }, out _), Is.True);
                Assert.That(trial.LastTurnResult.ConsecutiveUses, Is.EqualTo(1));
            }
        }

        [Test]
        public void LockedFlavorKeepsIdentityAndExcludesOtherFlavorAcrossTurns()
        {
            using (var trial = Create())
            {
                Find(trial, "phone.reply");
                long id = trial.Options.Single(option => option.Definition.Id == "phone.reply").InstanceId;
                trial.TrySetLocked(trial.CurrentTurn, id, true, out _);
                for (int index = 0; index < 30; index++)
                {
                    var flavor = trial.Options.Where(option => trial.Catalog.Get(option.Definition.Id).Kind ==
                        RescueOptionKind.Flavor).ToArray();
                    Assert.That(flavor.Length, Is.EqualTo(1));
                    Assert.That(flavor.Single().InstanceId, Is.EqualTo(id));
                    Assert.That(flavor.Single().IsLocked, Is.True);
                    Use(trial, "give-space");
                }

                Assert.That(trial.LastTurnResult.ActionId, Is.EqualTo("give-space"));
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { id }, out _), Is.True);
                Assert.That(trial.LastTurnResult.ConsecutiveUses, Is.EqualTo(1));
            }
        }

        private static RescueCaseTrial Create(CaseStart start = CaseStart.Normal)
        {
            return new RescueCaseTrial(Guid.NewGuid(), 1731, start: start,
                catalog: RescueOptionContentTests.LoadSaved().Catalog);
        }

        private static void Use(RescueCaseTrial trial, string id)
        {
            Find(trial, id);
            long instance = trial.Options.Single(option => option.Definition.Id == id).InstanceId;
            Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { instance }, out _), Is.True);
            Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
        }

        private static void Find(RescueCaseTrial trial, string id)
        {
            for (int index = 0; index < 100 && trial.Options.All(option => option.Definition.Id != id); index++)
            {
                RescueOption quiet = new[] { "give-space", "toy", "approach", "wait" }.Select(action =>
                    trial.Options.FirstOrDefault(option => option.Definition.Id == action)).First(option =>
                    option != null);
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { quiet.InstanceId }, out _), Is.True);
                Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
            }

            Assert.That(trial.Options.Any(option => option.Definition.Id == id), Is.True, id);
        }

        private static string AnimalAndSupplies(RescueCaseTrial trial)
        {
            var animal = trial.Animal;
            return string.Join("/", animal.AnimalId, animal.IsHungry, animal.Emotion, animal.Behavior,
                animal.Stress, animal.HasSpace, animal.HasFoodOnGround, animal.IsWithinReach, animal.Placement,
                trial.FoodRemaining);
        }
    }
}
