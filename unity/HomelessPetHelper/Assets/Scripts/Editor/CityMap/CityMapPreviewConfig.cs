using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Modules.Outing;
using UnityEngine;

namespace HomelessPetHelper.Editor.CityMap
{
    /// <summary>Validated, local Editor fixture configuration. Never shared mutable gameplay state.</summary>
    public sealed class CityMapPreviewConfig
    {
        private readonly CityMapPreviewConfigData _data;
        private readonly Dictionary<string, string> _detailKeys = new Dictionary<string, string>();
        private readonly HelpRequestTemplate[] _templates;

        public CityMapDefinition Definition { get; }
        public string InitialPointId => _data.initialPointId;
        public int InitialCapacity => _data.initialCapacity;
        public int MinutesPerDay => _data.minutesPerDay;
        public int Seed => _data.seed;
        public OutingTimePolicy TimePolicy { get; }

        public CityMapPreviewConfig(TextAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _data = JsonUtility.FromJson<CityMapPreviewConfigData>(source.text);
            if (_data == null || _data.schemaVersion != 1 || _data.initialCapacity < 0 ||
                _data.minutesPerDay <= 0 || _data.seed < 0 || _data.timePolicy == null ||
                _data.regionIds == null || _data.regionIds.Length == 0 || _data.points == null)
                throw new ArgumentException("Invalid city map config header/settings: " + source.name);
            _data.requestTemplates = _data.requestTemplates ?? Array.Empty<CityMapPreviewConfigData.Template>();
            _data.initialRequests = _data.initialRequests ?? Array.Empty<CityMapPreviewConfigData.Request>();
            ValidateIds(_data.regionIds, "regionIds");
            var timing = _data.timePolicy;
            TimePolicy = new OutingTimePolicy(timing.explorationMinutes, timing.openingMinutes,
                timing.negotiationMinutes, timing.handoverMinutes);
            Definition = BuildDefinition();
            if (!Definition.TryGetPoint(_data.initialPointId, out MapPointDefinition initial) || !initial.OpenByDefault)
                throw new ArgumentException("initialPointId must reference an initially open point.");
            _templates = BuildTemplates();
            ValidateRequests();
        }

        public string GetDetailKey(string pointId) => _detailKeys[pointId];
        public HelpRequestTemplate GetRequestTemplate(string id) =>
            _templates.SingleOrDefault(template => template.TemplateId == id);

        public void ValidateText(OptionPreviewText text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            foreach (MapPointDefinition point in Definition.Points)
            {
                text.Get(point.DisplayNameKey);
                text.Get(GetDetailKey(point.PointId));
            }
            foreach (HelpRequestTemplate template in _templates) text.Get(template.TextKey);
        }

        public CityMapRules CreateRules(PointUnlockState unlocks, RequestPoolState pool) =>
            new CityMapRules(Definition, unlocks, pool, _templates);

        public RequestPoolState CreateRequestPool()
        {
            var pool = new RequestPoolState();
            foreach (CityMapPreviewConfigData.Request entry in _data.initialRequests)
            {
                Guid requestId = Guid.NewGuid();
                OwnerReunionPlan reunion = !entry.hasDirectReception ? null : new OwnerReunionPlan(requestId,
                    entry.reunion.ownerId, entry.reunion.receivingArrangementId);
                var request = new HelpRequest(requestId, entry.templateId, entry.pointId,
                    entry.templateVersion, reunion);
                if (!pool.TryAdd(request, out var failure))
                    throw new InvalidOperationException("Validated initial request rejected: " + failure);
            }
            return pool;
        }

        private CityMapDefinition BuildDefinition()
        {
            var points = new List<MapPointDefinition>();
            foreach (CityMapPreviewConfigData.Point entry in _data.points)
            {
                if (entry == null || !_data.regionIds.Contains(entry.regionId))
                    throw new ArgumentException("Point has an unknown regionId.");
                RequireId(entry.detailKey, "point.detailKey");
                if (entry.sceneTemplateIds == null || entry.sceneTemplateIds.Length == 0)
                    throw new ArgumentException("Point needs sceneTemplateIds: " + entry.id);
                ValidateIds(entry.sceneTemplateIds, "point.sceneTemplateIds");
                var point = new MapPointDefinition(entry.id, entry.nameKey, entry.regionId,
                    entry.openByDefault, entry.sceneTemplateIds);
                if (_detailKeys.ContainsKey(point.PointId))
                    throw new ArgumentException("Duplicate point ID: " + point.PointId);
                _detailKeys.Add(point.PointId, entry.detailKey);
                points.Add(point);
            }
            return new CityMapDefinition(_data.backgroundId, _data.regionIds, points);
        }

        private HelpRequestTemplate[] BuildTemplates()
        {
            var templates = new List<HelpRequestTemplate>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CityMapPreviewConfigData.Template entry in _data.requestTemplates)
            {
                if (entry == null || entry.allowedPointIds == null)
                    throw new ArgumentException("Invalid request template/allowedPointIds.");
                ValidateIds(entry.allowedPointIds, "template.allowedPointIds");
                if (entry.allowedPointIds.Any(id => !Definition.TryGetPoint(id, out _)))
                    throw new ArgumentException("Template references an unknown point: " + entry.id);
                var template = new HelpRequestTemplate(entry.id, entry.textKey,
                    entry.animalDefinitionId, entry.allowedPointIds);
                if (!ids.Add(template.TemplateId))
                    throw new ArgumentException("Duplicate request template ID: " + template.TemplateId);
                templates.Add(template);
            }
            return templates.ToArray();
        }

        private void ValidateRequests()
        {
            foreach (CityMapPreviewConfigData.Request entry in _data.initialRequests)
            {
                if (entry == null || !Definition.TryGetPoint(entry.pointId, out _) || entry.templateVersion <= 0)
                    throw new ArgumentException("Invalid initial request point/version.");
                HelpRequestTemplate template = _templates.SingleOrDefault(item => item.TemplateId == entry.templateId);
                if (template == null || !template.AllowsPoint(entry.pointId))
                    throw new ArgumentException(
                        "Initial request has an unknown/inapplicable template: " + entry.templateId);
                if (!entry.hasDirectReception)
                {
                    if (entry.reunion != null && (!string.IsNullOrEmpty(entry.reunion.ownerId) ||
                        !string.IsNullOrEmpty(entry.reunion.receivingArrangementId)))
                        throw new ArgumentException("Reunion details require hasDirectReception: " + entry.pointId);
                    continue;
                }
                if (entry.reunion == null) throw new ArgumentException("Direct reception needs reunion details.");
                RequireId(entry.reunion.ownerId, "reunion.ownerId");
                RequireId(entry.reunion.receivingArrangementId, "reunion.receivingArrangementId");
            }
        }

        private static void ValidateIds(IEnumerable<string> values, string field)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                RequireId(value, field);
                if (!ids.Add(value)) throw new ArgumentException("Duplicate ID in " + field + ": " + value);
            }
        }

        private static void RequireId(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
                throw new ArgumentException("Missing or untrimmed ID/key: " + field);
        }
    }
}
