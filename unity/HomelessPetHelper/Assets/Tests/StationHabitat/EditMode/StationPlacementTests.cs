using System;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.Station.Habitat;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.StationHabitat.EditMode
{
    public sealed class StationPlacementTests
    {
        [TestCase(StationHealthAssessment.Unchecked, AnimalStress.None, true)]
        [TestCase(StationHealthAssessment.UrgentCareRequired, AnimalStress.None, true)]
        [TestCase(StationHealthAssessment.NoUrgentCareRequired, AnimalStress.Acute, true)]
        [TestCase(StationHealthAssessment.NoUrgentCareRequired, AnimalStress.None, false)]
        public void PlacementReadsAssessmentAndAcuteStressWithoutGrantingTouch(
            StationHealthAssessment assessment, AnimalStress stress, bool observing)
        {
            var animal = new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(false, AnimalEmotion.Watchful, stress, AnimalBehavior.Settled),
                new AnimalHealthSnapshot(Array.Empty<AnimalHealthProblem>()));
            var before = animal.Current;
            Assert.That(StationHabitatPlacement.NeedsObservation(assessment, before), Is.EqualTo(observing));
            Assert.That(animal.Current, Is.SameAs(before));
            Assert.That(animal.Current.CarePermissions.AcceptsTouch, Is.False);
        }

        [Test]
        public void KnownHealthProblemStillNeedsObservationAfterNonUrgentAssessment()
        {
            var animal = new AnimalIndividual(Guid.NewGuid(), "cat.shy",
                new AnimalDisposition(false, AnimalEmotion.Relaxed, AnimalStress.None, AnimalBehavior.Settled),
                new AnimalHealthSnapshot(new[] { new AnimalHealthProblem("injury-1", "minor-injury") }));
            var before = animal.Current;
            Assert.That(StationHabitatPlacement.NeedsObservation(
                StationHealthAssessment.NoUrgentCareRequired, before), Is.True);
            Assert.That(animal.Current, Is.SameAs(before));
        }

        [Test]
        public void MissingIndividualCannotBeAssumedHealthy()
        {
            Assert.That(StationHabitatPlacement.NeedsObservation(
                StationHealthAssessment.NoUrgentCareRequired, null), Is.True);
        }
    }
}
