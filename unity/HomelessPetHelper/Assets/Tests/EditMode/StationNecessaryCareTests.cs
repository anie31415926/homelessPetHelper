using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class StationNecessaryCareTests
    {
        private Guid _session;
        private RescueStation _station;
        private StationAnimalIntake _arrival;

        [SetUp]
        public void SetUp()
        {
            _session = Guid.NewGuid();
            _station = new RescueStation(_session, 3);
            _arrival = new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "cat.shy",
                new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 100, new[] { "observed-sign" }));
            Assert.That(_station.TryReceive(_session, _arrival, out _, out _), Is.True);
        }

        [Test]
        public void NewAdmissionNeedsAssistantAssessmentAndHistoryIsNotCurrentCare()
        {
            var initial = Snapshot();
            Assert.That(initial.AnimalId, Is.EqualTo(_arrival.AnimalId));
            Assert.That(initial.OutingId, Is.EqualTo(_arrival.OutingId));
            Assert.That(initial.BasicAssessment, Is.EqualTo(StationHealthAssessment.Unchecked));
            Assert.That(initial.NecessaryCareIsArranged, Is.False);
            Assert.That(_station.CanCompleteNecessaryCare(_session, _arrival.AnimalId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.BasicAssessmentRequired));
            Assert.That(Snapshot(), Is.SameAs(initial));
        }

        [Test]
        public void NoUrgentProblemsAllowsDepartureWithoutCleaningOrInteraction()
        {
            var before = Snapshot();
            Assess();
            Assert.That(_station.CanCompleteNecessaryCare(_session, _arrival.AnimalId, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(StationFailure.None));
            Assert.That(Snapshot().BasicAssessment, Is.EqualTo(StationHealthAssessment.NoUrgentCareRequired));
            Assert.That(Snapshot().NecessaryCareIsArranged, Is.True);
            Assert.That(before.BasicAssessment, Is.EqualTo(StationHealthAssessment.Unchecked));
            Assert.That(_arrival.Condition.Dirtiness, Is.EqualTo(100));
            Assert.That(_arrival.Condition.HealthAssessment, Is.EqualTo(StationHealthAssessment.Unchecked));
            Assert.That(_arrival.Condition.KnownConditionIds, Is.EqualTo(new[] { "observed-sign" }));
        }

        [Test]
        public void EveryNecessaryProblemRequiresConfirmationAndActualSafeAcceptance()
        {
            Assess("injury", "other-urgent-sign");
            var assessed = Snapshot();
            Assert.That(_station.TryRecordSafeHandoff(_session, _arrival.AnimalId, "injury", out var failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.UnexpectedCareStatus));
            Assert.That(Snapshot(), Is.SameAs(assessed));
            Request("injury");
            AssertPending();
            Handoff("injury");
            AssertPending();
            Request("other-urgent-sign");
            AssertPending();
            Handoff("other-urgent-sign");
            Assert.That(_station.CanCompleteNecessaryCare(_session, _arrival.AnimalId, out _), Is.True);
            Assert.That(Snapshot().BasicAssessment, Is.EqualTo(StationHealthAssessment.UrgentCareRequired));
            Assert.That(Snapshot().NecessaryProblems.Count, Is.EqualTo(2), "Handoff must not erase health facts.");
            Assert.That(assessed.NecessaryProblems.All(problem =>
                problem.Status == ProfessionalCareStatus.AwaitingConfirmation), Is.True);
            Assert.That(_station.Animals.Count, Is.EqualTo(1));
            Assert.That(_station.AvailableTemporaryPlaces, Is.EqualTo(2), "Handoff is not adoption or discharge.");
        }

        [Test]
        public void RepeatedAssessmentAndHandoffDoNotResetOrCureAnimal()
        {
            Assess("injury");
            Request("injury");
            var pending = Snapshot();
            Assert.That(_station.TryRequestProfessionalCare(_session, _arrival.AnimalId, "injury", out _), Is.False);
            Assert.That(_station.TryCompleteBasicAssessment(_session, _arrival.AnimalId,
                Array.Empty<string>(), out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.BasicAssessmentAlreadyCompleted));
            Assert.That(Snapshot(), Is.SameAs(pending));
            Handoff("injury");
            var handedOver = Snapshot();
            Assert.That(_station.TryRecordSafeHandoff(_session, _arrival.AnimalId, "injury", out _), Is.False);
            Assert.That(_station.TryReceive(_session, _arrival, out _, out _), Is.False);
            Assert.That(Snapshot(), Is.SameAs(handedOver));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" padded ")]
        public void MalformedProblemIdsRejectWholeAssessmentWithoutPartialState(string id)
        {
            var before = Snapshot();
            Assert.That(_station.TryCompleteBasicAssessment(_session, _arrival.AnimalId,
                new[] { "valid-first", id }, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.InvalidCareAssessment));
            Assert.That(Snapshot(), Is.SameAs(before));
            Assess("valid-after-retry");
            Assert.That(Snapshot().NecessaryProblems.Single().Id, Is.EqualTo("valid-after-retry"));
        }

        [Test]
        public void NullAndDuplicateAssessmentsAreRejectedAndCallerCollectionIsCopied()
        {
            var before = Snapshot();
            Assert.That(_station.TryCompleteBasicAssessment(_session, _arrival.AnimalId, null, out _), Is.False);
            Assert.That(_station.TryCompleteBasicAssessment(_session, _arrival.AnimalId,
                new[] { "injury", "injury" }, out _), Is.False);
            Assert.That(Snapshot(), Is.SameAs(before));
            var ids = new List<string> { "injury" };
            Assert.That(_station.TryCompleteBasicAssessment(_session, _arrival.AnimalId, ids, out _), Is.True);
            ids.Clear();
            Assert.That(Snapshot().NecessaryProblems.Single().Id, Is.EqualTo("injury"));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<StationCareProblem>)Snapshot().NecessaryProblems).Clear());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing")]
        public void UnknownHandoffTargetCannotChangeAnyProblem(string id)
        {
            Assess("injury");
            var before = Snapshot();
            Assert.That(_station.TryRequestProfessionalCare(_session, _arrival.AnimalId, id, out var failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.CareProblemNotFound));
            Assert.That(_station.TryRecordSafeHandoff(_session, _arrival.AnimalId, id, out _), Is.False);
            Assert.That(Snapshot(), Is.SameAs(before));
        }

        [Test]
        public void MissingAssessmentAndUnknownAnimalCannotBeBypassed()
        {
            Assert.That(_station.TryRequestProfessionalCare(_session, _arrival.AnimalId, "injury", out var failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.BasicAssessmentRequired));
            Assert.That(_station.TryRecordSafeHandoff(_session, _arrival.AnimalId, "injury", out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.BasicAssessmentRequired));
            var unknown = Guid.NewGuid();
            Assert.That(_station.TryGetCareSnapshot(unknown, out var snapshot), Is.False);
            Assert.That(snapshot, Is.Null);
            Assert.That(_station.TryCompleteBasicAssessment(_session, unknown, Array.Empty<string>(), out failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.AnimalNotFound));
            Assert.That(_station.CanCompleteNecessaryCare(_session, unknown, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.AnimalNotFound));
        }

        [TestCase("wrong-session", StationFailure.WrongSession)]
        [TestCase("paused", StationFailure.Suspended)]
        [TestCase("closed", StationFailure.Closed)]
        public void LifecycleRejectionPreservesLatestCare(string mode, StationFailure expected)
        {
            Assess("injury");
            Request("injury");
            var before = Snapshot();
            Guid session = mode == "wrong-session" ? Guid.NewGuid() : _session;
            if (mode == "paused") _station.SetSuspended(true);
            if (mode == "closed") _station.Close();
            Assert.That(_station.TryRecordSafeHandoff(session, _arrival.AnimalId, "injury", out var failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(_station.TryRequestProfessionalCare(session, _arrival.AnimalId, "injury", out failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(_station.TryCompleteBasicAssessment(session, _arrival.AnimalId,
                Array.Empty<string>(), out failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(_station.CanCompleteNecessaryCare(session, _arrival.AnimalId, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(Snapshot(), Is.SameAs(before));
            _station.SetSuspended(false);
            if (mode != "closed") Handoff("injury");
        }

        [Test]
        public void DifferentAnimalsAndSessionsNeverShareAssessmentProgress()
        {
            var second = new StationAnimalIntake(Guid.NewGuid(), Guid.NewGuid(), "dog",
                new AnimalIntakeCondition(StationHealthAssessment.NoUrgentCareRequired, 0, Array.Empty<string>()));
            Assert.That(_station.TryReceive(_session, second, out _, out _), Is.True);
            Assess();
            Assert.That(_station.CanCompleteNecessaryCare(_session, second.AnimalId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.BasicAssessmentRequired));
            var other = new RescueStation(Guid.NewGuid(), 2);
            Assert.That(other.TryGetCareSnapshot(_arrival.AnimalId, out _), Is.False);
            Assert.That(_station.TryGetAnimal(_arrival.AnimalId, out var same), Is.True);
            Assert.That(same, Is.SameAs(_arrival));
        }

        private StationCareSnapshot Snapshot()
        {
            Assert.That(_station.TryGetCareSnapshot(_arrival.AnimalId, out var snapshot), Is.True);
            return snapshot;
        }

        private void Assess(params string[] ids)
        {
            Assert.That(_station.TryCompleteBasicAssessment(_session, _arrival.AnimalId, ids, out var failure),
                Is.True, failure.ToString());
        }

        private void Request(string id)
        {
            Assert.That(_station.TryRequestProfessionalCare(_session, _arrival.AnimalId, id, out var failure),
                Is.True, failure.ToString());
        }

        private void Handoff(string id)
        {
            Assert.That(_station.TryRecordSafeHandoff(_session, _arrival.AnimalId, id, out var failure),
                Is.True, failure.ToString());
        }

        private void AssertPending()
        {
            Assert.That(_station.CanCompleteNecessaryCare(_session, _arrival.AnimalId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(StationFailure.NecessaryCarePending));
            Assert.That(Snapshot().NecessaryCareIsArranged, Is.False);
        }
    }
}
