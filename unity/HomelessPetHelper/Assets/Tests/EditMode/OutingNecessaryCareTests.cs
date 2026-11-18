using System;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;
using static HomelessPetHelper.Tests.EditMode.OutingCoordinatorTests;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class OutingNecessaryCareTests
    {
        [Test]
        public void DirtyHealthyAnimalNeedsAssessmentButNoOptionalCareBeforeNextOuting()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid id = RunToRescued(coordinator, f);
            var arrival = new StationAnimalIntake(id, Guid.NewGuid(), "cat.shy",
                new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 100, Array.Empty<string>()));
            Assert.That(coordinator.TryReturnAndSettle(id, arrival, 30, out var summary, out _), Is.True);
            var clock = f.Clock.Current;
            var animals = f.Station.Animals;
            Assert.That(coordinator.TryCompleteCare(id, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.BasicAssessmentRequired));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Care));
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, arrival.AnimalId,
                Array.Empty<string>(), out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(id, out failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.None));
            Assert.That(f.Clock.Current, Is.SameAs(clock));
            Assert.That(f.Station.Animals, Is.SameAs(animals));
            Assert.That(arrival.Condition.Dirtiness, Is.EqualTo(100));
            Assert.That(coordinator.LastSummary, Is.SameAs(summary));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(coordinator.TryCompleteCare(id, out _), Is.False);
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 8, out _), Is.True);
        }

        [Test]
        public void PendingOrPartiallyHandedOverProblemsBlockOnlyUntilAllAreSafelyArranged()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            Guid id = RunToRescued(coordinator, f);
            var arrival = MakeArrival(id, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(id, arrival, 30, out var summary, out _), Is.True);
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, arrival.AnimalId,
                new[] { "urgent-a", "urgent-b" }, out _), Is.True);
            var clock = f.Clock.Current;
            foreach (string problem in new[] { "urgent-a", "urgent-b" })
            {
                Assert.That(coordinator.TryCompleteCare(id, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.NecessaryCarePending));
                Assert.That(f.Station.TryRequestProfessionalCare(f.StationSessionId, arrival.AnimalId,
                    problem, out _), Is.True);
                Assert.That(coordinator.TryCompleteCare(id, out failure), Is.False);
                Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.NecessaryCarePending));
                Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Care));
                Assert.That(f.Station.TryRecordSafeHandoff(f.StationSessionId, arrival.AnimalId,
                    problem, out _), Is.True);
                Assert.That(f.Clock.Current, Is.SameAs(clock));
                Assert.That(coordinator.LastSummary, Is.SameAs(summary));
            }

            Assert.That(coordinator.TryCompleteCare(id, out _), Is.True);
            Assert.That(f.Station.TryGetCareSnapshot(arrival.AnimalId, out var care), Is.True);
            Assert.That(care.BasicAssessment, Is.EqualTo(StationHealthAssessment.UrgentCareRequired));
            Assert.That(care.NecessaryProblems.Count, Is.EqualTo(2));
            Assert.That(f.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
        }

        [Test]
        public void AnotherAnimalAssessmentCannotReleaseReturnedAnimalAndNextOutingStartsFreshCareOnly()
        {
            var f = CreateFixture(out var coordinator, out _, out var pool, out _, out _);
            Guid firstId = RunToRescued(coordinator, f);
            var first = MakeArrival(firstId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(firstId, first, 1, out _, out _), Is.True);
            var other = MakeArrival(Guid.NewGuid(), Guid.NewGuid());
            Assert.That(f.Station.TryReceive(f.StationSessionId, other, out _, out _), Is.True);
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, other.AnimalId,
                Array.Empty<string>(), out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(firstId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.BasicAssessmentRequired));
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, first.AnimalId,
                Array.Empty<string>(), out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(firstId, out _), Is.True);
            Assert.That(f.Station.TryGetCareSnapshot(first.AnimalId, out var firstCare), Is.True);
            Assert.That(pool.TryAdd(new HelpRequest(Guid.NewGuid(), "help.parking", Parking, 1), out _), Is.True);
            Guid secondId = RunToRescued(coordinator, f);
            var second = MakeArrival(secondId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(secondId, second, 1, out _, out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(firstId, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongSession));
            Assert.That(coordinator.TryCompleteCare(secondId, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.BasicAssessmentRequired));
            Assert.That(f.Station.TryGetCareSnapshot(first.AnimalId, out var latestFirstCare), Is.True);
            Assert.That(latestFirstCare, Is.SameAs(firstCare), "New outings must not reinitialize existing care.");
        }
    }
}
