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
        public bool IsNewGame { get; }

        public MainProgressSession(CityMapPreviewConfig config, StationArtwork artwork, ISaveStorage storage = null,
            FieldProgressContent fieldContent = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _artwork = artwork ?? throw new ArgumentNullException(nameof(artwork));
            _fieldContent = fieldContent;
            MainProgressData data;
            if (storage == null)
            {
                data = MainProgressCodec.NewGame(config);
                IsNewGame = true;
            }
            else
            {
                _saves = new SaveService(storage);
                _section = new SaveSection<MainProgressData>("main.station", 5,
                    () => MainProgressCodec.NewGame(config), Validate,
                    new Dictionary<int, Func<JObject, JObject>>
                    {
                        [1] = MigrateVersionOne, [2] = MigrateVersionTwo, [3] = MigrateVersionThree,
                        [4] = MigrateVersionFour
                    });
                _saves.Register(_section);
                if (!_saves.TryOpen(out var status, out var error) ||
                    !_saves.TryRead(_section, out data, out error))
                {
                    PersistenceError = error;
                    return;
                }
                Recovered = status == SaveOpenStatus.Recovered;
                IsNewGame = status == SaveOpenStatus.New;
            }
            Session = MainProgressCodec.Restore(data, config, artwork, out var care, out var links);
            Care = care;
            _requestsByOuting = links;
            Fields = new FieldProgressSession(data.Fields, fieldContent, config);
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
                if (_saves == null) return true;
                var battle = (Session.Activity as SiegeRescueActivity)?.Battle;
                if (battle != null && data.Fields.Active?.Battle != null)
                {
                    _checkpointBattle = JToken.FromObject(data.Fields.Active.Battle);
                    _checkpointPhase = battle.Phase;
                }
                try
                {
                    if (_saves.TryCommit(writer => writer.Set(_section, data), out var error)) return true;
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
                using var staged = MainProgressCodec.Restore(data, _config, _artwork, out _, out _);
                RequestDispatch.Validate(data.Dispatch, staged, data.Fields);
                bool capturedBattle = _checkpointBattle != null && data.Fields?.Active?.Battle != null &&
                    JToken.DeepEquals(_checkpointBattle, JToken.FromObject(data.Fields.Active.Battle));
                FieldProgressValidator.Validate(data.Fields, staged, _config, _fieldContent, data.Residents,
                    data.Memorials,
                    capturedBattle ? _checkpointPhase : (Modules.Siege.SiegeState.BattlePhase?)null);
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
