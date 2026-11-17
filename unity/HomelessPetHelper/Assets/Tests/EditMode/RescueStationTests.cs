using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueStationTests
    {
        private Guid _sessionId;
        private RescueStation _station;

        [SetUp]
        public void SetUp()
        {
            _sessionId = Guid.NewGuid();
            _station = new RescueStation(_sessionId, 2);
        }

        [Test]
        public void NewStationHasSeparateEmptyTemporaryRoster()
        {
            Assert.That(_station.Animals, Is.Empty);
            Assert.That(_station.TemporaryCapacity, Is.EqualTo(2));
            Assert.That(_station.AvailableTemporaryPlaces, Is.EqualTo(2));
            Assert.That(_station.IsFull, Is.False);
            Assert.That(_station.IsSuspended, Is.False);
            Assert.That(_station.IsClosed, Is.False);
        }

        [Test]
        public void InvalidConstructionIsRejected()
        {
            Assert.Throws<ArgumentException>(() => new RescueStation(Guid.Empty, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RescueStation(_sessionId, -1));
        }

        [Test]
        public void ReceivePreservesIdentityConditionAndSummary()
        {
            var condition = new AnimalIntakeCondition(StationHealthAssessment.UrgentCareRequired,
                63, new[] { "known-injury" });
            var arrival = CreateArrival(condition);
            Assert.That(_station.TryReceive(_sessionId, arrival, out var summary, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(StationFailure.None));
            Assert.That(_station.Animals, Is.EqualTo(new[] { arrival }));
            Assert.That(_station.TryGetAnimal(arrival.AnimalId, out var animal), Is.True);
            Assert.That(animal, Is.SameAs(arrival));
            Assert.That(animal.Condition, Is.SameAs(condition));
            Assert.That(summary.Arrival, Is.SameAs(arrival));
            Assert.That(summary.OccupiedTemporaryPlacesOnArrival, Is.EqualTo(1));
            Assert.That(summary.TemporaryCapacityOnArrival, Is.EqualTo(2));
            Assert.That(_station.AvailableTemporaryPlaces, Is.EqualTo(1));
        }

        [Test]
        public void AdmissionPrecheckDoesNotReserveOrReceiveAnimal()
        {
            var arrival = CreateArrival();
            var roster = _station.Animals;
            Assert.That(_station.CanReceive(_sessionId, arrival, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(StationFailure.None));
            Assert.That(_station.Animals, Is.SameAs(roster));
            Assert.That(_station.TryGetAnimal(arrival.AnimalId, out _), Is.False);
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
            Assert.That(_station.CanReceive(_sessionId, arrival, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.DuplicateOuting));
        }

        [Test]
        public void UncheckedArrivalIsNotAutomaticallyDiagnosedOrCleaned()
        {
            var arrival = CreateArrival(new AnimalIntakeCondition(StationHealthAssessment.Unchecked,
                100, Array.Empty<string>()));
            Assert.That(_station.TryReceive(_sessionId, arrival, out var summary, out _), Is.True);
            Assert.That(summary.Arrival.Condition.HealthAssessment, Is.EqualTo(StationHealthAssessment.Unchecked));
            Assert.That(summary.Arrival.Condition.Dirtiness, Is.EqualTo(100));
            Assert.That(summary.Arrival.Condition.KnownConditionIds, Is.Empty);
        }

        [Test]
        public void CleanArrivalIsAcceptedWithoutInventedCareRequirements()
        {
            var arrival = CreateArrival(new AnimalIntakeCondition(StationHealthAssessment.NoUrgentCareRequired,
                0, Array.Empty<string>()));
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
            Assert.That(arrival.Condition.Dirtiness, Is.Zero);
            Assert.That(arrival.Condition.HealthAssessment, Is.EqualTo(StationHealthAssessment.NoUrgentCareRequired));
        }

        [Test]
        public void DuplicateArrivalDoesNotConsumeSecondPlaceOrCreateSummary()
        {
            var arrival = CreateArrival();
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
            var snapshot = _station.Animals;
            Assert.That(_station.TryReceive(_sessionId, arrival, out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.DuplicateOuting));
            Assert.That(summary, Is.Null);
            Assert.That(_station.Animals, Is.SameAs(snapshot));
            Assert.That(_station.AvailableTemporaryPlaces, Is.EqualTo(1));
        }

        [Test]
        public void SameAnimalInAnotherOutingIsRejected()
        {
            var arrival = CreateArrival();
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
            var repeated = new StationAnimalIntake(Guid.NewGuid(), arrival.AnimalId,
                arrival.AnimalDefinitionId, arrival.Condition);
            Assert.That(_station.TryReceive(_sessionId, repeated, out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.DuplicateAnimal));
            Assert.That(summary, Is.Null);
            Assert.That(_station.Animals.Count, Is.EqualTo(1));
        }

        [Test]
        public void AnotherAnimalInSameOutingIsRejected()
        {
            var arrival = CreateArrival();
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
            var repeated = new StationAnimalIntake(arrival.OutingId, Guid.NewGuid(),
                arrival.AnimalDefinitionId, arrival.Condition);
            Assert.That(_station.TryReceive(_sessionId, repeated, out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.DuplicateOuting));
            Assert.That(_station.Animals.Count, Is.EqualTo(1));
        }

        [Test]
        public void FullStationRejectsWithoutReservingIdsAndExpansionAllowsRetry()
        {
            Assert.That(_station.TryReceive(_sessionId, CreateArrival(), out _, out _), Is.True);
            Assert.That(_station.TryReceive(_sessionId, CreateArrival(), out _, out _), Is.True);
            var pending = CreateArrival();
            var snapshot = _station.Animals;
            Assert.That(_station.IsFull, Is.True);
            Assert.That(_station.TryReceive(_sessionId, pending, out var summary, out var failure), Is.False);
            Assert.That(summary, Is.Null);
            Assert.That(failure, Is.EqualTo(StationFailure.TemporaryCapacityFull));
            Assert.That(_station.Animals, Is.SameAs(snapshot));
            Assert.That(_station.TryGetAnimal(pending.AnimalId, out _), Is.False);
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, 3, out _), Is.True);
            Assert.That(_station.TryReceive(_sessionId, pending, out _, out _), Is.True);
            Assert.That(_station.Animals.Count, Is.EqualTo(3));
        }

        [Test]
        public void ZeroCapacityRejectsAdmissionUntilApprovedExpansion()
        {
            var station = new RescueStation(_sessionId, 0);
            Assert.That(station.IsFull, Is.True);
            Assert.That(station.TryReceive(_sessionId, CreateArrival(), out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.TemporaryCapacityFull));
            Assert.That(station.TryExpandTemporaryCapacity(_sessionId, 1, out _), Is.True);
            Assert.That(station.IsFull, Is.False);
        }

        [Test]
        public void NullArrivalIsRejectedWithoutMutation()
        {
            Assert.That(_station.TryReceive(_sessionId, null, out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.InvalidArrival));
            Assert.That(summary, Is.Null);
            Assert.That(_station.Animals, Is.Empty);
        }

        [Test]
        public void WrongSessionCannotAdmitOrExpand()
        {
            Assert.That(_station.TryReceive(Guid.NewGuid(), CreateArrival(),
                out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.WrongSession));
            Assert.That(summary, Is.Null);
            Assert.That(_station.TryExpandTemporaryCapacity(Guid.Empty, 3, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.WrongSession));
            Assert.That(_station.TemporaryCapacity, Is.EqualTo(2));
            Assert.That(_station.Animals, Is.Empty);
        }

        [Test]
        public void SuspensionRejectsMutationsAndResumePreservesPendingAdmission()
        {
            var arrival = CreateArrival();
            _station.SetSuspended(true);
            _station.SetSuspended(true);
            Assert.That(_station.TryReceive(_sessionId, arrival, out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.Suspended));
            Assert.That(summary, Is.Null);
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, 3, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.Suspended));
            _station.SetSuspended(false);
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
        }

        [Test]
        public void ClosedStationKeepsSnapshotButRejectsAllFurtherMutations()
        {
            var arrival = CreateArrival();
            Assert.That(_station.TryReceive(_sessionId, arrival, out _, out _), Is.True);
            _station.Close();
            _station.Close();
            _station.SetSuspended(false);
            Assert.That(_station.TryReceive(_sessionId, CreateArrival(), out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.Closed));
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, 3, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.Closed));
            Assert.That(_station.TryGetAnimal(arrival.AnimalId, out _), Is.True);
            Assert.That(_station.Animals.Count, Is.EqualTo(1));
            Assert.That(_station.TemporaryCapacity, Is.EqualTo(2));
        }

        [TestCase(-1, StationFailure.InvalidCapacity)]
        [TestCase(0, StationFailure.CapacityNotIncreased)]
        [TestCase(1, StationFailure.CapacityNotIncreased)]
        [TestCase(2, StationFailure.CapacityNotIncreased)]
        public void InvalidExpansionDoesNotChangeCapacity(int capacity, StationFailure expectedFailure)
        {
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, capacity, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(_station.TemporaryCapacity, Is.EqualTo(2));
        }

        [Test]
        public void RepeatedExpansionDoesNotIncrementAgainAndSupportsIntegerLimit()
        {
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, int.MaxValue, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(StationFailure.None));
            Assert.That(_station.AvailableTemporaryPlaces, Is.EqualTo(int.MaxValue));
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, int.MaxValue, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.CapacityNotIncreased));
            Assert.That(_station.TemporaryCapacity, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void SnapshotsAndHistoricalSummaryDoNotChangeWhenRosterGrows()
        {
            var first = CreateArrival();
            var second = CreateArrival();
            Assert.That(_station.TryReceive(_sessionId, first, out var summary, out _), Is.True);
            var snapshot = _station.Animals;
            Assert.That(_station.TryReceive(_sessionId, second, out _, out _), Is.True);
            Assert.That(_station.TryExpandTemporaryCapacity(_sessionId, 4, out _), Is.True);
            Assert.That(snapshot, Is.EqualTo(new[] { first }));
            Assert.That(_station.Animals, Is.EqualTo(new[] { first, second }));
            Assert.That(summary.OccupiedTemporaryPlacesOnArrival, Is.EqualTo(1));
            Assert.That(summary.TemporaryCapacityOnArrival, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<StationAnimalIntake>)snapshot).Clear());
        }

        [Test]
        public void MissingAnimalQueryAndIndependentInstancesDoNotMutateRoster()
        {
            var other = new RescueStation(Guid.NewGuid(), 1);
            Assert.That(_station.TryReceive(_sessionId, CreateArrival(), out _, out _), Is.True);
            Assert.That(other.TryGetAnimal(Guid.NewGuid(), out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(other.Animals, Is.Empty);
            Assert.That(other.IsSuspended, Is.False);
        }

        [Test]
        public void ConditionCopiesCallerDataAndDoesNotExposeWritableState()
        {
            var ids = new List<string> { "observed-condition" };
            var condition = new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 20, ids);
            ids.Clear();
            Assert.That(condition.KnownConditionIds, Is.EqualTo(new[] { "observed-condition" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)condition.KnownConditionIds).Clear());
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void InvalidHealthAssessmentIsRejected(int assessment)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AnimalIntakeCondition((StationHealthAssessment)assessment, 0, Array.Empty<string>()));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void DirtinessOutsideProvisionalScaleIsRejected(int dirtiness)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new AnimalIntakeCondition(StationHealthAssessment.Unchecked, dirtiness, Array.Empty<string>()));
        }

        [Test]
        public void InvalidConditionIdsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 0, null));
            var invalidSets = new[] { new[] { "" }, new[] { " padded " }, new[] { "id", "id" }, new string[] { null } };
            foreach (var ids in invalidSets)
            {
                Assert.Throws<ArgumentException>(() =>
                    new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 0, ids));
            }
        }

        [Test]
        public void InvalidArrivalIdentityOrConditionIsRejected()
        {
            var condition = new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 0, Array.Empty<string>());
            Assert.Throws<ArgumentException>(() =>
                new StationAnimalIntake(Guid.Empty, Guid.NewGuid(), "cat", condition));
            Assert.Throws<ArgumentException>(() =>
                new StationAnimalIntake(Guid.NewGuid(), Guid.Empty, "cat", condition));
            foreach (string id in new[] { null, "", " ", " cat " })
            {
                Assert.Throws<ArgumentException>(() =>
                    new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), id, condition));
            }

            Assert.Throws<ArgumentNullException>(() =>
                new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "cat", null));
        }

        private static StationAnimalIntake CreateArrival(AnimalIntakeCondition condition = null)
        {
            condition = condition ?? new AnimalIntakeCondition(StationHealthAssessment.Unchecked,
                20, Array.Empty<string>());
            return new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "cat-sample", condition);
        }
    }
}
