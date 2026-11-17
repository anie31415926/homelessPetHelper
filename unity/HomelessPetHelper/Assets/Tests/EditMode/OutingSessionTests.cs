using System;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class OutingSessionTests
    {
        private static readonly OutingTimeMark TimeMark =
            new OutingTimeMark(Guid.NewGuid(), 120);

        [Test]
        public void NewSessionStartsSelectingWithFixedMark()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 42, TimeMark);

            Assert.That(session.SessionId, Is.EqualTo(sessionId));
            Assert.That(session.Seed, Is.EqualTo(42));
            Assert.That(session.Stage, Is.EqualTo(OutingStage.MapSelecting));
            Assert.That(session.TimeMark.EnteredMinutes, Is.EqualTo(120));
            Assert.That(session.HasDeparted, Is.False);
            Assert.That(session.IsSettled, Is.False);
        }

        [Test]
        public void DepartThenSettleCompletesOnce()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 1, TimeMark);
            var requestId = Guid.NewGuid();

            Assert.That(session.TryDepart(sessionId, "parking", requestId, out var departFailure), Is.True);
            Assert.That(departFailure, Is.EqualTo(OutingFailure.None));
            Assert.That(session.Stage, Is.EqualTo(OutingStage.Departed));
            Assert.That(session.SelectedPointId, Is.EqualTo("parking"));
            Assert.That(session.AcceptedRequestId, Is.EqualTo(requestId));

            Assert.That(session.TrySettle(sessionId, out var settleFailure), Is.True);
            Assert.That(settleFailure, Is.EqualTo(OutingFailure.None));
            Assert.That(session.IsSettled, Is.True);
        }

        [Test]
        public void DepartTwiceIsRejectedWithoutOverwrite()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 1, TimeMark);
            var firstRequest = Guid.NewGuid();
            session.TryDepart(sessionId, "parking", firstRequest, out _);

            Assert.That(session.TryDepart(sessionId, "porch", Guid.NewGuid(), out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.AlreadyDeparted));
            Assert.That(session.SelectedPointId, Is.EqualTo("parking"));
            Assert.That(session.AcceptedRequestId, Is.EqualTo(firstRequest));
        }

        [Test]
        public void SettleTwiceIsRejected()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 1, TimeMark);
            session.TryDepart(sessionId, "parking", Guid.NewGuid(), out _);
            session.TrySettle(sessionId, out _);

            Assert.That(session.TrySettle(sessionId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.AlreadySettled));
            Assert.That(session.Stage, Is.EqualTo(OutingStage.Settled));
        }

        [Test]
        public void SettleBeforeDepartIsRejected()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 1, TimeMark);

            Assert.That(session.TrySettle(sessionId, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.NotDeparted));
            Assert.That(session.Stage, Is.EqualTo(OutingStage.MapSelecting));
        }

        [Test]
        public void WrongSessionIsRejectedForBothOperations()
        {
            var session = new OutingSession(Guid.NewGuid(), 1, TimeMark);
            var wrong = Guid.NewGuid();

            Assert.That(session.TryDepart(wrong, "parking", Guid.NewGuid(), out var departFailure), Is.False);
            Assert.That(departFailure, Is.EqualTo(OutingFailure.WrongSession));
            Assert.That(session.TrySettle(wrong, out var settleFailure), Is.False);
            Assert.That(settleFailure, Is.EqualTo(OutingFailure.WrongSession));
            Assert.That(session.Stage, Is.EqualTo(OutingStage.MapSelecting));
        }

        [Test]
        public void SuspendedSessionRejectsDepart()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 1, TimeMark);
            session.SetSuspended(true);

            Assert.That(session.TryDepart(sessionId, "parking", Guid.NewGuid(), out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.Suspended));
            Assert.That(session.HasDeparted, Is.False);
        }

        [Test]
        public void InvalidDepartArgumentsAreRejectedWithoutMutation()
        {
            var sessionId = Guid.NewGuid();
            var session = new OutingSession(sessionId, 1, TimeMark);

            Assert.That(session.TryDepart(sessionId, " ", Guid.NewGuid(), out var pointFailure), Is.False);
            Assert.That(pointFailure, Is.EqualTo(OutingFailure.InvalidPoint));
            Assert.That(session.TryDepart(sessionId, "parking", Guid.Empty, out var requestFailure), Is.False);
            Assert.That(requestFailure, Is.EqualTo(OutingFailure.InvalidRequest));
            Assert.That(session.Stage, Is.EqualTo(OutingStage.MapSelecting));
        }

        [Test]
        public void SameSeedAndInputReproduceSameChosenPoint()
        {
            var sessionId = Guid.NewGuid();
            var first = new OutingSession(sessionId, 20261117, TimeMark);
            var second = new OutingSession(sessionId, 20261117, TimeMark);
            var requestId = Guid.NewGuid();

            Assert.That(first.TryDepart(sessionId, "parking", requestId, out _), Is.True);
            Assert.That(second.TryDepart(sessionId, "parking", requestId, out _), Is.True);
            Assert.That(first.SelectedPointId, Is.EqualTo(second.SelectedPointId));
            Assert.That(first.Seed, Is.EqualTo(second.Seed));
        }

        [Test]
        public void ConstructorRejectsInvalidArguments()
        {
            Assert.Throws<ArgumentException>(() => new OutingSession(Guid.Empty, 1, TimeMark));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new OutingSession(Guid.NewGuid(), -1, TimeMark));
            Assert.Throws<ArgumentException>(() =>
                new OutingSession(Guid.NewGuid(), 1, new OutingTimeMark(Guid.Empty, 5)));
        }
    }
}
