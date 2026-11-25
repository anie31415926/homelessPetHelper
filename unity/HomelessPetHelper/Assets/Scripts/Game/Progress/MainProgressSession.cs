using System;
using System.Collections.Generic;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Station;
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

        public MainProgressSession(CityMapPreviewConfig config, StationArtwork artwork, ISaveStorage storage = null,
            FieldProgressContent fieldContent = null) : this(config, artwork, storage, fieldContent, null) { }

        internal MainProgressSession(CityMapPreviewConfig config, StationArtwork artwork, ISaveStorage storage,
            FieldProgressContent fieldContent, StationEconomyRules economyRules)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
            _fieldContent = fieldContent;
            _economyRules = economyRules;
            MainProgressData data;
            if (storage == null) data = NewGame();
            else
            {
                _saves = new SaveService(storage);
                _section = new SaveSection<MainProgressData>("main.station", 5, NewGame, Validate,
                    new Dictionary<int, Func<JObject, JObject>>
                    {
                        [1] = MigrateVersionOne, [2] = MigrateVersionTwo,
                        [3] = MigrateVersionThree, [4] = MigrateVersionFour
                    });
                _saves.Register(_section);
                if (!_saves.TryOpen(out var status, out var error) ||
                    !_saves.TryRead(_section, out data, out error))
                {
                    PersistenceError = error;
                    return;
                }
                Recovered = status == SaveOpenStatus.Recovered;
            }
            Session = MainProgressCodec.Restore(data, config, artwork, out var care, out var links);
            Care = care;
            _requestsByOuting = links;
            Fields = new FieldProgressSession(data.Fields, fieldContent, config);
            PublishEconomy(data.Economy ?? new StationEconomyData());
            _memorials = data.Memorials ?? Array.Empty<MemorialData>();
            foreach (var resident in data.Residents)
                if (resident.AdoptionListed) _listed.Add(Guid.ParseExact(resident.Current.Id, "N"));
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

        private static JObject MigrateVersionFour(JObject data)
        {
            data["Economy"] = JObject.FromObject(new StationEconomyData());
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

        internal Modules.Outing.HelpRequest UnshownRescueNotice => _dispatch?.UnshownNotice;

        internal void MarkRescueNoticeShown() => _dispatch.MarkShown();

        public void RecordRescuedReturn(Guid outingId, Guid requestId)
        {
            if (!CanContinue || !Session.Requests.TryGet(requestId, out var request) ||
                request.State != Modules.Outing.RequestState.Resolved
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
        }

        public void Dispose()
        {
            Fields?.Dispose();
            Session?.Dispose();
        }
    }
}
