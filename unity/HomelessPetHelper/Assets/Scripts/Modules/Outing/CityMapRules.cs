using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Pure rule-side coordinator over the static definition, unlock state and request pool.
    /// Builds immutable map snapshots and validates point selection without mutating state:
    /// the caller applies RequestPoolState.TryTransition and OutingSession.TryDepart afterwards.
    /// No Unity, UI, clock or random dependency. Departure planning also supports exploration
    /// without a request, and safe direct reunions that do not need station space.
    /// </summary>
    public sealed class CityMapRules
    {
        private readonly CityMapDefinition _definition;
        private readonly PointUnlockState _unlockState;
        private readonly RequestPoolState _requestPool;
        private readonly Dictionary<string, HelpRequestTemplate> _templatesById;

        public CityMapRules(CityMapDefinition definition, PointUnlockState unlockState,
            RequestPoolState requestPool, IEnumerable<HelpRequestTemplate> templates)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _unlockState = unlockState ?? throw new ArgumentNullException(nameof(unlockState));
            _requestPool = requestPool ?? throw new ArgumentNullException(nameof(requestPool));
            if (templates == null)
            {
                throw new ArgumentNullException(nameof(templates));
            }

            _templatesById = new Dictionary<string, HelpRequestTemplate>();
            foreach (var template in templates)
            {
                if (template == null || _templatesById.ContainsKey(template.TemplateId))
                {
                    throw new ArgumentException("Templates must be nonnull with unique IDs.", nameof(templates));
                }

                _templatesById.Add(template.TemplateId, template);
            }
        }

        /// <summary>
        /// Builds a frozen map view from the current state. Old snapshots never change afterwards.
        /// </summary>
        public MapViewSnapshot BuildSnapshot()
        {
            var points = new List<MapPointView>(_definition.Points.Count);
            foreach (var point in _definition.Points)
            {
                points.Add(BuildPointView(point));
            }

            return new MapViewSnapshot(_definition.BackgroundId, _definition.RegionLabels, points);
        }

        /// <summary>
        /// Validates that a point is open and carries an available request. Returns the request
        /// for the caller to accept; this method changes no state. No-request points are refused
        /// here for the legacy request-only coordinator. Use TryPlanDeparture for exploration.
        /// </summary>
        public bool TrySelectPoint(string pointId, out HelpRequest request, out OutingFailure failure)
        {
            request = null;
            if (!TryRequireOpenPoint(pointId, out failure))
            {
                return false;
            }

            if (!TryGetAvailableRequest(pointId, out request, out _))
            {
                failure = OutingFailure.PointHasNoRequest;
                return false;
            }

            failure = OutingFailure.None;
            return true;
        }

        /// <summary>
        /// Read-only departure gate. Unknown destinations and exploration require a free
        /// temporary place; only a request-specific direct reunion arrangement is exempt.
        /// Existing accepted requests cannot be bypassed as no-request exploration.
        /// </summary>
        public bool TryPlanDeparture(string pointId, int availableTemporarySpaces,
            out MapDeparturePlan plan, out OutingFailure failure)
        {
            plan = null;
            if (availableTemporarySpaces < 0)
            {
                failure = OutingFailure.InvalidCapacity;
                return false;
            }

            if (!TryRequireOpenPoint(pointId, out failure))
            {
                return false;
            }

            if (!TryGetAvailableRequest(pointId, out var request, out _) &&
                HasAcceptedRequest(pointId))
            {
                failure = OutingFailure.RequestNotAvailable;
                return false;
            }

            var candidate = new MapDeparturePlan(pointId, request);
            if (candidate.RequiresStationSpace && availableTemporarySpaces == 0)
            {
                failure = OutingFailure.CapacityNotAvailable;
                return false;
            }

            plan = candidate;
            failure = OutingFailure.None;
            return true;
        }

        private bool TryRequireOpenPoint(string pointId, out OutingFailure failure)
        {
            failure = OutingFailure.None;
            if (!_definition.TryGetPoint(pointId, out var point))
            {
                failure = OutingFailure.InvalidPoint;
                return false;
            }

            if (!_unlockState.IsOpen(point))
            {
                failure = OutingFailure.PointLocked;
                return false;
            }

            return true;
        }

        private bool HasAcceptedRequest(string pointId)
        {
            foreach (var request in _requestPool.Requests)
            {
                if (request.PointId == pointId && request.State == RequestState.Accepted)
                {
                    return true;
                }
            }

            return false;
        }

        private MapPointView BuildPointView(MapPointDefinition point)
        {
            var open = _unlockState.IsOpen(point);
            if (!open)
            {
                return new MapPointView(point.PointId, point.DisplayNameKey, point.RegionId,
                    PointDisplayState.Locked, null);
            }

            if (TryGetAvailableRequest(point.PointId, out _, out var template))
            {
                return new MapPointView(point.PointId, point.DisplayNameKey, point.RegionId,
                    PointDisplayState.HasHelpRequest, template.TextKey);
            }

            return new MapPointView(point.PointId, point.DisplayNameKey, point.RegionId,
                PointDisplayState.NoRequest, null);
        }

        private bool TryGetAvailableRequest(string pointId, out HelpRequest found, out HelpRequestTemplate template)
        {
            found = null;
            template = null;
            foreach (var request in _requestPool.Requests)
            {
                if (request.State == RequestState.Available && request.PointId == pointId &&
                    _templatesById.TryGetValue(request.TemplateId, out var candidate) && candidate.AllowsPoint(pointId))
                {
                    found = request;
                    template = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
