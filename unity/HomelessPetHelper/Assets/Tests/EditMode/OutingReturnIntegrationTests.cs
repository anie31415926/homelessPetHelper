using System;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.WorldTime;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class OutingReturnIntegrationTests
    {
        [Test]
        public void MapCancellationIsFreeAndReopeningUsesANewIdentity()
        {
            var f = new Fixture();
            var id = f.Begin();
            Assert.That(f.Coordinator.TryEndUnresolved(id, out var summary, out _), Is.True);
            Assert.That(summary.ElapsedMinutes, Is.Zero);
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(f.Coordinator.Session.HasDeparted, Is.False);
            Assert.That(f.Coordinator.TryBeginOuting(id, 1, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.SessionIdReused));
            f.Begin();
        }

        [TestCase(false, 5L)]
        [TestCase(true, 15L)]
        public void UnresolvedReturnChargesReachedActivityOnceAndReleasesSameRequest(bool opening, long expected)
        {
            var f = new Fixture();
            var id = f.Begin();
            Assert.That(f.Coordinator.TrySelectAndDepart(id, "parking", out _), Is.True);
            if (opening) Assert.That(f.Coordinator.TryStartOpeningRoute(id, out _), Is.True);
            Assert.That(f.Pool.TryTransition(f.Request.RequestId, RequestSignal.HelpedElsewhere, out _), Is.False);
            Assert.That(f.Coordinator.TryEndUnresolved(id, out var summary, out _), Is.True);
            Assert.That(summary.ElapsedMinutes, Is.EqualTo(expected));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(expected));
            Assert.That(f.Request.State, Is.EqualTo(RequestState.Available));
            Assert.That(f.Station.Animals, Is.Empty);
            Assert.That(f.Coordinator.TryEndUnresolved(id, out _, out _), Is.False);
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(expected));
            Assert.That(f.Pool.TryTransition(f.Request.RequestId, RequestSignal.HelpedElsewhere, out _), Is.True);
        }

        [Test]
        public void ExplorationReturnsWithoutFabricatingARequestAnimalOrSuccess()
        {
            var f = new Fixture();
            var id = f.Begin();
            Assert.That(f.Coordinator.TrySelectAndDepart(id, "yard", out _), Is.True);
            Assert.That(f.Coordinator.Session.AcceptedRequestId, Is.EqualTo(Guid.Empty));
            Assert.That(f.Coordinator.TryEndUnresolved(id, out var summary, out _), Is.True);
            Assert.That(summary.OutcomeKey, Is.EqualTo("returned-unresolved"));
            Assert.That(summary.ElapsedMinutes, Is.EqualTo(5));
            Assert.That(f.Request.State, Is.EqualTo(RequestState.Available));
            Assert.That(f.Pool.Requests.Count, Is.EqualTo(2));
            Assert.That(f.Station.Animals, Is.Empty);
        }

        [TestCase("yard")]
        [TestCase("parking")]
        public void LocationVisitAtFullStationChargesTravelWithoutAcceptingHelpRequest(string pointId)
        {
            var fixture = new Fixture(0);
            Guid outingId = fixture.Begin();

            Assert.That(fixture.Coordinator.TryVisitLocation(outingId, pointId, out _), Is.True);
            Assert.That(fixture.Coordinator.Session.SelectedPointId, Is.EqualTo(pointId));
            Assert.That(fixture.Coordinator.Session.AcceptedRequestId, Is.EqualTo(Guid.Empty));
            Assert.That(fixture.Coordinator.Session.DeparturePlan.RequiresStationSpace, Is.False);
            Assert.That(fixture.Request.State, Is.EqualTo(RequestState.Available));
            Assert.That(fixture.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(fixture.Coordinator.TryEndUnresolved(outingId, out var summary, out _), Is.True);
            Assert.That(summary.ElapsedMinutes, Is.EqualTo(5));
            Assert.That(fixture.Coordinator.TryEndUnresolved(outingId, out _, out _), Is.False);
            Assert.That(fixture.Clock.Current.ElapsedMinutes, Is.EqualTo(5));
            Assert.That(fixture.Station.Animals, Is.Empty);
            Assert.That(fixture.Request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void ExplorationCanSettleTwoPlayedActivitiesWithoutClaimingTheRequest()
        {
            var fixture = new Fixture(0);
            Guid outingId = fixture.Begin();
            Assert.That(fixture.Coordinator.TryVisitLocation(outingId, "parking", out _), Is.True);
            Assert.That(fixture.Coordinator.TryEndUnresolved(outingId, out _, out _, 3), Is.False);
            Assert.That(fixture.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(fixture.Coordinator.TryEndUnresolved(outingId, out var summary, out _, 2), Is.True);
            Assert.That(summary.ElapsedMinutes, Is.EqualTo(25));
            Assert.That(fixture.Coordinator.TryEndUnresolved(outingId, out _, out _, 2), Is.False);
            Assert.That(fixture.Clock.Current.ElapsedMinutes, Is.EqualTo(25));
            Assert.That(fixture.Request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void InvalidLocationVisitDoesNotDepartOrAdvanceTime()
        {
            var fixture = new Fixture(0);
            Guid outingId = fixture.Begin();

            Assert.That(fixture.Coordinator.TryVisitLocation(outingId, "missing", out _), Is.False);
            Assert.That(fixture.Coordinator.Session.HasDeparted, Is.False);
            Assert.That(fixture.Flow.Stage, Is.EqualTo(RescueStage.Map));
            Assert.That(fixture.Clock.Current.ElapsedMinutes, Is.Zero);
        }

        [TestCase(false, 20L)]
        [TestCase(true, 30L)]
        public void NegotiationReturnChargesOnlyActivitiesActuallyEntered(bool openedRoute, long expected)
        {
            var f = new Fixture();
            Guid id = f.Begin();
            f.Coordinator.TrySelectAndDepart(id, "parking", out _);
            if (openedRoute) f.Coordinator.TryStartOpeningRoute(id, out _);
            Assert.That(f.Coordinator.TryAdvanceField(id, RescueSignal.ApproachCompleted, out _), Is.True);
            Assert.That(f.Coordinator.TryEndUnresolved(id, out var summary, out _), Is.True);
            Assert.That(summary.ElapsedMinutes, Is.EqualTo(expected));
        }

        [Test]
        public void InvalidProgressRejectsFieldAdvanceBeforeMutatingFlow()
        {
            var f = new Fixture();
            Guid id = f.Begin();
            f.Coordinator.TrySelectAndDepart(id, "parking", out _);
            f.Coordinator.Session.TryRecordProgress(id, OutingReturnProgress.Negotiating, out _);
            Assert.That(f.Coordinator.TryAdvanceField(id, RescueSignal.ApproachCompleted, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongStage));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Approach));
        }

        [Test]
        public void FullStationAllowsOnlyConfirmedReunionAtActualDeparture()
        {
            var f = new Fixture(0);
            var id = f.Begin();
            Assert.That(f.Coordinator.TrySelectAndDepart(id, "parking", out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.StationFull));
            Assert.That(f.Coordinator.TrySelectAndDepart(id, "yard", out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.StationFull));
            Assert.That(f.Coordinator.TrySelectAndDepart(id, "porch", out _), Is.True);
            Assert.That(f.Coordinator.Session.DeparturePlan.RequiresStationSpace, Is.False);
            Assert.That(f.Coordinator.TryEndUnresolved(id, out _, out _), Is.True);
            Assert.That(f.Reunion.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void ReunionCannotBeSettledAsOrdinaryIntakeOrDiscardedAfterRescue()
        {
            var f = new Fixture();
            Guid id = f.Begin();
            f.Coordinator.TrySelectAndDepart(id, "porch", out _);
            f.Coordinator.TryAdvanceField(id, RescueSignal.ApproachCompleted, out _);
            f.Coordinator.TryAdvanceField(id, RescueSignal.NegotiationCompleted, out _);
            var arrival = OutingCoordinatorTests.MakeArrival(id, Guid.NewGuid());
            Assert.That(f.Coordinator.TryReturnAndSettle(id, arrival, 35, out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.DirectHandoffRequired));
            Assert.That(summary, Is.Null);
            Assert.That(f.Coordinator.TryEndUnresolved(id, out _, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongStage));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            Assert.That(f.Coordinator.Session.IsSettled, Is.False);
            Assert.That(f.Reunion.State, Is.EqualTo(RequestState.Accepted));
            Assert.That(f.Station.Animals, Is.Empty);
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.Zero);
        }

        [Test]
        public void CapacityIsRecheckedAfterViewingTheMap()
        {
            var f = new Fixture(1);
            var id = f.Begin();
            f.Station.TryReceive(f.StationId,
                OutingCoordinatorTests.MakeArrival(Guid.NewGuid(), Guid.NewGuid()), out _, out _);
            Assert.That(f.Coordinator.TrySelectAndDepart(id, "parking", out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.StationFull));
            Assert.That(f.Request.State, Is.EqualTo(RequestState.Available));
            Assert.That(f.Coordinator.Session.HasDeparted, Is.False);
        }

        [TestCase("paused", OutingAssemblyFailure.Suspended)]
        [TestCase("stale", OutingAssemblyFailure.TimeVersionStale)]
        [TestCase("overflow", OutingAssemblyFailure.TimeRejected)]
        [TestCase("closed", OutingAssemblyFailure.TimeRejected)]
        [TestCase("request", OutingAssemblyFailure.RequestNotAvailable)]
        [TestCase("wrong", OutingAssemblyFailure.WrongSession)]
        public void RefusedReturnLeavesAllParticipantsUnchanged(string cause, OutingAssemblyFailure expected)
        {
            var f = new Fixture(initialMinutes: cause == "overflow" ? long.MaxValue - 1 : 0);
            var id = f.Begin();
            f.Coordinator.TrySelectAndDepart(id, "parking", out _);
            if (cause == "paused") f.Coordinator.Session.SetSuspended(true);
            if (cause == "closed") f.Clock.Close();
            if (cause == "stale") f.Clock.TryAdvance(f.Clock.Current.Version, 1, out _, out _);
            if (cause == "request") f.Pool.TryTransition(f.Request.RequestId, RequestSignal.Resolve, out _);
            var before = f.Clock.Current;
            var state = f.Request.State;
            Assert.That(f.Coordinator.TryEndUnresolved(cause == "wrong" ? Guid.NewGuid() : id,
                out var summary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(summary, Is.Null);
            Assert.That(f.Clock.Current, Is.SameAs(before));
            Assert.That(f.Request.State, Is.EqualTo(state));
            Assert.That(f.Coordinator.Session.Stage, Is.EqualTo(OutingStage.Departed));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Approach));
        }

        [Test]
        public void PausingDoesNotReleaseRequestOrCommitAnyTime()
        {
            var f = new Fixture();
            var id = f.Begin();
            f.Coordinator.TrySelectAndDepart(id, "parking", out _);
            f.Coordinator.Session.SetSuspended(true);
            Assert.That(f.Request.State, Is.EqualTo(RequestState.Accepted));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.Zero);
            f.Coordinator.Session.SetSuspended(false);
            Assert.That(f.Coordinator.TryEndUnresolved(id, out _, out _), Is.True);
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(5));
        }

        private sealed class Fixture
        {
            internal readonly Guid StationId = Guid.NewGuid();
            internal readonly RescueStation Station;
            internal readonly RescueFlow Flow = new RescueFlow();
            internal readonly ActionWorldClock Clock;
            internal readonly RequestPoolState Pool = new RequestPoolState();
            internal readonly HelpRequest Request = new HelpRequest(Guid.NewGuid(), "help", "parking", 1);
            internal readonly HelpRequest Reunion;
            internal readonly OutingCoordinator Coordinator;

            internal Fixture(int capacity = 2, long initialMinutes = 0)
            {
                var worldId = Guid.NewGuid();
                Clock = new ActionWorldClock(worldId, 1440, initialMinutes);
                Station = new RescueStation(StationId, capacity);
                var reunionId = Guid.NewGuid();
                Reunion = new HelpRequest(reunionId, "pet", "porch", 1,
                    new OwnerReunionPlan(reunionId, "owner", "safe-receiving"));
                Pool.TryAdd(Request, out _);
                Pool.TryAdd(Reunion, out _);
                var definition = new CityMapDefinition("map", new[] { "nearby" }, new[]
                {
                    new MapPointDefinition("parking", "parking", "nearby", true, new[] { "shed" }),
                    new MapPointDefinition("yard", "yard", "nearby", true, new[] { "yard" }),
                    new MapPointDefinition("porch", "porch", "nearby", true, new[] { "porch" })
                });
                var rules = new CityMapRules(definition, new PointUnlockState(), Pool, new[]
                {
                    new HelpRequestTemplate("help", "help", "cat", new[] { "parking" }),
                    new HelpRequestTemplate("pet", "pet", "dog", new[] { "porch" })
                });
                Coordinator = new OutingCoordinator(Flow, Station, StationId, Clock, worldId, rules, Pool,
                    new OutingTimePolicy(5, 10, 15, 5));
            }

            internal Guid Begin()
            {
                var id = Guid.NewGuid();
                Assert.That(Coordinator.TryBeginOuting(id, 1, out _), Is.True);
                return id;
            }
        }
    }
}
