using System;
using System.Collections.Generic;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Pure rule-side coordinator over the static definition, unlock state and request pool.
    /// Builds immutable map snapshots and validates point selection without mutating state:
    /// the caller applies RequestPoolState.TryTransition and OutingSession.TryDepart afterwards.
    /// No Unity, UI, clock or random dependency. Point behaviour for no-request points is a B-group
    /// decision; this layer only reports the fact that no help request is available.
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
        /// here as a fact only — their eventual click behaviour belongs to the B group.
        /// </summary>
        public bool TrySelectPoint(string pointId, out HelpRequest request, out OutingFailure failure)
        {
            request = null;
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

            if (!TryGetAvailableRequest(pointId, out request))
            {
                failure = OutingFailure.PointHasNoRequest;
                return false;
            }

            failure = OutingFailure.None;
            return true;
        }

        private MapPointView BuildPointView(MapPointDefinition point)
        {
            var open = _unlockState.IsOpen(point);
            if (!open)
            {
                return new MapPointView(point.PointId, point.DisplayNameKey, point.RegionId,
                    PointDisplayState.Locked, null);
            }

            if (TryGetAvailableRequest(point.PointId, out var request) &&
                _templatesById.TryGetValue(request.TemplateId, out var template))
            {
                return new MapPointView(point.PointId, point.DisplayNameKey, point.RegionId,
                    PointDisplayState.HasHelpRequest, template.TextKey);
            }

            return new MapPointView(point.PointId, point.DisplayNameKey, point.RegionId,
                PointDisplayState.NoRequest, null);
        }

        private bool TryGetAvailableRequest(string pointId, out HelpRequest found)
        {
            found = null;
            foreach (var request in _requestPool.Requests)
            {
                if (request.State == RequestState.Available && request.PointId == pointId)
                {
                    found = request;
                    return true;
                }
            }

            return false;
        }
    }
}
