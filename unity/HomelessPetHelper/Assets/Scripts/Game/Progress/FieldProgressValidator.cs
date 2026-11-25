using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Siege;

namespace HomelessPetHelper.Game.Progress
{
    internal static class FieldProgressValidator
    {
        internal static void Validate(WorldFieldData data, GameLoopSession staged, CityMapPreviewConfig config,
            FieldProgressContent content, ResidentData[] residents, MemorialData[] memorials,
            SiegeState.BattlePhase? capturedPhase = null)
        {
            if (data?.Encounters == null || data.Locations == null ||
                data.Encounters.Length > staged.Requests.Requests.Count ||
                data.Locations.Length > config.Definition.Points.Count)
                throw new ArgumentException("Invalid saved world field header.");
            if (data.Encounters.Length == 0 && data.Locations.Length == 0 && data.Active == null) return;
            if (content == null) throw new ArgumentException("Saved fields require authored content bindings.");
            var requestIds = new HashSet<Guid>();
            var animalIds = new HashSet<Guid>();
            foreach (var encounter in data.Encounters)
            {
                if (encounter == null || encounter.Seed < 0 ||
                    !Guid.TryParseExact(encounter.RequestId, "N", out var requestId) || !requestIds.Add(requestId) ||
                    !staged.Requests.TryGet(requestId, out var request) || request.PointId != encounter.PointId ||
                    request.ReunionPlan != null || config.GetRescue(request.TemplateId) == null)
                    throw new ArgumentException("Invalid encounter request identity.");
                var animal = MainResidentCodec.RestoreAnimal(encounter.Animal).Current;
                if (encounter.RuleVersion != FieldProgressContent.EncounterVersion(
                    config.GetRescue(request.TemplateId), config.TimePolicy))
                    throw new ArgumentException("Saved encounter rules changed without migration.");
                if (!animalIds.Add(animal.AnimalId) ||
                    animal.DefinitionId != config.GetRescue(request.TemplateId).AnimalDefinitionId)
                    throw new ArgumentException("Invalid encounter animal identity.");
                var resident = residents.SingleOrDefault(item => item.RequestId == encounter.RequestId);
                if (request.State == RequestState.Resolved ? resident?.Arrival?.Id != encounter.Animal.Id :
                    staged.Station.Animals.Any(arrival => arrival.AnimalId == animal.AnimalId))
                    throw new ArgumentException("Encounter and resident identities disagree.");
                ValidateOpening(content, encounter.PointId, encounter.LayoutId, encounter.ContentVersion,
                    encounter.TemplateId, encounter.Mode, encounter.Opening);
            }
            var points = new HashSet<string>();
            foreach (var location in data.Locations)
            {
                if (location == null || !points.Add(location.PointId) ||
                    !config.Definition.TryGetPoint(location.PointId, out _) || location.UpdatedMinutes < 0 ||
                    location.UpdatedMinutes > staged.Clock.Current.ElapsedMinutes)
                    throw new ArgumentException("Invalid saved location identity/time.");
                ValidateOpening(content, location.PointId, location.LayoutId, location.ContentVersion,
                    location.TemplateId, location.Mode, location.Opening);
                var source = data.Encounters.SingleOrDefault(item => item.RequestId == location.RequestId);
                if (source == null && Guid.TryParseExact(location.RequestId, "N", out var memorialRequestId) &&
                    memorials != null && memorials.Any(item => item.RequestId == location.RequestId) &&
                    staged.Requests.TryGet(memorialRequestId, out var completed) &&
                    completed.PointId == location.PointId) continue;
                if (source == null || source.PointId != location.PointId || source.LayoutId != location.LayoutId ||
                    source.Mode != location.Mode || source.TemplateId != location.TemplateId ||
                    source.Opening.Bridge != location.Opening.Bridge ||
                    !source.Opening.Boxes.SequenceEqual(location.Opening.Boxes) ||
                    !source.Opening.Remaining.SequenceEqual(location.Opening.Remaining))
                    throw new ArgumentException("Location and its latest encounter results disagree.");
            }
            ValidateActive(data, staged, config, content, capturedPhase);
        }

