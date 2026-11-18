using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Rescue.Options;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueAnimalHandoffTests
    {
        [TestCase(CaseStart.Normal)]
        [TestCase(CaseStart.StressRecovery)]
        public void ActualCaseCompletionFeedsOneIndividualIntoCoordinatorAndStation(CaseStart start)
        {
            var f = OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid session = BeginNegotiation(coordinator);
            using (var trial = new RescueCaseTrial(session, 1731, start: start))
            {
                AnimalSnapshot initial = trial.Individual.Current;
                Assert.That(initial.Health.Problems, Is.Empty);
                Assert.That(coordinator.TryBindRescueAnimal(session, trial, out _), Is.True);
                Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted,
                    out var early), Is.False);
                Assert.That(early, Is.EqualTo(OutingAssemblyFailure.RescueNotComplete));
                CompleteRescue(trial);
                Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out _), Is.True);
                AnimalSnapshot rescued = trial.Individual.Current;
                Assert.That(rescued.AnimalId, Is.EqualTo(initial.AnimalId));
                Assert.That(rescued.Health, Is.SameAs(initial.Health));
                Assert.That(rescued.Disposition.IsHungry, Is.False);
                Assert.That(rescued.Disposition.Stress, Is.EqualTo(AnimalStress.None));
                var arrival = StationIndividualTests.Arrival(trial.Individual, session);
                Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out _, out _), Is.True);
                Assert.That(f.Station.TryGetCurrentAnimal(initial.AnimalId, out var current), Is.True);
                Assert.That(current, Is.SameAs(rescued));
                Assert.That(coordinator.TryCompleteCare(session, out _), Is.False);
                Assert.That(f.Station.TryAssessKnownHealthyAnimal(f.StationSessionId, initial.AnimalId, out _),
                    Is.True);
                Assert.That(coordinator.TryCompleteCare(session, out _), Is.True);
                Assert.That(trial.HasInteracted, Is.False, "Petting is not a departure condition.");
                Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
                Assert.That(f.Clock.Current.Version.ElapsedMinutes, Is.EqualTo(15));
                Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out _, out _), Is.False);
                Assert.That(f.Station.Animals.Count, Is.EqualTo(1));
                Assert.That(f.Clock.Current.Version.ElapsedMinutes, Is.EqualTo(15));
            }
        }

        [TestCase(CaseStart.Normal)]
        [TestCase(CaseStart.StressRecovery)]
        public void ExistingPreviewReturnChecksSameAnimalWithoutMutatingAdmissionHistory(CaseStart start)
        {
            using (var trial = new RescueCaseTrial(Guid.NewGuid(), 1731, start: start))
            {
                AnimalSnapshot initial = trial.Individual.Current;
                CompleteRescue(trial);
                AnimalSnapshot rescued = trial.Individual.Current;
                Assert.That(trial.TryReturnToStation(trial.CurrentTurn, out _), Is.True);
                Assert.That(trial.CanLeaveStation, Is.False);
                Assert.That(trial.ReturnSummary.Arrival.AnimalAtArrival, Is.SameAs(rescued));
                var beforeCheck = trial.StationCare;
                OptionTurnId turn = trial.CurrentTurn;
                Assert.That(trial.TryCheckAtStation(new OptionTurnId(Guid.NewGuid(), turn.TurnNumber), out _),
                    Is.False);
                Assert.That(trial.TryCheckAtStation(new OptionTurnId(turn.SessionId, turn.TurnNumber + 1), out _),
                    Is.False);
                trial.SetSuspended(true);
                Assert.That(trial.TryCheckAtStation(turn, out _), Is.False);
                Assert.That(trial.CanLeaveStation, Is.False);
                trial.SetSuspended(false);
                Assert.That(trial.StationCare, Is.SameAs(beforeCheck));
                Assert.That(trial.TryCheckAtStation(turn, out _), Is.True);
                StationCareSnapshot checkedCare = trial.StationCare;
                Assert.That(checkedCare.HealthAtAssessment, Is.SameAs(initial.Health));
                Assert.That(trial.Individual.Current, Is.SameAs(rescued), "Inspecting does not reroll health.");
                Assert.That(trial.CanLeaveStation, Is.True);
                Assert.That(trial.TryCheckAtStation(turn, out _), Is.True);
                Assert.That(trial.StationCare, Is.SameAs(checkedCare));
                Assert.That(trial.TryReturnToStation(turn, out _), Is.False);
                Assert.That(trial.TryInteract(turn, out _), Is.True);
                Assert.That(trial.Individual.Current.Disposition.Behavior, Is.EqualTo(AnimalBehavior.Settled));
                Assert.That(trial.ReturnSummary.Arrival.AnimalAtArrival, Is.SameAs(rescued));
                Assert.That(rescued.Disposition.Behavior, Is.EqualTo(AnimalBehavior.SettlingInPlacement));
                Assert.That(trial.Individual.Current.Health, Is.SameAs(initial.Health));
                Assert.That(trial.Individual.Current.AnimalId, Is.EqualTo(initial.AnimalId));
                trial.Dispose();
                Assert.That(trial.TryCheckAtStation(turn, out _), Is.False);
                Assert.That(trial.CanLeaveStation, Is.False);
            }
        }

        [Test]
        public void WrongOrStaleArrivalRefusesWithoutSettlingTimeRequestOrRoster()
        {
            var f = OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid session = BeginNegotiation(coordinator);
            using (var trial = new RescueCaseTrial(session, 1731))
            {
                Assert.That(coordinator.TryBindRescueAnimal(session, trial, out _), Is.True);
                var stale = StationIndividualTests.Arrival(trial.Individual, session);
                CompleteRescue(trial);
                Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out _), Is.True);
                var invalid = new[]
                {
                    stale,
                    StationIndividualTests.Arrival(AnimalIndividualTests.CreateAnimal(), session),
                    StationIndividualTests.Arrival(trial.Individual, Guid.NewGuid()),
                    OutingCoordinatorTests.MakeArrival(session, trial.Individual.Current.AnimalId)
                };
                foreach (StationAnimalIntake arrival in invalid)
                {
                    Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out var summary,
                        out var failure), Is.False);
                    Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.IdentityMismatch));
                    Assert.That(summary, Is.Null);
                    Assert.That(coordinator.Session.IsSettled, Is.False);
                    Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
                    Assert.That(f.Station.Animals, Is.Empty);
                    Assert.That(f.Clock.Current.Version.ElapsedMinutes, Is.EqualTo(0));
                }

                var latest = StationIndividualTests.Arrival(trial.Individual, session);
                Assert.That(coordinator.TryReturnAndSettle(session, latest, 15, out _, out _), Is.True);
            }
        }

        [Test]
        public void ActualReturnRefusesPauseAndCapacityThenRetriesWithSameIndividual()
        {
            var f = OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out _, out _, out _, capacity: 1);
            Guid session = BeginNegotiation(coordinator);
            using (var trial = new RescueCaseTrial(session, 1731))
            {
                Assert.That(coordinator.TryBindRescueAnimal(session, trial, out _), Is.True);
                CompleteRescue(trial);
                Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out _), Is.True);
                var arrival = StationIndividualTests.Arrival(trial.Individual, session);
                AnimalSnapshot rescued = trial.Individual.Current;
                f.Station.SetSuspended(true);
                Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out _, out _), Is.False);
                f.Station.SetSuspended(false);
                Assert.That(f.Station.TryReceive(f.StationSessionId,
                    OutingCoordinatorTests.MakeArrival(Guid.NewGuid(), Guid.NewGuid()), out _, out _), Is.True);
                Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out _, out var full), Is.False);
                Assert.That(full, Is.EqualTo(OutingAssemblyFailure.StationFull));
                Assert.That(coordinator.Session.IsSettled, Is.False);
                Assert.That(f.Clock.Current.Version.ElapsedMinutes, Is.EqualTo(0));
                Assert.That(trial.Individual.Current, Is.SameAs(rescued));
                Assert.That(f.Station.TryExpandTemporaryCapacity(f.StationSessionId, 2, out _), Is.True);
                Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out _, out _), Is.True);
            }
        }

        [Test]
        public void SourceCannotSwapIndividualOrSessionAfterBinding()
        {
            OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid session = BeginNegotiation(coordinator);
            var source = new ChangingSource { SessionId = session, Individual = AnimalIndividualTests.CreateAnimal() };
            Assert.That(coordinator.TryBindRescueAnimal(session, source, out _), Is.True);
            var original = source.Individual;
            source.Individual = AnimalIndividualTests.CreateAnimal();
            source.IsRescueComplete = true;
            Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out var swapped),
                Is.False);
            Assert.That(swapped, Is.EqualTo(OutingAssemblyFailure.IdentityMismatch));
            source.Individual = original;
            source.SessionId = Guid.NewGuid();
            Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out _), Is.False);
            source.SessionId = session;
            Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out _), Is.True);
            source.Individual = null;
            Assert.That(coordinator.TryReturnAndSettle(session, StationIndividualTests.Arrival(original, session),
                15, out _, out _), Is.False);
        }

        [Test]
        public void BindingRejectsWrongSessionCompletedSourceAndDuplicates()
        {
            OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid session = BeginNegotiation(coordinator);
            using (var wrong = new RescueCaseTrial(Guid.NewGuid(), 1731))
            using (var valid = new RescueCaseTrial(session, 1731))
            {
                Assert.That(coordinator.TryBindRescueAnimal(session, null, out _), Is.False);
                Assert.That(coordinator.TryBindRescueAnimal(session, wrong, out _), Is.False);
                Assert.That(coordinator.TryBindRescueAnimal(Guid.NewGuid(), valid, out _), Is.False);
                Assert.That(coordinator.TryBindRescueAnimal(session,
                    new ChangingSource { SessionId = session, Individual = valid.Individual, IsRescueComplete = true },
                    out _), Is.False);
                Assert.That(coordinator.TryBindRescueAnimal(session, valid, out _), Is.True);
                Assert.That(coordinator.TryBindRescueAnimal(session, valid, out _), Is.False);
            }
        }

        [Test]
        public void LiveArrivalRequiresExplicitSourceBinding()
        {
            var f = OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid session = OutingCoordinatorTests.RunToRescued(coordinator, f);
            var arrival = StationIndividualTests.Arrival(AnimalIndividualTests.CreateAnimal(), session);
            Assert.That(coordinator.TryReturnAndSettle(session, arrival, 15, out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.IdentityMismatch));
            Assert.That(f.Station.Animals, Is.Empty);
        }

        [Test]
        public void NewOutingBindsNewAnimalWithoutResettingThePreviousResident()
        {
            var f = OutingCoordinatorTests.CreateFixture(out var coordinator, out _, out var pool, out _, out _);
            AnimalSnapshot firstResident = null;
            for (int index = 0; index < 2; index++)
            {
                if (index > 0)
                {
                    Assert.That(pool.TryAdd(new HelpRequest(Guid.NewGuid(), "help.parking",
                        OutingCoordinatorTests.Parking, 1), out _), Is.True);
                }

                Guid session = BeginNegotiation(coordinator);
                using (var trial = new RescueCaseTrial(session, 1731))
                {
                    Assert.That(coordinator.TryBindRescueAnimal(session, trial, out _), Is.True);
                    CompleteRescue(trial);
                    Assert.That(coordinator.TryAdvanceField(session, RescueSignal.NegotiationCompleted, out _),
                        Is.True);
                    Assert.That(coordinator.TryReturnAndSettle(session,
                        StationIndividualTests.Arrival(trial.Individual, session), 15, out _, out _), Is.True);
                    Assert.That(f.Station.TryAssessKnownHealthyAnimal(f.StationSessionId,
                        trial.Individual.Current.AnimalId, out _), Is.True);
                    Assert.That(coordinator.TryCompleteCare(session, out _), Is.True);
                    if (index == 0)
                    {
                        firstResident = trial.Individual.Current;
                    }
                    else
                    {
                        Assert.That(trial.Individual.Current.AnimalId, Is.Not.EqualTo(firstResident.AnimalId));
                        Assert.That(f.Station.TryGetCurrentAnimal(firstResident.AnimalId, out var previous), Is.True);
                        Assert.That(previous, Is.SameAs(firstResident));
                    }
                }
            }

            Assert.That(f.Station.Animals.Count, Is.EqualTo(2));
            Assert.That(f.Clock.Current.Version.ElapsedMinutes, Is.EqualTo(30));
        }

        [Test]
        public void CaseEnumAdaptersCoverSharedCategoryValues()
        {
            AssertEnumMapping(typeof(CaseEmotion), typeof(AnimalEmotion));
            AssertEnumMapping(typeof(CaseStress), typeof(AnimalStress));
            AssertEnumMapping(typeof(CaseBehavior), typeof(AnimalBehavior));
        }

        private static void AssertEnumMapping(Type local, Type shared)
        {
            foreach (string name in Enum.GetNames(local))
            {
                Assert.That(Enum.IsDefined(shared, name), Is.True, name);
                Assert.That(Convert.ToInt32(Enum.Parse(local, name)),
                    Is.EqualTo(Convert.ToInt32(Enum.Parse(shared, name))), name);
            }
        }

        private static Guid BeginNegotiation(OutingCoordinator coordinator)
        {
            Guid session = Guid.NewGuid();
            Assert.That(coordinator.TryBeginOuting(session, 1731, out _), Is.True);
            Assert.That(coordinator.TrySelectAndDepart(session, OutingCoordinatorTests.Parking, out _), Is.True);
            Assert.That(coordinator.TryAdvanceField(session, RescueSignal.ApproachCompleted, out _), Is.True);
            return session;
        }

        private static void CompleteRescue(RescueCaseTrial trial)
        {
            for (int index = 0; index < 60 && trial.Stage != CaseStage.ReadyForRescue; index++)
            {
                if (trial.Stage == CaseStage.Approachable)
                {
                    Assert.That(trial.TryPrepareRescue(trial.CurrentTurn, out _), Is.True);
                }

                if (trial.Stage == CaseStage.Responding)
                {
                    Assert.That(trial.TryContinue(trial.CurrentTurn, out _), Is.True);
                }

                string desired = !trial.Animal.HasSpace ? "give-space" :
                    trial.Animal.IsHungry && !trial.Animal.HasFoodOnGround ? "food" : "wait";
                RescueOption option = trial.Options.FirstOrDefault(item => item.Definition.Id == desired) ??
                    trial.Options.First(item => item.Definition.Id == "give-space" || item.Definition.Id == "toy" ||
                        item.Definition.Id == "wait");
                Assert.That(trial.TrySubmit(trial.CurrentTurn, new[] { option.InstanceId }, out _), Is.True);
                Assert.That(trial.Animal.Individual, Is.SameAs(trial.Individual.Current));
            }

            Assert.That(trial.Stage, Is.EqualTo(CaseStage.ReadyForRescue));
            Assert.That(trial.TryFinishRescue(trial.CurrentTurn, out _), Is.True);
            Assert.That(trial.IsRescueComplete, Is.True);
        }

        private sealed class ChangingSource : IRescueAnimalSource
        {
            public Guid SessionId { get; set; }
            public AnimalIndividual Individual { get; set; }
            public bool IsRescueComplete { get; set; }
        }
    }
}
