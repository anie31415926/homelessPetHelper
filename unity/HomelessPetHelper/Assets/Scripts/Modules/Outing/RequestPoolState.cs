using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Owns the request set and is the single transition entry point for request state.
    /// Accepted requests are protected from external rescue until the caller explicitly releases
    /// them after an unresolved return. Supply, timing and outing identity checks belong to the
    /// caller; this pool never advances time, fabricates success or pays rewards.
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

        /// <summary>
        /// Reconstructs detached stable requests; active Accepted requests need an outing checkpoint.
        /// </summary>
        public static RequestPoolState Restore(IReadOnlyList<HelpRequest> requests, IReadOnlyList<RequestState> states)
        {
            if (requests == null || states == null || requests.Count != states.Count)
                throw new ArgumentException("Request state sizes differ.");
            var pool = new RequestPoolState();
            for (int i = 0; i < requests.Count; i++)
            {
                if (states[i] != RequestState.Available && states[i] != RequestState.Resolved &&
                    states[i] != RequestState.HelpedElsewhere)
                    throw new ArgumentException("Not a stable request state.");
                var source = requests[i] ?? throw new ArgumentException("Missing request.");
                var request = new HelpRequest(source.RequestId, source.TemplateId, source.PointId,
                    source.TemplateVersion, source.ReunionPlan) { State = states[i] };
                if (!pool.TryAdd(request, out _)) throw new ArgumentException("Duplicate request identity.");
            }
            return pool;
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
            if (!CanTransition(requestId, signal, out failure))
            {
                return false;
            }

            var request = _requestsById[requestId];
            TryGetNextState(request.State, signal, out var nextState);
            request.State = nextState;
            return true;
        }

        /// <summary>Read-only transition gate; shares the mutation entry's complete validation.</summary>
        public bool CanTransition(Guid requestId, RequestSignal signal, out OutingFailure failure)
        {
            if (signal != RequestSignal.Accept && signal != RequestSignal.Resolve &&
                signal != RequestSignal.ReleaseAfterReturn && signal != RequestSignal.HelpedElsewhere)
            {
                failure = OutingFailure.InvalidSignal;
                return false;
            }

            if (!_requestsById.TryGetValue(requestId, out var request))
            {
                failure = OutingFailure.InvalidRequest;
                return false;
            }

            if (!TryGetNextState(request.State, signal, out _))
            {
                failure = OutingFailure.UnexpectedSignal;
                return false;
            }

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
                case RequestState.Accepted when signal == RequestSignal.ReleaseAfterReturn:
                    nextState = RequestState.Available;
                    return true;
                case RequestState.Available when signal == RequestSignal.HelpedElsewhere:
                    nextState = RequestState.HelpedElsewhere;
                    return true;
                default:
                    return false;
            }
        }
    }
}
