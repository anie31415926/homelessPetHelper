using System;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using NUnit.Framework;
using static HomelessPetHelper.Tests.EditMode.OutingCoordinatorTests;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class OutingCoordinatorRegressionTests
    {
        [TestCase("duplicateAnimal", OutingAssemblyFailure.StationRejected)]
        [TestCase("duplicateOuting", OutingAssemblyFailure.SessionAlreadySettled)]
        [TestCase("full", OutingAssemblyFailure.StationFull)]
        [TestCase("stationPaused", OutingAssemblyFailure.StationRejected)]
        [TestCase("stationClosed", OutingAssemblyFailure.StationRejected)]
        [TestCase("wrongStation", OutingAssemblyFailure.WrongSession)]
        [TestCase("clockPaused", OutingAssemblyFailure.TimeRejected)]
        [TestCase("clockClosed", OutingAssemblyFailure.TimeRejected)]
        [TestCase("staleTime", OutingAssemblyFailure.TimeVersionStale)]
        [TestCase("overflow", OutingAssemblyFailure.TimeRejected)]
        [TestCase("resolvedRequest", OutingAssemblyFailure.RequestNotAvailable)]
        [TestCase("sessionPaused", OutingAssemblyFailure.Suspended)]
        [TestCase("flowPaused", OutingAssemblyFailure.Suspended)]
        public void ReturnRejectionLeavesAllParticipantsUnchanged(string reason, OutingAssemblyFailure expected)
        {
            var f = CreateFixture(out var coordinator, out var requestId, out _, out _, out _,
                initialMinutes: reason == "overflow" ? long.MaxValue - 5 : 0);
            if (reason == "wrongStation")
            {
                coordinator = new OutingCoordinator(f.Flow, f.Station, Guid.NewGuid(), f.Clock,
                    f.WorldSessionId, f.Rules, f.Pool);
            }

            Guid sessionId = RunToRescued(coordinator, f);
            Guid animalId = Guid.NewGuid();
            switch (reason)
            {
                case "duplicateAnimal":
                    Assert.That(f.Station.TryReceive(f.StationSessionId, MakeArrival(Guid.NewGuid(), animalId),
                        out _, out _), Is.True);
                    break;
                case "duplicateOuting":
                    Assert.That(f.Station.TryReceive(f.StationSessionId, MakeArrival(sessionId, Guid.NewGuid()),
                        out _, out _), Is.True);
                    break;
                case "full":
                    for (int i = 0; i < 3; i++)
                    {
                        Assert.That(f.Station.TryReceive(f.StationSessionId,
                            MakeArrival(Guid.NewGuid(), Guid.NewGuid()), out _, out _), Is.True);
                    }
                    break;
                case "stationPaused": f.Station.SetSuspended(true); break;
                case "stationClosed": f.Station.Close(); break;
                case "clockPaused": f.Clock.SetSuspended(true); break;
                case "clockClosed": f.Clock.Close(); break;
                case "staleTime":
                    Assert.That(f.Clock.TryAdvance(f.Clock.Current.Version, 1, out _, out _), Is.True);
                    break;
                case "resolvedRequest":
                    Assert.That(f.Pool.TryTransition(requestId, RequestSignal.Resolve, out _), Is.True);
                    break;
                case "sessionPaused": coordinator.Session.SetSuspended(true); break;
                case "flowPaused": f.Flow.SetSuspended(true); break;
            }

            var animals = f.Station.Animals;
            var time = f.Clock.Current;
            Assert.That(f.Pool.TryGet(requestId, out var request), Is.True);
            var requestState = request.State;
            Assert.That(coordinator.TryReturnAndSettle(sessionId, MakeArrival(sessionId, animalId), 10,
                out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(summary, Is.Null);
            Assert.That(coordinator.LastSummary, Is.Null);
            Assert.That(coordinator.Session.Stage, Is.EqualTo(OutingStage.Departed));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            Assert.That(request.State, Is.EqualTo(requestState));
            Assert.That(f.Station.Animals, Is.SameAs(animals));
            Assert.That(f.Clock.Current, Is.SameAs(time));

            if (reason == "duplicateAnimal" || reason == "overflow")
            {
                Assert.That(coordinator.TryReturnAndSettle(sessionId, MakeArrival(sessionId, Guid.NewGuid()),
                    5, out _, out _), Is.True, "Corrected retry must still be possible.");
                Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(time.ElapsedMinutes + 5));
                Assert.That(f.Station.Animals.Count, Is.EqualTo(animals.Count + 1));
            }
        }

        [Test]
        public void UsedSessionIdsCannotBeReopenedEvenAfterAnotherOuting()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var firstId = RunToRescued(coordinator, f);
            var firstArrival = MakeArrival(firstId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(firstId, firstArrival,
                1, out _, out _), Is.True);
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, firstArrival.AnimalId,
                Array.Empty<string>(), out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(firstId, out _), Is.True);
            Assert.That(coordinator.TryBeginOuting(firstId, 8, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.SessionIdReused));

            Assert.That(f.Pool.TryAdd(new HelpRequest(Guid.NewGuid(), "help.parking", Parking, 1), out _), Is.True);
            var secondId = RunToRescued(coordinator, f);
            var secondArrival = MakeArrival(secondId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(secondId, secondArrival,
                1, out _, out _), Is.True);
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, secondArrival.AnimalId,
                Array.Empty<string>(), out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(secondId, out _), Is.True);
            var summary = coordinator.LastSummary;
            Assert.That(coordinator.TryBeginOuting(firstId, 9, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.SessionIdReused));
            Assert.That(coordinator.Session.SessionId, Is.EqualTo(secondId));
            Assert.That(coordinator.LastSummary, Is.SameAs(summary));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 9, out _), Is.True);
            Assert.That(coordinator.TryAdvanceField(firstId, RescueSignal.ApproachCompleted, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongSession));
        }

        [Test]
        public void ReturnWaitsForExplicitCareCompletionWithoutSettlingTwice()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var id = RunToRescued(coordinator, f);
            Assert.That(coordinator.TryCompleteCare(id, out _), Is.False);
            Assert.That(coordinator.TryReturnAndSettle(id, MakeArrival(id, Guid.NewGuid()),
                10, out var summary, out _), Is.True);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Care));
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 1, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongStage));
            Assert.That(coordinator.TryCompleteCare(Guid.NewGuid(), out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongSession));
            coordinator.Session.SetSuspended(true);
            Assert.That(coordinator.TryCompleteCare(id, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.Suspended));
            coordinator.Session.SetSuspended(false);
            f.Flow.SetSuspended(true);
            Assert.That(coordinator.TryCompleteCare(id, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.Suspended));
            f.Flow.SetSuspended(false);
            f.Station.SetSuspended(true);
            Assert.That(coordinator.TryCompleteCare(id, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.StationRejected));
            f.Station.SetSuspended(false);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Care));
            Assert.That(coordinator.TryCompleteCare(id, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.BasicAssessmentRequired));
            Assert.That(f.Station.TryCompleteBasicAssessment(f.StationSessionId, f.Station.Animals[0].AnimalId,
                Array.Empty<string>(), out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(id, out _), Is.True);
            Assert.That(coordinator.TryCompleteCare(id, out _), Is.False);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(10));
            Assert.That(f.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(coordinator.LastSummary, Is.SameAs(summary));
        }

        [Test]
        public void MismatchedClockIdentityCannotBeginOuting()
        {
            var f = CreateFixture(out _, out _, out _, out _, out _);
            var coordinator = new OutingCoordinator(f.Flow, f.Station, f.StationSessionId, f.Clock,
                Guid.NewGuid(), f.Rules, f.Pool);
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 1, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongSession));
            Assert.That(coordinator.Session, Is.Null);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
        }

        [Test]
        public void DifferentRequestPoolCannotPartiallyAcceptDeparture()
        {
            var f = CreateFixture(out _, out var requestId, out _, out _, out _);
            var otherPool = new RequestPoolState();
            var otherRequest = new HelpRequest(requestId, "help.parking", Parking, 1);
            Assert.That(otherPool.TryAdd(otherRequest, out _), Is.True);
            var coordinator = new OutingCoordinator(f.Flow, f.Station, f.StationSessionId, f.Clock,
                f.WorldSessionId, f.Rules, otherPool);
            var id = Guid.NewGuid();
            Assert.That(coordinator.TryBeginOuting(id, 1, out _), Is.True);
            Assert.That(coordinator.TrySelectAndDepart(id, Parking, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.RequestNotAvailable));
            Assert.That(otherRequest.State, Is.EqualTo(RequestState.Available));
            Assert.That(f.Pool.TryGet(requestId, out var original), Is.True);
            Assert.That(original.State, Is.EqualTo(RequestState.Available));
            Assert.That(coordinator.Session.Stage, Is.EqualTo(OutingStage.MapSelecting));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Map));
        }
    }
}
