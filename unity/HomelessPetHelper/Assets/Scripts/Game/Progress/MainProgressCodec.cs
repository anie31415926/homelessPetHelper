using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Station;

namespace HomelessPetHelper.Game.Progress
{
    internal static class MainProgressCodec
    {
        internal static MainProgressData NewGame(CityMapPreviewConfig config) => new MainProgressData
        {
            Capacity = config.InitialCapacity, ElapsedMinutes = 0,
            Requests = config.CreateRequestPool().Requests.Select(CaptureRequest).ToArray(),
            Residents = Array.Empty<ResidentData>(), Fields = FieldProgressSession.Empty(),
            Memorials = Array.Empty<MemorialData>()
        };

        internal static MainProgressData Capture(GameLoopSession session, StationCareRoster care,
            IReadOnlyDictionary<Guid, Guid> requestsByOuting, WorldFieldData fields)
        {
            if (session.IsClosed || fields == null || fields.Active == null && (session.Activity != null ||
                (session.Outing.Session != null && !session.Outing.Session.IsSettled) ||
                (session.Flow.Stage != RescueStage.Station && session.Flow.Stage != RescueStage.Care)))
                throw new InvalidOperationException("Checkpoint requires a station or owned field instance.");
            return new MainProgressData
            {
                Capacity = session.Station.TemporaryCapacity, ElapsedMinutes = session.Clock.Current.ElapsedMinutes,
                Requests = session.Requests.Requests.Select(CaptureRequest).ToArray(),
                Fields = fields,
                Residents = session.Station.Animals.Select(arrival =>
                {
                    if (!requestsByOuting.TryGetValue(arrival.OutingId, out var requestId))
                        throw new InvalidOperationException("Missing rescue request identity.");
                    session.Station.TryGetCurrentAnimal(arrival.AnimalId, out var current);
                    session.Station.TryGetCareSnapshot(arrival.AnimalId, out var assessment);
                    return MainResidentCodec.Capture(arrival, current, assessment,
                        care.Get(arrival.AnimalId), requestId);
                }).ToArray()
            };
        }

        internal static GameLoopSession Restore(MainProgressData data, CityMapPreviewConfig config,
            StationArtwork artwork, out StationCareRoster care, out Dictionary<Guid, Guid> links)
        {
            if (data == null || data.Requests == null || data.Residents == null || data.Memorials == null ||
                data.ElapsedMinutes < 0 ||
                data.Capacity < 0 || data.Residents.Length > data.Capacity)
                throw new ArgumentException("Invalid station checkpoint header.");
            var pool = RestoreRequests(data.Requests, config, data.Fields?.Active);
            var session = new GameLoopSession(data.Capacity, config.MinutesPerDay,
                config.CreateRules(new PointUnlockState(), pool), pool, config.TimePolicy, data.ElapsedMinutes);
            care = new StationCareRoster(session, artwork, config);
            links = new Dictionary<Guid, Guid>();
            try
            {
                var residents = new List<StationResidentState>();
                var usedRequests = new HashSet<Guid>();
                foreach (var resident in data.Residents)
                {
                    if (resident == null ||
                        resident.CustomName != null && !MainProgressSession.IsValidAnimalName(resident.CustomName) ||
                        !Guid.TryParseExact(resident.RequestId, "N", out var requestId) ||
                        !pool.TryGet(requestId, out var request) || request.State != RequestState.Resolved ||
                        request.ReunionPlan != null || !usedRequests.Add(requestId))
                        throw new ArgumentException("Resident must belong to one resolved ordinary request.");
                    var encounter = config.GetRescue(request.TemplateId);
                    if (encounter == null || resident.Current?.DefinitionId != encounter.AnimalDefinitionId)
                        throw new ArgumentException("Request and animal content differ.");
                    var restored = MainResidentCodec.Restore(resident);
                    residents.Add(restored);
                    if (!links.TryAdd(restored.Arrival.OutingId, requestId))
                        throw new ArgumentException("Duplicate outing identity.");
                }
                foreach (var memorial in data.Memorials)
                {
                    if (memorial == null || !MainProgressSession.IsMemorialName(memorial.Name) ||
                        !Guid.TryParseExact(memorial.RequestId, "N", out var requestId) ||
                        !pool.TryGet(requestId, out var request) || request.State != RequestState.Resolved ||
                        request.ReunionPlan != null || !usedRequests.Add(requestId) ||
                        memorial.Appearance != config.GetRescue(request.TemplateId)?.AnimalDefinitionId ||
                        config.GetAnimalPresentation(memorial.Appearance) == null)
                        throw new ArgumentException("Invalid adoption memorial.");
                }
                if (pool.Requests.Count(request => request.State == RequestState.Resolved) != usedRequests.Count)
                    throw new ArgumentException("Resolved requests and arrivals differ.");
                session.Station.RestoreResidents(residents);
                config.TimePolicy.TryGetDuration(OutingReturnProgress.HandingOver, false, out var rescueMinutes, out _);
                long minimumMinutes = checked(rescueMinutes * (long)(usedRequests.Count - residents.Count));
                for (int i = 0; i < residents.Count; i++)
                {
                    var state = MainResidentCodec.RestoreCoat(data.Residents[i].Coat);
                    MainResidentCodec.ValidateMask(state, residents[i].Arrival.AnimalDefinitionId, artwork, config);
                    care.Restore(residents[i].Arrival.AnimalId, state);
                    minimumMinutes = checked(minimumMinutes + rescueMinutes + state.WashSettledMinutes +
                        state.GroomSettledMinutes);
                }
                if (data.ElapsedMinutes < minimumMinutes)
                    throw new ArgumentException("Checkpoint time is earlier than its settled work.");
                session.RestoreLastArrival();
                return session;
            }
            catch { session.Dispose(); throw; }
        }

