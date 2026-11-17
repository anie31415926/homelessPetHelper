using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Owns the request set and is the single transition entry point for request state.
    /// Only the confirmed lifecycle (Available -Accept-> Accepted -Resolve-> Resolved) exists.
    /// Defer / expire / cancel are unconfirmed and have no implicit implementation.
    /// Supply and capacity matching are decided by the caller, not here.
    /// </summary>
    public sealed class RequestPoolState
    {
        private readonly Dictionary<Guid, HelpRequest> _requestsById = new Dictionary<Guid, HelpRequest>();
        private readonly List<HelpRequest> _requests = new List<HelpRequest>();
        private ReadOnlyCollection<HelpRequest> _view = new ReadOnlyCollection<HelpRequest>(new List<HelpRequest>());

        public IReadOnlyList<HelpRequest> Requests => _view;

        /// <summary>
        /// Adds a fresh request to the pool. Duplicate IDs are rejected atomically.
        /// </summary>
        public bool TryAdd(HelpRequest request, out OutingFailure failure)
        {
            if (request == null)
            {
                failure = OutingFailure.InvalidRequest;
                return false;
            }

            if (_requestsById.ContainsKey(request.RequestId))
            {
                failure = OutingFailure.DuplicateRequestId;
                return false;
            }

            _requestsById.Add(request.RequestId, request);
            _requests.Add(request);
            _view = _requests.AsReadOnly();
            failure = OutingFailure.None;
            return true;
        }

        public bool TryGet(Guid requestId, out HelpRequest request)
        {
            return _requestsById.TryGetValue(requestId, out request);
        }

        /// <summary>
        /// Single mutation entry: a legal signal changes exactly one request's state;
        /// an illegal or repeated signal is rejected with no partial mutation.
        /// </summary>
        public bool TryTransition(Guid requestId, RequestSignal signal, out OutingFailure failure)
        {
            if (signal != RequestSignal.Accept && signal != RequestSignal.Resolve)
            {
                failure = OutingFailure.InvalidSignal;
                return false;
            }

            if (!_requestsById.TryGetValue(requestId, out var request))
            {
                failure = OutingFailure.InvalidRequest;
                return false;
            }

            if (!TryGetNextState(request.State, signal, out var nextState))
            {
                failure = OutingFailure.UnexpectedSignal;
                return false;
            }

            request.State = nextState;
            failure = OutingFailure.None;
            return true;
        }

        private static bool TryGetNextState(RequestState state, RequestSignal signal, out RequestState nextState)
        {
            nextState = state;
            switch (state)
            {
                case RequestState.Available when signal == RequestSignal.Accept:
                    nextState = RequestState.Accepted;
                    return true;
                case RequestState.Accepted when signal == RequestSignal.Resolve:
                    nextState = RequestState.Resolved;
                    return true;
                default:
                    return false;
            }
        }
    }
}
