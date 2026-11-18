using System;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class CityMapDepartureTests
    {
        [TestCase(0, false)]
        [TestCase(1, true)]
        public void OrdinaryRequestNeedsSpaceAndPlanningDoesNotAccept(int spaces, bool allowed)
        {
            var rules = CreateRules(out var pool, out _);
            var request = AddRequest(pool);

            Assert.That(rules.TryPlanDeparture("parking", spaces, out var plan, out var failure), Is.EqualTo(allowed));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
            Assert.That(pool.Requests.Count, Is.EqualTo(1));
            Assert.That(failure, Is.EqualTo(allowed ? OutingFailure.None : OutingFailure.CapacityNotAvailable));
            if (allowed)
            {
                Assert.That(plan.EntryKind, Is.EqualTo(MapEntryKind.HelpRequest));
                Assert.That(plan.RequiresStationSpace, Is.True);
                Assert.That(plan.RequestId, Is.EqualTo(request.RequestId));
            }
            else
            {
                Assert.That(plan, Is.Null);
            }
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        public void ExplorationNeedsSpaceButNotARequestOrGuaranteedAnimal(int spaces, bool allowed)
        {
            var rules = CreateRules(out var pool, out _);
            Assert.That(rules.TryPlanDeparture("yard", spaces, out var plan, out var failure), Is.EqualTo(allowed));
            Assert.That(pool.Requests, Is.Empty);
            Assert.That(failure, Is.EqualTo(allowed ? OutingFailure.None : OutingFailure.CapacityNotAvailable));
            if (allowed)
            {
                Assert.That(plan.EntryKind, Is.EqualTo(MapEntryKind.Exploration));
                Assert.That(plan.RequestId, Is.EqualTo(Guid.Empty));
                Assert.That(plan.RequiresStationSpace, Is.True);
                Assert.That(plan.ReunionPlan, Is.Null);
            }
            else
            {
                Assert.That(plan, Is.Null);
            }
        }

        [Test]
        public void ConfirmedDirectReunionCanBePlannedWhenStationIsFull()
        {
            var rules = CreateRules(out var pool, out _);
            var id = Guid.NewGuid();
            var arrangement = new OwnerReunionPlan(id, "owner.1", "handoff.1");
            var request = new HelpRequest(id, "help", "parking", 1, arrangement);
            Assert.That(pool.TryAdd(request, out _), Is.True);

            Assert.That(rules.TryPlanDeparture("parking", 0, out var plan, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingFailure.None));
            Assert.That(plan.EntryKind, Is.EqualTo(MapEntryKind.OwnerReunion));
            Assert.That(plan.RequiresStationSpace, Is.False);
            Assert.That(plan.ReunionPlan, Is.SameAs(arrangement));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void PetSearchTextWithoutArrangementDoesNotBypassCapacity()
        {
            var rules = CreateRules(out var pool, out _);
            var request = new HelpRequest(Guid.NewGuid(), "pet.search", "parking", 1);
            Assert.That(pool.TryAdd(request, out _), Is.True);

            Assert.That(rules.TryPlanDeparture("parking", 0, out var plan, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.CapacityNotAvailable));
            Assert.That(plan, Is.Null);
        }

        [Test]
        public void AcceptedRequestCannotBeBypassedByExplorationOrReunion()
        {
            var rules = CreateRules(out var pool, out _);
            var request = AddRequest(pool);
            Assert.That(rules.TryPlanDeparture("parking", 1, out var oldPlan, out _), Is.True);
            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.Accept, out _), Is.True);

            Assert.That(rules.TryPlanDeparture("parking", 1, out var newPlan, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.RequestNotAvailable));
            Assert.That(newPlan, Is.Null);
            Assert.That(oldPlan.RequestId, Is.EqualTo(request.RequestId));
            Assert.That(oldPlan.EntryKind, Is.EqualTo(MapEntryKind.HelpRequest));
            Assert.That(request.State, Is.EqualTo(RequestState.Accepted));
        }

        [Test]
        public void PlanIsNotACapacityReservationAndCanBeRechecked()
        {
            var rules = CreateRules(out _, out _);
            Assert.That(rules.TryPlanDeparture("yard", 1, out var oldPlan, out _), Is.True);
            Assert.That(rules.TryPlanDeparture("yard", 0, out var plan, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.CapacityNotAvailable));
            Assert.That(plan, Is.Null);
            Assert.That(oldPlan.RequiresStationSpace, Is.True);
        }

        [TestCase("missing", OutingFailure.InvalidPoint)]
        [TestCase(null, OutingFailure.InvalidPoint)]
        [TestCase("closed", OutingFailure.PointLocked)]
        public void InvalidAndClosedPointsAreRefused(string pointId, OutingFailure expected)
        {
            var rules = CreateRules(out var pool, out var unlock);
            unlock.Close("closed");
            Assert.That(rules.TryPlanDeparture(pointId, 1, out var plan, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(plan, Is.Null);
            Assert.That(pool.Requests, Is.Empty);
        }

        [Test]
        public void NegativeCapacityIsNotTreatedAsAnExemption()
        {
            var rules = CreateRules(out var pool, out _);
            var id = Guid.NewGuid();
            pool.TryAdd(new HelpRequest(id, "help", "parking", 1,
                new OwnerReunionPlan(id, "owner", "receiving")), out _);

            Assert.That(rules.TryPlanDeparture("parking", -1, out var plan, out var failure), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidCapacity));
        }

        [TestCase("missing")]
        [TestCase("yard.only")]
        public void InvalidTemplateCannotGrantReunionExemption(string templateId)
        {
            var rules = CreateRules(out var pool, out _);
            var id = Guid.NewGuid();
            pool.TryAdd(new HelpRequest(id, templateId, "parking", 1,
                new OwnerReunionPlan(id, "owner", "receiving")), out _);

            Assert.That(rules.TryPlanDeparture("parking", 0, out var plan, out var failure), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(failure, Is.EqualTo(OutingFailure.CapacityNotAvailable));
            Assert.That(rules.TryPlanDeparture("parking", 1, out plan, out _), Is.True);
            Assert.That(plan.EntryKind, Is.EqualTo(MapEntryKind.Exploration));
        }

        [Test]
        public void AnotherRequestsReunionArrangementIsRejected()
        {
            var arrangement = new OwnerReunionPlan(Guid.NewGuid(), "owner", "receiving");
            Assert.Throws<ArgumentException>(() =>
                new HelpRequest(Guid.NewGuid(), "help", "parking", 1, arrangement));
        }

        [TestCase(null, "receiving")]
        [TestCase("", "receiving")]
        [TestCase(" owner", "receiving")]
        [TestCase("owner", null)]
        [TestCase("owner", "")]
        [TestCase("owner", "receiving ")]
        public void IncompleteArrangementIsRejected(string ownerId, string receivingId)
        {
            Assert.Throws<ArgumentException>(() =>
                new OwnerReunionPlan(Guid.NewGuid(), ownerId, receivingId));
        }

        private static HelpRequest AddRequest(RequestPoolState pool)
        {
            var request = new HelpRequest(Guid.NewGuid(), "help", "parking", 1);
            Assert.That(pool.TryAdd(request, out _), Is.True);
            return request;
        }

        private static CityMapRules CreateRules(out RequestPoolState pool, out PointUnlockState unlock)
        {
            var definition = new CityMapDefinition("city", new[] { "north" }, new[]
            {
                new MapPointDefinition("parking", "point.parking", "north", true, new[] { "shed" }),
                new MapPointDefinition("yard", "point.yard", "north", true, new[] { "yard" }),
                new MapPointDefinition("closed", "point.closed", "north", true, new[] { "porch" })
            });
            pool = new RequestPoolState();
            unlock = new PointUnlockState();
            return new CityMapRules(definition, unlock, pool, new[]
            {
                new HelpRequestTemplate("help", "request.help", "animal", new[] { "parking" }),
                new HelpRequestTemplate("pet.search", "request.pet.search", "pet", new[] { "parking" }),
                new HelpRequestTemplate("yard.only", "request.yard", "animal", new[] { "yard" })
            });
        }
    }
}
