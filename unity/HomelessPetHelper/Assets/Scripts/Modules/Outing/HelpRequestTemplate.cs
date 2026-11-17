using System;
using System.Collections.Generic;
using System.Linq;

namespace HomelessPetHelper.Modules.Outing
{
    /// <summary>
    /// Immutable static definition of a help-request template. Carries only text keys and
    /// point constraints, never a concrete animal instance or its mutable state.
    /// Empty AllowedPointIds means the template may appear at any point.
    /// </summary>
    public sealed class HelpRequestTemplate
    {
        public string TemplateId { get; }

        public string TextKey { get; }

        public string AnimalDefinitionId { get; }

        public IReadOnlyList<string> AllowedPointIds { get; }

        public HelpRequestTemplate(string templateId, string textKey, string animalDefinitionId,
            IEnumerable<string> allowedPointIds)
        {
            if (string.IsNullOrWhiteSpace(templateId) || templateId != templateId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty template ID is required.", nameof(templateId));
            }

            if (string.IsNullOrWhiteSpace(textKey) || textKey != textKey.Trim())
            {
                throw new ArgumentException("A trimmed nonempty text key is required.", nameof(textKey));
            }

            if (string.IsNullOrWhiteSpace(animalDefinitionId) || animalDefinitionId != animalDefinitionId.Trim())
            {
                throw new ArgumentException("A trimmed nonempty animal definition ID is required.",
                    nameof(animalDefinitionId));
            }

            var allowed = allowedPointIds == null
                ? Array.Empty<string>()
                : allowedPointIds.ToArray();

            if (allowed.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Allowed point IDs must be nonempty.", nameof(allowedPointIds));
            }

            TemplateId = templateId;
            TextKey = textKey;
            AnimalDefinitionId = animalDefinitionId;
            AllowedPointIds = Array.AsReadOnly(allowed);
        }

        /// <summary>
        /// Whether this template may appear at the given point. An empty constraint allows any point.
        /// </summary>
        public bool AllowsPoint(string pointId)
        {
            return AllowedPointIds.Count == 0 || AllowedPointIds.Contains(pointId);
        }
    }
}
