using System;
using System.Collections.Generic;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class CityMapRulesTests
    {
        private const string Parking = "parking";
        private const string Courtyard = "courtyard";
        private const string Porch = "porch";

        [Test]
        public void SnapshotMarksOnlyRequestedOpenPointAsHasHelpRequest()
        {
            var rules = CreateRules(out _, out _, out _);
            var snapshot = rules.BuildSnapshot();

            Assert.That(snapshot.BackgroundId, Is.EqualTo("city_base"));
            Assert.That(snapshot.Points.Count, Is.EqualTo(3));
            Assert.That(Find(snapshot, Parking).DisplayState, Is.EqualTo(PointDisplayState.HasHelpRequest));
            Assert.That(Find(snapshot, Parking).RequestTextKey, Is.EqualTo("request.help.parking"));
            Assert.That(Find(snapshot, Courtyard).DisplayState, Is.EqualTo(PointDisplayState.NoRequest));
            Assert.That(Find(snapshot, Porch).DisplayState, Is.EqualTo(PointDisplayState.NoRequest));
        }

        [Test]
        public void ClosedPointShowsLockedEvenWhenItHasARequest()
        {
            var rules = CreateRules(out var unlock, out _, out _);
            unlock.Close(Parking);

            var snapshot = rules.BuildSnapshot();
            Assert.That(Find(snapshot, Parking).DisplayState, Is.EqualTo(PointDisplayState.Locked));
        }

        [Test]
        public void LockedPointCannotBeSelected()
        {
            var rules = CreateRules(out var unlock, out _, out _);
            unlock.Close(Parking);

            Assert.That(rules.TrySelectPoint(Parking, out var request, out var failure), Is.False);
            Assert.That(request, Is.Null);
            Assert.That(failure, Is.EqualTo(OutingFailure.PointLocked));
        }

        [Test]
        public void UnknownPointIsRejected()
        {
            var rules = CreateRules(out _, out _, out _);
            Assert.That(rules.TrySelectPoint("unknown", out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidPoint));
        }

        [Test]
        public void NoRequestPointIsReportedAsFactWithoutMutatingPool()
        {
            var rules = CreateRules(out _, out var pool, out _);

            Assert.That(rules.TrySelectPoint(Courtyard, out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.PointHasNoRequest));
            Assert.That(FindAvailable(pool, Parking).State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void SelectPointReturnsTheAvailableRequest()
        {
            var rules = CreateRules(out _, out _, out var requestId);

            Assert.That(rules.TrySelectPoint(Parking, out var request, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingFailure.None));
            Assert.That(request.RequestId, Is.EqualTo(requestId));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void SnapshotDoesNotChangeWhenStateChangesAfterwards()
        {
            var rules = CreateRules(out var unlock, out var pool, out _);
            var snapshot = rules.BuildSnapshot();

            unlock.Close(Parking);
            var request = FindAvailable(pool, Parking);
            pool.TryTransition(request.RequestId, RequestSignal.Accept, out _);

            Assert.That(Find(snapshot, Parking).DisplayState, Is.EqualTo(PointDisplayState.HasHelpRequest));
        }

        [TestCase("missing.template")]
        [TestCase("help.parking")]
        public void InvalidTemplateCannotBeDisplayedOrSelected(string templateId)
        {
            var rules = CreateRules(out _, out var pool, out _);
            var invalid = new HelpRequest(Guid.NewGuid(), templateId, Courtyard, 1);
            Assert.That(pool.TryAdd(invalid, out _), Is.True);
            Assert.That(Find(rules.BuildSnapshot(), Courtyard).DisplayState, Is.EqualTo(PointDisplayState.NoRequest));
            Assert.That(rules.TrySelectPoint(Courtyard, out var request, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.PointHasNoRequest));
            Assert.That(request, Is.Null);
            Assert.That(invalid.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void InvalidFirstRequestDoesNotHideLaterValidRequest()
        {
            var definition = new CityMapDefinition("city_base", new[] { "north" }, new[]
            {
                new MapPointDefinition(Parking, "point.parking", "north", true, new[] { "shed" })
            });
            var pool = new RequestPoolState();
            var invalid = new HelpRequest(Guid.NewGuid(), "missing", Parking, 1);
            var valid = new HelpRequest(Guid.NewGuid(), "help.parking", Parking, 1);
            Assert.That(pool.TryAdd(invalid, out _), Is.True);
            Assert.That(pool.TryAdd(valid, out _), Is.True);
            var rules = new CityMapRules(definition, new PointUnlockState(), pool, new[]
            {
                new HelpRequestTemplate("help.parking", "request.help.parking", "cat.shy", new[] { Parking })
            });
            Assert.That(Find(rules.BuildSnapshot(), Parking).RequestTextKey, Is.EqualTo("request.help.parking"));
            Assert.That(rules.TrySelectPoint(Parking, out var request, out _), Is.True);
            Assert.That(request, Is.SameAs(valid));
            Assert.That(invalid.State, Is.EqualTo(RequestState.Available));
        }

        private static CityMapRules CreateRules(out PointUnlockState unlock, out RequestPoolState pool,
            out Guid parkingRequestId)
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

            unlock = new PointUnlockState();
            pool = new RequestPoolState();
            parkingRequestId = Guid.NewGuid();
            var request = new HelpRequest(parkingRequestId, "help.parking", Parking, 1);
            Assert.That(pool.TryAdd(request, out _), Is.True);

            return new CityMapRules(definition, unlock, pool, templates);
        }

        private static MapPointView Find(MapViewSnapshot snapshot, string pointId)
        {
            foreach (var point in snapshot.Points)
            {
                if (point.PointId == pointId)
                {
                    return point;
                }
            }

            throw new AssertionException("Point not found: " + pointId);
        }

        private static HelpRequest FindAvailable(RequestPoolState pool, string pointId)
        {
            foreach (var request in pool.Requests)
            {
                if (request.PointId == pointId)
                {
                    return request;
                }
            }

            throw new AssertionException("Request not found: " + pointId);
        }
    }
}
