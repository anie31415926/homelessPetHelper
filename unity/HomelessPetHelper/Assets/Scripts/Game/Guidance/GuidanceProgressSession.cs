using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Framework.UI;

namespace HomelessPetHelper.Game.Guidance
{
    /// <summary>Separate storage from gameplay checkpoints. Failed loads never overwrite the original.</summary>
    public sealed class GuidanceProgressSession
    {
        private readonly SaveService _saves;
        private readonly SaveSection<GuidanceProgressData> _section;
        private readonly HashSet<string> _completed = new HashSet<string>(StringComparer.Ordinal);
        private bool _loaded;
        public SaveError PersistenceError { get; private set; }

        public GuidanceProgressSession(ISaveStorage storage)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            _saves = new SaveService(storage);
            _section = new SaveSection<GuidanceProgressData>("guidance.completed", 1,
                () => new GuidanceProgressData(), Validate);
            _saves.Register(_section);
            if (!_saves.TryOpen(out _, out var error) || !_saves.TryRead(_section, out var data, out error))
            { PersistenceError = error; return; }
            foreach (string id in data.Completed) _completed.Add(id);
            _loaded = true;
        }

        public bool HasCompleted(string mechanicId) => _completed.Contains(mechanicId);

        public bool TryRecordCompleted(GuideSession guide)
        {
            if (guide == null || !guide.IsComplete) return false;
            return TryRecordShown(guide.MechanicId);
        }

        internal bool TryRecordShown(string mechanicId)
        {
            if (_completed.Contains(mechanicId)) return true;
            if (!_loaded) return false;
            if (!_saves.CanWrite)
            {
                if (!_saves.TryOpen(out _, out var reloadError) ||
                    !_saves.TryRead(_section, out var restored, out reloadError))
                { PersistenceError = reloadError; return false; }
                foreach (string id in restored.Completed) _completed.Add(id);
                if (_completed.Contains(mechanicId)) { PersistenceError = null; return true; }
            }
            var staged = new List<string>(_completed) { mechanicId };
            staged.Sort(StringComparer.Ordinal);
            var data = new GuidanceProgressData { Completed = staged.ToArray() };
            if (!_saves.TryCommit(writer => writer.Set(_section, data), out var error))
            { PersistenceError = error; return false; }
            _completed.Add(mechanicId);
            PersistenceError = null;
            return true;
        }

        private static string Validate(GuidanceProgressData data)
        {
            if (data.Completed == null || data.Completed.Length > 512) return "Invalid guide completion list.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in data.Completed)
                if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || id != id.Trim() || !ids.Add(id))
                    return "Invalid or duplicate guide completion ID.";
            return null;
        }
    }
}
