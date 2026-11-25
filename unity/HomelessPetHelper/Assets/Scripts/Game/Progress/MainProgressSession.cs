using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Station;
using HomelessPetHelper.Modules.Outing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Game.Progress
{
    /// <summary>Owns one gameplay aggregate. Explicit stable checkpoints are the only disk mutations.</summary>
    public sealed partial class MainProgressSession : IDisposable
    {
        private JToken _checkpointBattle;
        private Modules.Siege.SiegeState.BattlePhase _checkpointPhase;
        private readonly SaveService _saves;
        private readonly SaveSection<MainProgressData> _section;
        private readonly CityMapPreviewConfig _config;
        private readonly StationArtwork _artwork;
        private readonly FieldProgressContent _fieldContent;
        private readonly Dictionary<Guid, Guid> _requestsByOuting;
        private readonly RequestDispatch _dispatch;
        public GameLoopSession Session { get; }
        public StationCareRoster Care { get; }
        public FieldProgressSession Fields { get; }
        public SaveError PersistenceError { get; private set; }
        public bool CanContinue => Session != null && PersistenceError == null && !Session.IsClosed;
        public bool Recovered { get; }
        public bool IsNewGame { get; }
        public bool ReconciledContent { get; private set; }

        public MainProgressSession(CityMapPreviewConfig config, StationArtwork artwork, ISaveStorage storage = null,
            FieldProgressContent fieldContent = null, bool reconcileContent = false)
            : this(config, artwork, storage, fieldContent, null, reconcileContent) { }

        internal MainProgressSession(CityMapPreviewConfig config, StationArtwork artwork, ISaveStorage storage,
            FieldProgressContent fieldContent, StationEconomyRules economyRules, bool reconcileContent = false)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
            _fieldContent = fieldContent;
            _economyRules = economyRules;
            MainProgressData data;
            if (storage == null)
            {
                data = NewGame();
                IsNewGame = true;
            }
            else
            {
                _saves = new SaveService(storage);
                _section = new SaveSection<MainProgressData>("main.station", 6, NewGame, Validate,
                    new Dictionary<int, Func<JObject, JObject>>
                    {
                        [1] = MigrateVersionOne, [2] = MigrateVersionTwo, [3] = MigrateVersionThree,
                        [4] = MigrateVersionFour, [5] = MigrateVersionFive
                    }, reconcileContent ? (Func<JObject, JObject>)ReconcileOnRead : null);
                _saves.Register(_section);
                if (!_saves.TryOpen(out var status, out var error) ||
                    !_saves.TryRead(_section, out data, out error))
                {
                    PersistenceError = error;
                    return;
                }
                if (ReconciledContent) _saves.PreserveOriginalsBeforeNextCommit();
                Recovered = status == SaveOpenStatus.Recovered;
                IsNewGame = status == SaveOpenStatus.New;
            }
            Session = MainProgressCodec.Restore(data, config, artwork, out var care, out var links);
            Care = care;
            _requestsByOuting = links;
            Fields = new FieldProgressSession(data.Fields, fieldContent, config);
            PublishEconomy(data.Economy ?? new StationEconomyData());
            _memorials = data.Memorials ?? Array.Empty<MemorialData>();
            foreach (var resident in data.Residents)
            {
                Guid animalId = Guid.ParseExact(resident.Current.Id, "N");
                if (resident.AdoptionListed) _listed.Add(animalId);
                if (resident.CustomName != null) _customNames.Add(animalId, resident.CustomName);
            }
            Fields.RestoreActive(Session);
            _dispatch = new RequestDispatch(config, fieldContent, Session, data.Dispatch);
            _dispatch.Refresh(Fields.HasActive);
        }

        private MainProgressData NewGame()
        {
            var data = MainProgressCodec.NewGame(_config);
            if (_economyRules != null) data.Capacity = _economyRules.initialCapacity;
            data.Economy = new StationEconomyData();
            return data;
        }

        private static JObject MigrateVersionFive(JObject data)
        {
            if (data["Economy"] == null) data["Economy"] = JObject.FromObject(new StationEconomyData());
            if (data["Residents"] is JArray residents)
                foreach (var item in residents)
                    if (item is JObject resident && resident["CustomName"] == null)
                        resident["CustomName"] = JValue.CreateNull();
            return data;
        }

        private static JObject MigrateVersionOne(JObject data)
        {
            data["Fields"] = new JObject
            {
                ["Encounters"] = new JArray(), ["Locations"] = new JArray(), ["Active"] = JValue.CreateNull()
            };
            return data;
        }

        private static JObject MigrateVersionTwo(JObject data)
        {
            data["Memorials"] = new JArray();
            if (data["Residents"] is JArray residents)
                foreach (var item in residents)
                    if (item is JObject resident) resident["AdoptionListed"] = false;
            return data;
        }

        private static JObject MigrateVersionThree(JObject data)
        {
            data["Dispatch"] = JValue.CreateNull();
            return data;
        }

        private static JObject MigrateVersionFour(JObject data)
        {
            if (data["Residents"] is JArray residents)
                foreach (var item in residents)
                    if (item is JObject resident) resident["CustomName"] = JValue.CreateNull();
            return data;
        }

        internal HelpRequest UnshownRescueNotice => _dispatch?.UnshownNotice;

        internal void MarkRescueNoticeShown() => _dispatch.MarkShown();

        public void RecordRescuedReturn(Guid outingId, Guid requestId)
        {
            if (!CanContinue || !Session.Requests.TryGet(requestId, out var request) ||
                request.State != RequestState.Resolved
                    || Session.LastArrival?.Arrival.OutingId != outingId ||
                _requestsByOuting.ContainsKey(outingId))
                throw new InvalidOperationException("Only a new, committed rescue return may be recorded.");
            _requestsByOuting.Add(outingId, requestId);
            _pendingRescueAid = checked(_pendingRescueAid + (_economyRules?.rescueAid ?? 0));
        }

        /// <summary>
        /// Failure keeps the live aggregate for inspection and blocks all subsequent player mutations.
        /// </summary>
        public bool TryCheckpoint()
        {
            if (!CanContinue) return false;
            try
            {
                _dispatch.Refresh(Fields.HasActive);
                var data = Capture();
                if (_saves == null) { PublishEconomy(data.Economy); return true; }
                var battle = (Session.Activity as SiegeRescueActivity)?.Battle;
                if (battle != null && data.Fields.Active?.Battle != null)
                {
                    _checkpointBattle = JToken.FromObject(data.Fields.Active.Battle);
                    _checkpointPhase = battle.Phase;
                }
                try
                {
                    if (_saves.TryCommit(writer => writer.Set(_section, data), out var error))
                    { PublishEconomy(data.Economy); return true; }
                    PersistenceError = error;
                }
                finally { _checkpointBattle = null; }
            }
            catch (InvalidOperationException cause)
            { PersistenceError = new SaveError(SaveErrorCode.InvalidSection, "Invalid Main checkpoint.", cause); }
            catch (ArgumentException cause)
            { PersistenceError = new SaveError(SaveErrorCode.InvalidSection, "Invalid Main checkpoint.", cause); }
            return false;
        }

        private string Validate(MainProgressData data)
        {
            try
            {
                if (data.Economy == null || data.Economy.Balance < 0 ||
                    (data.Economy.ClipEquipped && (!data.Economy.Clip || _economyRules == null)))
                    return "Invalid station economy checkpoint.";
                using var staged = MainProgressCodec.Restore(data, _config, _artwork, out _, out _);
                RequestDispatch.Validate(data.Dispatch, staged, data.Fields);
                bool capturedBattle = _checkpointBattle != null && data.Fields?.Active?.Battle != null &&
                    JToken.DeepEquals(_checkpointBattle, JToken.FromObject(data.Fields.Active.Battle));
                FieldProgressValidator.Validate(data.Fields, staged, _config, _fieldContent, data.Residents,
                    data.Memorials, capturedBattle ? _checkpointPhase : null, data.Economy.ClipEquipped);
                return null;
            }
            catch (ArgumentException) { return "Invalid or incompatible Main station checkpoint."; }
            catch (OverflowException) { return "Main checkpoint time exceeds the supported range."; }
            catch (InvalidOperationException) { return "Invalid Main checkpoint state."; }
            catch (KeyNotFoundException) { return "Unavailable Main checkpoint content."; }
        }

        private JObject ReconcileOnRead(JObject original)
        {
            var repaired = (JObject)original.DeepClone();
            // Notice scheduling is independent of the station and location snapshots.
            repaired["Dispatch"] = JValue.CreateNull();
            if (IsReadable(repaired)) return MarkReconciled(repaired);

            if (!(repaired["Requests"] is JArray requests)) return original;
            var settledIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in new[] { "Residents", "Memorials" })
                if (repaired[key] is JArray records)
                    foreach (var record in records.OfType<JObject>())
                        settledIds.Add((string)record["RequestId"]);
            foreach (var request in requests.OfType<JObject>())
                if ((int?)request["State"] == (int)RequestState.Accepted &&
                    !settledIds.Contains((string)request["Id"]))
                    request["State"] = (int)RequestState.Available;
            if (repaired["Fields"] is JObject fields && fields["Active"]?.Type == JTokenType.Object)
            {
                fields["Active"] = JValue.CreateNull();
                if (IsReadable(repaired)) return MarkReconciled(repaired);
            }
            repaired["Fields"] = JObject.FromObject(FieldProgressSession.Empty());
            if (IsReadable(repaired)) return MarkReconciled(repaired);

            // If the request catalog changed, replace only unowned requests with current seeds.
            // A settled ledger entry must still cross-check with its resident or memorial.
            var retained = requests.OfType<JObject>().Where(request =>
                (int?)request["State"] == (int)RequestState.Resolved || settledIds.Contains((string)request["Id"]));
            repaired["Requests"] = new JArray(retained.ToArray());
            foreach (var request in _config.CreateRequestPool().Requests)
                ((JArray)repaired["Requests"]).Add(JObject.FromObject(MainProgressCodec.CaptureRequest(request)));
            return IsReadable(repaired) ? MarkReconciled(repaired) : original;
        }

        private JObject MarkReconciled(JObject data)
        {
            ReconciledContent = true;
            return data;
        }

        private bool IsReadable(JObject snapshot)
        {
            try { return Validate(snapshot.ToObject<MainProgressData>(JsonSerializer.Create(new JsonSerializerSettings
                { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None,
                    MetadataPropertyHandling = MetadataPropertyHandling.Ignore }))) == null; }
            catch (JsonException) { return false; }
        }

        public void Dispose()
        {
            Fields?.Dispose();
            Session?.Dispose();
        }
    }
}
