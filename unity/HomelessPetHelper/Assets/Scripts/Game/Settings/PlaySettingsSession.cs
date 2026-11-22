using System;
using HomelessPetHelper.Framework.Persistence;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace HomelessPetHelper.Game.Settings
{
    /// <summary>Independent preference. Only an explicit successful save changes live battle cadence.</summary>
    public sealed class PlaySettingsSession
    {
        [Preserve, JsonObject(MemberSerialization.OptIn)]
        private sealed class Data
        {
            [Preserve, JsonProperty(Required = Required.Always)] public bool PauseAtDecision = true;
            [Preserve] public Data() { }
        }
        private readonly SaveService _saves;
        private readonly SaveSection<Data> _section;
        public bool PauseAtDecision { get; private set; } = true;
        public SaveError PersistenceError { get; private set; }
        public event Action<bool> Changed;

        public PlaySettingsSession(ISaveStorage storage = null)
        {
            if (storage == null) return;
            _saves = new SaveService(storage);
            _section = new SaveSection<Data>("play.preferences", 1, () => new Data(), _ => null);
            _saves.Register(_section);
            if (!_saves.TryOpen(out _, out var error) || !_saves.TryRead(_section, out var data, out error))
            { PersistenceError = error; return; }
            PauseAtDecision = data.PauseAtDecision;
        }

        public bool TrySave(bool pauseAtDecision)
        {
            // Failed writes can be uncertain. Reopen both slots before retrying; corrupt originals
            // remain untouched because TryOpen refuses them instead of creating a replacement.
            if (PersistenceError != null &&
                (!_saves.TryOpen(out _, out var reloadError) ||
                    !_saves.TryRead(_section, out _, out reloadError)))
            { PersistenceError = reloadError; return false; }
            if (_saves != null && !_saves.TryCommit(writer => writer.Set(_section,
                new Data { PauseAtDecision = pauseAtDecision }), out var error))
            { PersistenceError = error; return false; }
            PersistenceError = null;
            PauseAtDecision = pauseAtDecision;
            Changed?.Invoke(pauseAtDecision);
            return true;
        }
    }
}
