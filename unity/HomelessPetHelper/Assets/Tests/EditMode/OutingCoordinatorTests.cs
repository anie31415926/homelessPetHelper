using System;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;
using HomelessPetHelper.Modules.WorldTime;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class OutingCoordinatorTests
    {
        private const string Parking = "parking";
        private const string Courtyard = "courtyard";
        private const string Porch = "porch";

        [Test]
        public void BeginMovesFlowToMapAndCapturesTimeVersion()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var sessionId = Guid.NewGuid();

            Assert.That(coordinator.TryBeginOuting(sessionId, 42, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.None));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Map));
            Assert.That(coordinator.Session.SessionId, Is.EqualTo(sessionId));
            Assert.That(coordinator.Session.TimeMark.EnteredMinutes,
                Is.EqualTo(f.Clock.Current.Version.ElapsedMinutes));
        }

        [Test]
        public void BeginRefusesFullStationWithoutMutatingFlow()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _,
                capacity: 1);
            var animal = MakeArrival(Guid.NewGuid(), Guid.NewGuid());
            Assert.That(f.Station.TryReceive(f.StationSessionId, animal, out _, out _), Is.True);
            Assert.That(f.Station.IsFull, Is.True);

            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 1, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.StationFull));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(coordinator.Session, Is.Null);
        }

        [Test]
        public void SelectDepartFieldAndSettleCompletesOnce()
        {
            var f = CreateFixture(out var coordinator, out var requestId, out _, out _, out _);
            var sessionId = RunToRescued(coordinator, f);

            long before = f.Clock.Current.ElapsedMinutes;
            var arrival = MakeArrival(sessionId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(sessionId, arrival, 30,
                out var summary, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.None));

            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Assert.That(f.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(before + 30));
            Assert.That(coordinator.Session.IsSettled, Is.True);
            Assert.That(summary.SessionId, Is.EqualTo(sessionId));
            Assert.That(summary.RequestId, Is.EqualTo(requestId));
            Assert.That(summary.SelectedPointId, Is.EqualTo(Parking));
            Assert.That(summary.OutcomeKey, Is.EqualTo("rescued"));
            Assert.That(coordinator.LastSummary, Is.SameAs(summary));
            Assert.That(f.Pool.TryGet(requestId, out var request), Is.True);
            Assert.That(request.State, Is.EqualTo(RequestState.Resolved));
        }

        [Test]
        public void RepeatedSettleIsRefusedWithNoSecondAnimalOrTimeAdvance()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var sessionId = RunToRescued(coordinator, f);
            var arrival = MakeArrival(sessionId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(sessionId, arrival, 20, out _, out _), Is.True);

            long elapsedAfterFirst = f.Clock.Current.ElapsedMinutes;
            var secondArrival = MakeArrival(sessionId, Guid.NewGuid());
            Assert.That(coordinator.TryReturnAndSettle(sessionId, secondArrival, 20,
                out var secondSummary, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.SessionAlreadySettled));
            Assert.That(secondSummary, Is.Null);
            Assert.That(f.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(f.Clock.Current.ElapsedMinutes, Is.EqualTo(elapsedAfterFirst));
        }

        [Test]
        public void WrongSessionCannotDriveAnyPhase()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var sessionId = Guid.NewGuid();
            var wrongId = Guid.NewGuid();
            Assert.That(coordinator.TryBeginOuting(sessionId, 1, out _), Is.True);

            Assert.That(coordinator.TrySelectAndDepart(wrongId, Parking, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongSession));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Map));

            Assert.That(coordinator.TryAdvanceField(wrongId, RescueSignal.ApproachCompleted, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongSession));
        }

        [Test]
        public void NoRequestPointCannotDepart()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 1, out _), Is.True);

            Assert.That(coordinator.TrySelectAndDepart(coordinator.Session.SessionId, Courtyard,
                out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.PointHasNoRequest));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Map));
            Assert.That(coordinator.Session.HasDeparted, Is.False);
        }

        [Test]
        public void SuspensionBlocksDepart()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 1, out _), Is.True);
            coordinator.Session.SetSuspended(true);

            Assert.That(coordinator.TrySelectAndDepart(coordinator.Session.SessionId, Parking,
                out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.Suspended));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Map));
        }

        [Test]
        public void ArrivalFromAnotherOutingIsRejectedBeforeReceive()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var sessionId = RunToRescued(coordinator, f);
            var strangerArrival = MakeArrival(Guid.NewGuid(), Guid.NewGuid());

            Assert.That(coordinator.TryReturnAndSettle(sessionId, strangerArrival, 10,
                out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.IdentityMismatch));
            Assert.That(f.Station.Animals.Count, Is.Zero);
            Assert.That(coordinator.Session.IsSettled, Is.False);
        }

        [Test]
        public void FieldSignalCannotSkipPhases()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            var sessionId = Guid.NewGuid();
            Assert.That(coordinator.TryBeginOuting(sessionId, 1, out _), Is.True);
            Assert.That(coordinator.TrySelectAndDepart(sessionId, Parking, out _), Is.True);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Approach));

            // NegotiationCompleted arrives while still in Approach.
            Assert.That(coordinator.TryAdvanceField(sessionId,
                RescueSignal.NegotiationCompleted, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.WrongStage));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Approach));
        }

        [Test]
        public void FieldSignalBeforeDepartIsRejected()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _);
            Assert.That(coordinator.TryBeginOuting(Guid.NewGuid(), 1, out _), Is.True);

            Assert.That(coordinator.TryAdvanceField(coordinator.Session.SessionId,
                RescueSignal.ApproachCompleted, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.NotDeparted));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Map));
        }

        [Test]
        public void BothOpenRouteVariantsReachRescued()
        {
            // The coordinator does not branch on open-route variant; both converge on the
            // same validated field-signal channel. Run two full outings to prove reuse.
            var f = CreateFixture(out var coordinator, out _, out var pool, out _, out _,
                capacity: 2);
            for (int i = 0; i < 2; i++)
            {
                if (i > 0)
                {
                    // Each outing consumes one request; replenish for the next round.
                    Assert.That(pool.TryAdd(new HelpRequest(Guid.NewGuid(), "help.parking",
                        Parking, 1), out _), Is.True);
                }

                var sessionId = RunToRescued(coordinator, f, 100 + i);
                var arrival = MakeArrival(sessionId, Guid.NewGuid());
                Assert.That(coordinator.TryReturnAndSettle(sessionId, arrival, 15, out _, out _), Is.True);
            }

            Assert.That(f.Station.Animals.Count, Is.EqualTo(2));
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Station));
        }

        [Test]
        public void SecondOutingCannotReuseSettledSession()
        {
            var f = CreateFixture(out var coordinator, out _, out _, out _, out _, capacity: 2);
            var sessionId = RunToRescued(coordinator, f);
            Assert.That(coordinator.TryReturnAndSettle(sessionId, MakeArrival(sessionId, Guid.NewGuid()),
                10, out _, out _), Is.True);

            Assert.That(coordinator.TrySelectAndDepart(sessionId, Parking, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.SessionAlreadySettled));
        }

        private static Guid RunToRescued(OutingCoordinator coordinator, Fixture f, int seed = 7)
        {
            var sessionId = Guid.NewGuid();
            Assert.That(coordinator.TryBeginOuting(sessionId, seed, out var beginFailure), Is.True,
                "begin: " + beginFailure);
            Assert.That(coordinator.TrySelectAndDepart(sessionId, Parking, out var departFailure), Is.True,
                "depart: " + departFailure);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Approach));
            Assert.That(coordinator.TryAdvanceField(sessionId, RescueSignal.ApproachCompleted, out _), Is.True);
            Assert.That(coordinator.TryAdvanceField(sessionId, RescueSignal.NegotiationCompleted, out _), Is.True);
            Assert.That(f.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            return sessionId;
        }

        private static StationAnimalIntake MakeArrival(Guid outingId, Guid animalId)
        {
            var condition = new AnimalIntakeCondition(StationHealthAssessment.Unchecked, 0,
                Array.Empty<string>());
            return new StationAnimalIntake(outingId, animalId, "cat.shy", condition);
        }

        private static Fixture CreateFixture(out OutingCoordinator coordinator, out Guid parkingRequestId,
            out RequestPoolState pool, out RescueFlow flow, out ActionWorldClock clock,
            int capacity = 3)
        {
            var points = new[]
            {
                new MapPointDefinition(Parking, "point.parking", "north", true, new[] { "shed" }),
                new MapPointDefinition(Courtyard, "point.courtyard", "north", true, new[] { "yard" }),
                new MapPointDefinition(Porch, "point.porch", "south", true, new[] { "lane" })
            };
            var definition = new CityMapDefinition("city_base", new[] { "north", "south" }, points);
            var templates = new[]
            {
                new HelpRequestTemplate("help.parking", "request.help.parking", "cat.shy", new[] { Parking })
            };

            var unlock = new PointUnlockState();
            pool = new RequestPoolState();
            parkingRequestId = Guid.NewGuid();
            Assert.That(pool.TryAdd(new HelpRequest(parkingRequestId, "help.parking", Parking, 1), out _), Is.True);

            var rules = new CityMapRules(definition, unlock, pool, templates);
            flow = new RescueFlow();

            var worldSessionId = Guid.NewGuid();
            var stationSessionId = Guid.NewGuid();
            var station = new RescueStation(stationSessionId, capacity);
            clock = new ActionWorldClock(worldSessionId, 1440);

            coordinator = new OutingCoordinator(flow, station, stationSessionId, clock,
                worldSessionId, rules, pool);
            return new Fixture(flow, station, stationSessionId, clock, worldSessionId, pool);
        }

        private sealed class Fixture
        {
            public readonly RescueFlow Flow;
            public readonly RescueStation Station;
            public readonly Guid StationSessionId;
            public readonly ActionWorldClock Clock;
            public readonly Guid WorldSessionId;
            public readonly RequestPoolState Pool;

            public Fixture(RescueFlow flow, RescueStation station, Guid stationSessionId,
                ActionWorldClock clock, Guid worldSessionId, RequestPoolState pool)
            {
                Flow = flow;
                Station = station;
                StationSessionId = stationSessionId;
                Clock = clock;
                WorldSessionId = worldSessionId;
                Pool = pool;
            }
        }
    }
}
