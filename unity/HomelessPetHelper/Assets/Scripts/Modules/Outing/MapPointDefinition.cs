using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable static definition of one selectable city-map point.
    /// Region is a grouping label only, not a traversable node layer.
    /// Unlock is opt-in via the open condition; no level/building system is implemented here.
    /// </summary>
    public sealed class MapPointDefinition
    {
        public string PointId { get; }

        public string DisplayNameKey { get; }

        public string RegionId { get; }

        public bool OpenByDefault { get; }

        public IReadOnlyList<string> SceneTemplateIds { get; }

        public MapPointDefinition(string pointId, string displayNameKey, string regionId,
            bool openByDefault, IEnumerable<string> sceneTemplateIds)
        {
            if (string.IsNullOrWhiteSpace(pointId) || pointId != pointId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty point ID is required.", nameof(pointId));
            }

            if (string.IsNullOrWhiteSpace(displayNameKey) || displayNameKey != displayNameKey.Trim())
            {
                throw new ArgumentException("A trimmed nonempty display name key is required.", nameof(displayNameKey));
            }

            if (string.IsNullOrWhiteSpace(regionId) || regionId != regionId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty region ID is required.", nameof(regionId));
            }

            if (sceneTemplateIds == null)
            {
                throw new ArgumentNullException(nameof(sceneTemplateIds));
            }

            var templates = sceneTemplateIds.ToArray();
            if (templates.Length == 0)
            {
                throw new ArgumentException("At least one scene template ID is required.", nameof(sceneTemplateIds));
            }

            if (templates.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Scene template IDs must be nonempty.", nameof(sceneTemplateIds));
            }

            PointId = pointId;
            DisplayNameKey = displayNameKey;
            RegionId = regionId;
            OpenByDefault = openByDefault;
            SceneTemplateIds = Array.AsReadOnly(templates);
        }
    }
}