        private static void ValidateOpening(FieldProgressContent content, string pointId, string layoutId,
            string version, string templateId, int mode, OpeningData state)
        {
            if (!content.IsCompatibleVersion(pointId, layoutId, version) || !Enum.IsDefined(typeof(ApproachMode), mode))
                throw new ArgumentException("Saved field content version is incompatible.");
            var catalog = content.Opening(pointId, layoutId);
            var template = catalog.Get((ApproachMode)mode);
            if (template.Id != templateId) throw new ArgumentException("Saved opening template is unavailable.");
            using var opening = new ApproachSession(Guid.NewGuid(), 0, template, catalog);
            opening.RestoreCheckpoint(FieldCheckpointCodec.Restore(state));
        }

        private static void ValidateActive(WorldFieldData data, GameLoopSession staged, CityMapPreviewConfig config,
            FieldProgressContent content, SiegeState.BattlePhase? capturedPhase)
        {
            var active = data.Active;
            if (active == null) return;
            if (!Guid.TryParseExact(active.OutingId, "N", out var outingId) || outingId == Guid.Empty ||
                active.OutingSeed < 0 || active.Stage < 0 || active.Stage > 2 ||
                (active.Stage == 0) != (active.Battle == null))
                throw new ArgumentException("Invalid active outing checkpoint.");
            var encounter = data.Encounters.SingleOrDefault(item => item.RequestId == active.RequestId);
            if (encounter == null || !staged.Requests.TryGet(Guid.ParseExact(active.RequestId, "N"), out var request) ||
                request.State != RequestState.Available)
                throw new ArgumentException("Active outing request is no longer available.");
            var location = data.Locations.SingleOrDefault(item => item.PointId == encounter.PointId);
            if (location == null || location.RequestId != encounter.RequestId)
                throw new ArgumentException("Active outing does not own its current location snapshot.");
            if (!staged.TryOpenMap(outingId, active.OutingSeed, out _) ||
                !staged.Outing.TrySelectAndDepart(outingId, encounter.PointId, out _) ||
                staged.Outing.Session.AcceptedRequestId != request.RequestId)
                throw new ArgumentException("Cannot restore saved departure.");
            var catalog = content.Opening(encounter.PointId, encounter.LayoutId);
            var opening = new ApproachSession(outingId, encounter.Seed,
                catalog.Get((ApproachMode)encounter.Mode), catalog);
            opening.RestoreCheckpoint(FieldCheckpointCodec.Restore(encounter.Opening));
            if (active.OpenedRoute ? !staged.TryStartOpeningRoute(opening, out _) : !opening.IsComplete)
            { opening.Dispose(); throw new ArgumentException("Cannot restore opening ownership."); }
            if (active.Stage == 0) { opening.Dispose(); return; }
            if (!opening.IsComplete) throw new ArgumentException("Battle checkpoint has an obstructed route.");
            var activity = new SiegeRescueActivity(outingId, encounter.Seed,
                content.Battle(encounter.PointId, encounter.LayoutId, opening), config.GetRescue(request.TemplateId),
                MainResidentCodec.RestoreAnimal(encounter.Animal));
            bool entered = active.OpenedRoute
                ? staged.TryEnterRescueAfterOpening(activity, out _)
                : staged.TryEnterRouteFreeRescue(activity, out _);
            opening.Dispose();
            if (!entered)
            { activity.Dispose(); throw new ArgumentException("Cannot restore battle ownership."); }
            // The capture witness is scoped to this synchronous save and compares every replay field.
            // Disk reads have no witness and always replay all simulation inputs.
            if (!capturedPhase.HasValue) activity.Battle.RestoreReplay(FieldCheckpointCodec.Restore(active.Battle));
            if (active.Stage == 2 && (capturedPhase ?? activity.Battle.Phase) != SiegeState.BattlePhase.Won)
                throw new ArgumentException("Confirmed rescue has no winning battle.");
        }
    }
}
