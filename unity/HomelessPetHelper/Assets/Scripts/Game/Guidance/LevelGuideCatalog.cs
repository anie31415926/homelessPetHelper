using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.UI;
using HomelessPetHelper.Game.Text;
using UnityEngine;

namespace HomelessPetHelper.Game.Guidance
{
    public sealed class LevelGuideCatalog
    {
        [Serializable] private sealed class CatalogData
        {
            public int schemaVersion;
            public Definition[] guides;
        }
        [Serializable] private sealed class Definition
        {
            public string contextId, mechanicId;
            public Step[] steps;
        }
        [Serializable] private sealed class Step
        {
            public string captionKey, completionAction;
            public string[] allowedActions;
        }
        private readonly Dictionary<string, Definition> _definitions =
            new Dictionary<string, Definition>(StringComparer.Ordinal);

        public LevelGuideCatalog(TextAsset data, OptionPreviewText text)
        {
            if (data == null || text == null) throw new ArgumentNullException(nameof(data));
            var catalog = JsonUtility.FromJson<CatalogData>(data.text);
            if (catalog == null || catalog.schemaVersion != 1 || catalog.guides == null ||
                catalog.guides.Length == 0 || catalog.guides.Length > 32)
                throw new ArgumentException("Invalid level guide catalog.");
            foreach (var definition in catalog.guides)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.contextId) ||
                    string.IsNullOrWhiteSpace(definition.mechanicId) || definition.steps == null ||
                    definition.mechanicId.Length > 128 || definition.mechanicId != definition.mechanicId.Trim() ||
                    definition.steps.Length == 0 || definition.steps.Length > 32 ||
                    _definitions.ContainsKey(definition.contextId))
                    throw new ArgumentException("Invalid or duplicate guide context.");
                foreach (var step in definition.steps)
                {
                    if (step == null || step.allowedActions == null || step.allowedActions.Length > 12)
                        throw new ArgumentException("Invalid guide step.");
                    new GuideStep(step.captionKey, step.completionAction, step.allowedActions);
                    text.Get(step.captionKey);
                    foreach (string action in step.allowedActions) ValidateAction(action);
                }
                _definitions.Add(definition.contextId, definition);
            }
        }

        public GuideSession Create(string contextId)
        {
            if (!_definitions.TryGetValue(contextId, out var definition)) return null;
            var steps = new GuideStep[definition.steps.Length];
            for (int index = 0; index < steps.Length; index++)
            {
                var step = definition.steps[index];
                steps[index] = new GuideStep(step.captionKey, step.completionAction, step.allowedActions);
            }
            return new GuideSession(definition.mechanicId, steps);
        }

        public void ValidateOpening(Modules.Approach.ApproachTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (!_definitions.TryGetValue(template.Id, out var definition)) return;
            foreach (var step in definition.steps)
                foreach (string action in step.allowedActions)
                {
                    if (!action.StartsWith("approach.", StringComparison.Ordinal)) continue;
                    string suffix = action.Substring(action.LastIndexOf('.') + 1);
                    if (int.TryParse(suffix, out int cell) && !template.Contains(cell))
                        throw new ArgumentException("Guide target is outside its opening template: " + action);
                }
        }

        private static void ValidateAction(string action)
        {
            if (action == "siege.card" || action == "siege.route" || action == "siege.submit") return;
            if (action == "approach.tool.Litter" || action == "approach.tool.Weeds" ||
                action == "approach.tool.Mud") return;
            foreach (string prefix in new[] { "approach.clean.", "approach.box.", "approach.move.",
                "approach.bridge." })
                if (action.StartsWith(prefix, StringComparison.Ordinal) &&
                    int.TryParse(action.Substring(prefix.Length), out int cell) && cell >= 0 &&
                    cell < Modules.Approach.ApproachCatalog.MaxCells) return;
            throw new ArgumentException("Unknown guide action: " + action);
        }
    }
}