        private static RequestPoolState RestoreRequests(RequestData[] data, CityMapPreviewConfig config,
            ActiveOutingData active)
        {
            if (active != null && (data.Count(item => item?.State == (int)RequestState.Accepted) != 1 ||
                !data.Any(item => item != null && item.Id == active.RequestId &&
                    item.State == (int)RequestState.Accepted)))
                throw new ArgumentException("Active outing and protected request disagree.");
            var requests = new List<HelpRequest>();
            var states = new List<RequestState>();
            foreach (var item in data)
            {
                if (item == null || !Guid.TryParseExact(item.Id, "N", out var id))
                    throw new ArgumentException("Invalid request identity.");
                var template = config.GetRequestTemplate(item.TemplateId);
                var definition = config.RequestBinding(item.TemplateId, item.PointId, item.Version);
                var reunionBinding = definition?.hasDirectReception == true ? definition.reunion : null;
                if (template == null || definition == null ||
                    !template.AllowsPoint(item.PointId) || item.OwnerId != reunionBinding?.ownerId ||
                    item.ReceivingId != reunionBinding?.receivingArrangementId ||
                    !config.Definition.TryGetPoint(item.PointId, out _) ||
                    (item.OwnerId == null) != (item.ReceivingId == null) ||
                    item.State == (int)RequestState.Accepted && active?.RequestId != item.Id)
                    throw new ArgumentException("Incompatible request definition.");
                var reunion = item.OwnerId == null ? null : new OwnerReunionPlan(id, item.OwnerId, item.ReceivingId);
                requests.Add(new HelpRequest(id, item.TemplateId, item.PointId, item.Version, reunion));
                states.Add(item.State == (int)RequestState.Accepted
                    ? RequestState.Available : (RequestState)item.State);
            }
            return RequestPoolState.Restore(requests, states);
        }

        private static RequestData CaptureRequest(HelpRequest request) => new RequestData
        {
            Id = request.RequestId.ToString("N"), TemplateId = request.TemplateId, PointId = request.PointId,
            Version = request.TemplateVersion, State = (int)request.State,
            OwnerId = request.ReunionPlan?.OwnerId, ReceivingId = request.ReunionPlan?.ReceivingArrangementId
        };
    }
}
