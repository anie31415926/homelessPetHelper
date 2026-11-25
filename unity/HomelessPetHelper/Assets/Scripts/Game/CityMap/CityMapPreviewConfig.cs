using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Outing;
using UnityEngine;

namespace HomelessPetHelper.Game.CityMap
{
    /// <summary>Validated local content. Never shared mutable gameplay state.</summary>
    public sealed class CityMapPreviewConfig
    {
        private readonly CityMapPreviewConfigData _data;
        private readonly Dictionary<string, string> _detailKeys = new Dictionary<string, string>();
        private readonly HelpRequestTemplate[] _templates;
        private readonly RescueEncounterDefinition[] _rescueEncounters;

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
            _rescueEncounters = (_data.rescueEncounters ?? Array.Empty<CityMapPreviewConfigData.RescueEncounter>())
                .Select(entry => new RescueEncounterDefinition(entry ??
                    throw new ArgumentException("Missing rescue encounter."))).ToArray();
            ValidateDispatch();
        }

        internal CityMapPreviewConfigData.RequestDispatch Dispatch =>
            _data.dispatch?.enabled == true ? _data.dispatch : null;

        internal CityMapPreviewConfigData.Request RequestBinding(string templateId, string pointId, int version) =>
            _data.initialRequests.Concat(Dispatch?.candidates ?? Array.Empty<CityMapPreviewConfigData.Request>())
                .FirstOrDefault(entry => entry.templateId == templateId && entry.pointId == pointId &&
                    entry.templateVersion == version);

        private void ValidateDispatch()
        {
            var dispatch = Dispatch;
            if (dispatch == null) return;
            if (dispatch.lifetimeMinutes <= 0 || dispatch.highIntervalMinutes <= 0 ||
                dispatch.highIntervalMinutes > dispatch.lowIntervalMinutes ||
                dispatch.lowIntervalMinutes > dispatch.fullIntervalMinutes || dispatch.highCapacityThreshold < 2 ||
                dispatch.candidates == null || dispatch.candidates.Length == 0)
                throw new ArgumentException("Invalid rescue dispatch timing or candidates.");
            var points = new HashSet<string>();
            foreach (var entry in dispatch.candidates)
                if (entry == null || entry.templateVersion <= 0 || entry.hasDirectReception ||
                    (entry.reunion != null && (!string.IsNullOrEmpty(entry.reunion.ownerId) ||
                        !string.IsNullOrEmpty(entry.reunion.receivingArrangementId))) ||
                    !Definition.TryGetPoint(entry.pointId, out _) || !points.Add(entry.pointId) ||
                    GetRequestTemplate(entry.templateId)?.AllowsPoint(entry.pointId) != true ||
                    GetRescue(entry.templateId) == null || _data.initialRequests.Any(initial =>
                        initial.templateId == entry.templateId && initial.pointId == entry.pointId &&
                        initial.templateVersion == entry.templateVersion && initial.hasDirectReception))
                    throw new ArgumentException("Invalid dispatch rescue binding: " + entry?.pointId);
        }

        public RescueEncounterDefinition GetRescue(string templateId) =>
            _rescueEncounters.SingleOrDefault(item => item.TemplateId == templateId);

        public RescueEncounterDefinition GetAnimalPresentation(string definitionId) =>
            _rescueEncounters.FirstOrDefault(item => item.AnimalDefinitionId == definitionId);

        public void ValidateRescues(Modules.Siege.SiegeCatalog catalog)
        {
            var ids = new HashSet<string>();
            foreach (var entry in _rescueEncounters)
            {
                if (entry == null || !ids.Add(entry.TemplateId) ||
                    GetRequestTemplate(entry.TemplateId)?.AnimalDefinitionId != entry.AnimalDefinitionId ||
                    catalog.Content.Encounter(entry.EncounterId) == null
                        || catalog.Profile(entry.RescueProfile) == null ||
                    !entry.KnownHealthy || entry.WashMinutes < 1 || entry.WashMinutes > 1440 ||
                    entry.GroomMinutes < 1 || entry.GroomMinutes > 1440 ||
                    entry.Dirtiness < 0 || entry.Dirtiness > 100 || entry.LooseFur < 0 || entry.LooseFur > 100)
                    throw new ArgumentException("Invalid Main rescue encounter mapping.");
            }
            foreach (var request in _data.initialRequests)
                if (!request.hasDirectReception && GetRescue(request.templateId) == null)
                    throw new ArgumentException("Main ordinary request has no authored encounter.");
        }

        public string GetDetailKey(string pointId) => _detailKeys[pointId];
        public HelpRequestTemplate GetRequestTemplate(string id) =>
            _templates.SingleOrDefault(template => template.TemplateId == id);

        public void ValidateText(OptionPreviewText text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            foreach (string key in new[]
            {
                "title", "pause", "point.Locked", "point.HasHelpRequest", "point.NoRequest",
                "space.required", "space.directReception", "unavailable.reunion", "unavailable.exploration",
                "request.unavailable", "depart", "return", "reopen", "arrived.detail", "arrived.exploring",
                "station", "cancelled", "paused", "ready", "full", "failure", "shelter"
            }) ValidateTextEntry(text, key);
            // Use the same argument types as rendering, so formats are checked too.
            ValidateTextEntry(text, "status", 0, string.Empty);
            ValidateTextEntry(text, "arrived", string.Empty);
            ValidateTextEntry(text, "returned", string.Empty);
            ValidateTextEntry(text, "returned.exploring", string.Empty);
            ValidateTextEntry(text, "detail.noRequest", string.Empty);
            ValidateTextEntry(text, "detail.request", string.Empty, string.Empty);
            ValidateTextEntry(text, "departure.rescue", string.Empty, string.Empty);
            ValidateTextEntry(text, "departure.preview", string.Empty, string.Empty);
            if (Dispatch != null)
            {
                ValidateTextEntry(text, "dispatch.title");
                ValidateTextEntry(text, "dispatch.body", string.Empty, string.Empty);
                ValidateTextEntry(text, "dispatch.closed", string.Empty);
                ValidateTextEntry(text, "dispatch.map");
                ValidateTextEntry(text, "dispatch.later");
            }
            foreach (MapPointDefinition point in Definition.Points)
            {
                ValidateTextEntry(text, point.DisplayNameKey);
                ValidateTextEntry(text, GetDetailKey(point.PointId));
            }
            foreach (HelpRequestTemplate template in _templates) ValidateTextEntry(text, template.TextKey);
        }

        private static void ValidateTextEntry(OptionPreviewText text, string key, params object[] values)
        {
            try { text.Get(key, values); }
            catch (FormatException error)
            {
                throw new ArgumentException("Invalid city map text format for key: " + key, nameof(text), error);
            }
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
