using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class StationIndividualTests
    {
        [Test]
        public void InspectionReadsExistingHealthAndHistoryDoesNotFollowLiveUpdates()
        {
            Guid session = Guid.NewGuid();
            var station = new RescueStation(session, 1);
            var animal = AnimalIndividualTests.CreateAnimal();
            AnimalSnapshot initial = animal.Current;
            StationAnimalIntake intake = Arrival(animal);
            Assert.That(station.TryReceive(session, intake, out var summary, out _), Is.True);
            Assert.That(station.CanCompleteNecessaryCare(session, initial.AnimalId, out _), Is.False);
            Assert.That(station.TryAssessKnownHealthyAnimal(session, initial.AnimalId, out _), Is.True);
            Assert.That(station.TryGetCareSnapshot(initial.AnimalId, out var assessed), Is.True);
            Assert.That(assessed.HealthAtAssessment, Is.SameAs(initial.Health));
            Assert.That(assessed.BasicAssessment, Is.EqualTo(StationHealthAssessment.NoUrgentCareRequired));
            Assert.That(station.CanCompleteNecessaryCare(session, initial.AnimalId, out _), Is.True);
            Assert.That(intake.Condition.Dirtiness, Is.EqualTo(60), "Optional cleaning is not required.");
            Assert.That(animal.TryApplyDisposition(initial,
                new AnimalDisposition(false, AnimalEmotion.Relaxed, AnimalStress.None, AnimalBehavior.Settled)),
                Is.True);
            Assert.That(station.TryGetCurrentAnimal(initial.AnimalId, out var latest), Is.True);
            Assert.That(latest, Is.SameAs(animal.Current));
            Assert.That(latest.Disposition.IsHungry, Is.False);
            Assert.That(intake.AnimalAtArrival, Is.SameAs(initial));
            Assert.That(summary.Arrival.AnimalAtArrival.Disposition.IsHungry, Is.True);
            Assert.That(summary.Arrival.Condition.HealthAssessment, Is.EqualTo(StationHealthAssessment.Unchecked));
            Assert.That(station.TryAssessKnownHealthyAnimal(session, initial.AnimalId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.BasicAssessmentAlreadyCompleted));
            Assert.That(station.TryGetCareSnapshot(initial.AnimalId, out var stillAssessed), Is.True);
            Assert.That(stillAssessed, Is.SameAs(assessed));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingSourceOrExistingProblemsCannotUseHealthySampleAssessment(bool withProblems)
        {
            Guid session = Guid.NewGuid();
            var station = new RescueStation(session, 1);
            var animal = AnimalIndividualTests.CreateAnimal(new AnimalHealthProblem("p1", "test.type"));
            StationAnimalIntake intake = withProblems ? Arrival(animal) :
                new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "cat.shy", Condition());
            Assert.That(station.TryReceive(session, intake, out _, out _), Is.True);
            station.TryGetCareSnapshot(intake.AnimalId, out var before);
            Assert.That(station.TryAssessKnownHealthyAnimal(session, intake.AnimalId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.AssessmentSourceUnavailable));
            station.TryGetCareSnapshot(intake.AnimalId, out var after);
            Assert.That(after, Is.SameAs(before));
            Assert.That(after.NecessaryCareIsArranged, Is.False);
            Assert.That(after.HealthAtAssessment, Is.Null);
        }

        [Test]
        public void AssessmentGuardsPreserveHealthRosterAndCare()
        {
            Guid session = Guid.NewGuid();
            var station = new RescueStation(session, 1);
            var animal = AnimalIndividualTests.CreateAnimal();
            Assert.That(station.TryReceive(session, Arrival(animal), out _, out _), Is.True);
            station.TryGetCareSnapshot(animal.Current.AnimalId, out var before);
            Assert.That(station.TryAssessKnownHealthyAnimal(Guid.NewGuid(), animal.Current.AnimalId, out _), Is.False);
            Assert.That(station.TryAssessKnownHealthyAnimal(session, Guid.NewGuid(), out _), Is.False);
            station.SetSuspended(true);
            Assert.That(station.TryAssessKnownHealthyAnimal(session, animal.Current.AnimalId, out var paused),
                Is.False);
            Assert.That(paused, Is.EqualTo(StationFailure.Suspended));
            station.SetSuspended(false);
            station.Close();
            Assert.That(station.TryAssessKnownHealthyAnimal(session, animal.Current.AnimalId, out var closed),
                Is.False);
            Assert.That(closed, Is.EqualTo(StationFailure.Closed));
            station.TryGetCareSnapshot(animal.Current.AnimalId, out var after);
            Assert.That(after, Is.SameAs(before));
            Assert.That(station.Animals.Count, Is.EqualTo(1));
            Assert.That(station.Animals[0].AnimalAtArrival, Is.SameAs(animal.Current));
        }

        [Test]
        public void StaleArrivalAndNullOwnerAreRejectedBeforeAdmission()
        {
            var animal = AnimalIndividualTests.CreateAnimal();
            var stale = Arrival(animal);
            Assert.That(animal.TryApplyDisposition(animal.Current,
                new AnimalDisposition(false, AnimalEmotion.Watchful, AnimalStress.None, AnimalBehavior.Eating)),
                Is.True);
            Guid session = Guid.NewGuid();
            var station = new RescueStation(session, 1);
            Assert.That(station.TryReceive(session, stale, out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.InvalidArrival));
            Assert.That(summary, Is.Null);
            Assert.That(station.Animals, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => new StationAnimalIntake(Guid.NewGuid(), null, Condition()));
            Assert.That(station.TryReceive(session, Arrival(animal), out _, out _), Is.True);
        }

        [Test]
        public void ProfessionalArrangementsDoNotEraseActualProblemsOrChangeAssessmentSource()
        {
            Guid session = Guid.NewGuid();
            var station = new RescueStation(session, 1);
            var animal = AnimalIndividualTests.CreateAnimal(new AnimalHealthProblem("p1", "test.type"),
                new AnimalHealthProblem("p2", "test.type"));
            AnimalHealthSnapshot health = animal.Current.Health;
            Assert.That(station.TryReceive(session, Arrival(animal), out _, out _), Is.True);
            Assert.That(station.TryCompleteBasicAssessment(session, animal.Current.AnimalId,
                new[] { "p1", "p2" }, out _), Is.True);
            foreach (string id in new[] { "p1", "p2" })
            {
                Assert.That(station.TryRequestProfessionalCare(session, animal.Current.AnimalId, id, out _), Is.True);
                Assert.That(station.TryRecordSafeHandoff(session, animal.Current.AnimalId, id, out _), Is.True);
            }

            station.TryGetCareSnapshot(animal.Current.AnimalId, out var care);
            Assert.That(care.NecessaryCareIsArranged, Is.True);
            Assert.That(care.HealthAtAssessment, Is.SameAs(health));
            Assert.That(animal.Current.Health, Is.SameAs(health));
            Assert.That(health.Problems.Count, Is.EqualTo(2));
            Assert.That(station.Animals.Count, Is.EqualTo(1));
        }

        internal static StationAnimalIntake Arrival(AnimalIndividual animal, Guid? outingId = null)
        {
            return new StationAnimalIntake(outingId ?? Guid.NewGuid(), animal, Condition());
        }

        private static AnimalIntakeCondition Condition()
        {
            return new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 60, Array.Empty<string>());
        }
    }
}
