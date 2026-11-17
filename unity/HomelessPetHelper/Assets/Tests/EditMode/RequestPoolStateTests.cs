using System;
using HomelessPetHelper.Modules.Outing;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RequestPoolStateTests
    {
        [Test]
        public void NewRequestStartsAvailable()
        {
            var pool = new RequestPoolState();
            var request = CreateRequest();

            Assert.That(pool.TryAdd(request, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(OutingFailure.None));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void AcceptThenResolveReachesTerminalState()
        {
            var pool = new RequestPoolState();
            var request = CreateRequest();
            pool.TryAdd(request, out _);

            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.Accept, out var acceptFailure), Is.True);
            Assert.That(acceptFailure, Is.EqualTo(OutingFailure.None));
            Assert.That(request.State, Is.EqualTo(RequestState.Accepted));

            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.Resolve, out var resolveFailure), Is.True);
            Assert.That(resolveFailure, Is.EqualTo(OutingFailure.None));
            Assert.That(request.State, Is.EqualTo(RequestState.Resolved));
        }

        [Test]
        public void ResolveFromAvailableIsRejectedWithoutMutation()
        {
            var pool = new RequestPoolState();
            var request = CreateRequest();
            pool.TryAdd(request, out _);

            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.Resolve, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.UnexpectedSignal));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void DuplicateAcceptIsRejected()
        {
            var pool = new RequestPoolState();
            var request = CreateRequest();
            pool.TryAdd(request, out _);
            pool.TryTransition(request.RequestId, RequestSignal.Accept, out _);

            Assert.That(pool.TryTransition(request.RequestId, RequestSignal.Accept, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.UnexpectedSignal));
            Assert.That(request.State, Is.EqualTo(RequestState.Accepted));
        }

        [Test]
        public void ResolvedIsTerminalForEverySignal()
        {
            var pool = new RequestPoolState();
            var request = CreateRequest();
            pool.TryAdd(request, out _);
            pool.TryTransition(request.RequestId, RequestSignal.Accept, out _);
            pool.TryTransition(request.RequestId, RequestSignal.Resolve, out _);

            foreach (RequestSignal signal in Enum.GetValues(typeof(RequestSignal)))
            {
                Assert.That(pool.TryTransition(request.RequestId, signal, out var failure), Is.False);
                Assert.That(failure, Is.EqualTo(OutingFailure.UnexpectedSignal));
            }

            Assert.That(request.State, Is.EqualTo(RequestState.Resolved));
        }

        [Test]
        public void UnknownRequestIsRejected()
        {
            var pool = new RequestPoolState();
            Assert.That(pool.TryTransition(Guid.NewGuid(), RequestSignal.Accept, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidRequest));
        }

        [Test]
        public void InvalidSignalIsRejected()
        {
            var pool = new RequestPoolState();
            var request = CreateRequest();
            pool.TryAdd(request, out _);

            Assert.That(pool.TryTransition(request.RequestId, (RequestSignal)99, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.InvalidSignal));
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
        }

        [Test]
        public void DuplicateRequestIdIsRejectedAtomically()
        {
            var pool = new RequestPoolState();
            var first = CreateRequest();
            Assert.That(pool.TryAdd(first, out _), Is.True);

            var second = new HelpRequest(first.RequestId, "tpl", "p", 1);
            Assert.That(pool.TryAdd(second, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingFailure.DuplicateRequestId));
            Assert.That(pool.Requests.Count, Is.EqualTo(1));
        }

        private static HelpRequest CreateRequest()
        {
            return new HelpRequest(Guid.NewGuid(), "help.parking", "parking", 1);
        }
    }
}
