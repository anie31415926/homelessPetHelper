using System;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RequestReturnLifecycleTests
    {
        [Test]
        public void OtherRescuerCanCloseAnAvailableRequestButCannotCloseAcceptedOne()
        {
            var pool = CreatePool(out var request);
            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.Accept, out _), Is.True);
            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.HelpedElsewhere,
                out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.UnexpectedSignal));
            Assert.That(request.State, Is.EqualTo(RequestState.Accepted));

            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.ReleaseAfterReturn, out _), Is.True);
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.HelpedElsewhere, out _), Is.True);
            Assert.That(request.State, Is.EqualTo(RequestState.HelpedElsewhere));
            Assert.That(request.State, Is.Not.EqualTo(RequestState.Resolved));
        }

        [Test]
        public void UnresolvedReturnKeepsTheSameRequestAndDoesNotForceItClosed()
        {
            var pool = CreatePool(out var request);
            var id = request.RequestId;
            pool.TryTransition(id, RequestSignal.Accept, out _);
            Assert.That(pool.CanTransition(id, RequestSignal.ReleaseAfterReturn, out _), Is.True);
            Assert.That(request.State, Is.EqualTo(RequestState.Accepted));
            Assert.That(pool.TryTransition(id, RequestSignal.ReleaseAfterReturn, out _), Is.True);
            Assert.That(pool.TryTransition(id, RequestSignal.ReleaseAfterReturn, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.UnexpectedSignal));
            Assert.That(pool.TryGet(id, out var same), Is.True);
            Assert.That(same, Is.SameAs(request));
            Assert.That(same.State, Is.EqualTo(RequestState.Available));
            Assert.That(pool.Requests.Count, Is.EqualTo(1));
            Assert.That(pool.TryTransition(id, RequestSignal.Accept, out _), Is.True);
        }

        [Test]
        public void HelpedElsewhereIsTerminalForAllSignals()
        {
            var pool = CreatePool(out var request);
            pool.TryTransition(request.RequestId, RequestSignal.HelpedElsewhere, out _);
            foreach (RequestSignal signal in Enum.GetValues(typeof(RequestSignal)))
            {
                Assert.That(pool.TryTransition(request.RequestId, signal, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(OutingFailure.UnexpectedSignal));
                Assert.That(request.State, Is.EqualTo(RequestState.HelpedElsewhere));
            }
        }

        [TestCase(RequestSignal.ReleaseAfterReturn)]
        [TestCase(RequestSignal.HelpedElsewhere)]
        public void NewSignalsRejectUnknownRequestsWithoutChangingOtherRequests(RequestSignal signal)
        {
            var pool = CreatePool(out var request);
            Assert.That(pool.TryTransition(Guid.NewGuid(), signal, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidRequest));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
        }

        private static RequestPoolState CreatePool(out HelpRequest request)
        {
            var pool = new RequestPoolState();
            request = new HelpRequest(Guid.NewGuid(), "help", "parking", 1);
            Assert.That(pool.TryAdd(request, out _), Is.True);
            return pool;
        }
    }
}
