using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence
{
    /// <summary>
    /// Explicit single-thread/single-writer service. Open never writes. Read returns detached DTOs.
    /// Commit writes a whole checkpoint to the inactive slot, then verifies it before accepting it.
    /// No automatic reset, global instance, gameplay references, or implicit exit-time save.
    /// </summary>
    public sealed class SaveService
    {
        private readonly ISaveStorage _storage;
        private readonly Dictionary<string, SaveSectionDefinition> _definitions =
            new Dictionary<string, SaveSectionDefinition>(StringComparer.Ordinal);
        private readonly string[] _observed = new string[2];
        private readonly bool[] _damaged = new bool[2];
        private readonly List<SaveError> _recoveryWarnings = new List<SaveError>();
        private JObject _document;
        private int _activeSlot = -1;
        private bool _started;
        private bool _busy;
        private bool _hasStorageSnapshot;
        private bool _archiveBeforeCommit;

        public bool CanWrite { get; private set; }
        public bool CanDelete => !_busy && _hasStorageSnapshot && _storage is IDeletableSaveStorage &&
            (_observed[0] != null || _observed[1] != null);
        public IReadOnlyList<SaveError> RecoveryWarnings => _recoveryWarnings.AsReadOnly();
        public long Revision => _document == null ? 0 : SaveJson.Integer(_document, "revision");

        public SaveService(ISaveStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public void Register<T>(SaveSection<T> section) where T : class
        {
            if (_started) throw new InvalidOperationException("Register sections before the first Open.");
            if (section == null) throw new ArgumentNullException(nameof(section));
            _definitions.Add(section.Id, section);
        }

        public bool TryOpen(out SaveOpenStatus status, out SaveError error)
        {
            status = SaveOpenStatus.Blocked;
            error = null;
            if (_busy) return Fail(SaveErrorCode.Busy, "A save operation is already running.", out error);
            _started = true;
            CanWrite = false;
            _document = null;
            _activeSlot = -1;
            _recoveryWarnings.Clear();
            _archiveBeforeCommit = false;
            _busy = true;
            try
            {
                ReadStorageSnapshot();
                var candidates = ReadCandidates();
                SelectCandidate(candidates);
                var prepared = (JObject)(_document ?? SaveEnvelope.NewDocument()).DeepClone();
                foreach (var definition in _definitions.Values)
                {
                    var record = prepared["sections"][definition.Id] as JObject;
                    if (record != null) definition.Prepare(record);
                }
                _document = prepared;
                CanWrite = true;
                status = _activeSlot < 0 ? SaveOpenStatus.New :
                    _damaged[0] || _damaged[1] ? SaveOpenStatus.Recovered : SaveOpenStatus.Loaded;
                return true;
            }
            catch (Exception cause)
            {
                _document = null;
                error = ConvertError(cause, SaveErrorCode.InvalidSection, "Save could not be opened.");
                return false;
            }
            finally { _busy = false; }
        }

        public bool TryRead<T>(SaveSection<T> section, out T data, out SaveError error) where T : class
        {
            data = null;
            error = null;
            if (_busy) return Fail(SaveErrorCode.Busy, "A save operation is already running.", out error);
            if (!CanWrite) return Fail(SaveErrorCode.NotOpen, "Open a valid save before reading.", out error);
            if (section == null || !_definitions.TryGetValue(section.Id, out var registered) ||
                !ReferenceEquals(section, registered))
                return Fail(SaveErrorCode.InvalidSection, "Section is not registered.", out error);
            try
            {
                data = section.Read(_document["sections"][section.Id] as JObject);
                // A default factory may return a shared instance; never expose that instance to a caller.
                data = section.Capture(data).ToObject<T>(SaveJson.Serializer());
                return true;
            }
            catch (Exception cause)
            {
                data = null;
                error = ConvertError(cause, SaveErrorCode.InvalidSection, "Section could not be read.");
                return false;
            }
        }

        public bool TryCommit(Action<SaveWriter> update, out SaveError error)
        {
            error = null;
            if (_busy) return Fail(SaveErrorCode.Busy, "A save operation is already running.", out error);
            if (!CanWrite) return Fail(SaveErrorCode.NotOpen, "Open a valid save before committing.", out error);
            if (update == null) throw new ArgumentNullException(nameof(update));
            if (Revision == long.MaxValue)
                return Fail(SaveErrorCode.RevisionOverflow, "Save revision is exhausted.", out error);
            _busy = true;
            try
            {
                var draft = (JObject)_document.DeepClone();
                var writer = new SaveWriter((JObject)draft["sections"], _definitions);
                try { update(writer); }
                finally { writer.Close(); }
                draft["revision"] = Revision + 1;
                return WriteCheckpoint(draft, out error);
            }
            catch (Exception cause)
            {
                error = ConvertError(cause, SaveErrorCode.InvalidSection, "Checkpoint capture failed.");
                return false;
            }
            finally { _busy = false; }
        }

        public void PreserveOriginalsBeforeNextCommit()
        {
            if (!CanWrite || _busy) throw new InvalidOperationException("Open a valid save first.");
            _archiveBeforeCommit = true;
        }

        /// <summary>
        /// Delete the exact copies observed by Open, including readable but invalid saves.
        /// On an uncertain result, reopen before retrying. An older copy may already be gone.
        /// </summary>
        public bool TryDelete(out SaveError error)
        {
            if (_busy) return Fail(SaveErrorCode.Busy, "A save operation is already running.", out error);
            if (!CanDelete)
                return Fail(SaveErrorCode.NotOpen, "No observed checkpoint can be deleted.", out error);
            _busy = true;
            CanWrite = _hasStorageSnapshot = false;
            try
            {
                for (int slot = 0; slot < _observed.Length; slot++)
                    if (!string.Equals(_observed[slot], _storage.Read(slot), StringComparison.Ordinal))
                        return Fail(SaveErrorCode.Conflict, "Storage changed; reopen before deleting.", out error);

                // Remove the inactive copy first so a failed removal cannot roll a good profile back.
                int first = _activeSlot == 0 ? 1 : 0;
                var storage = (IDeletableSaveStorage)_storage;
                DeleteObservedCopy(storage, first);
                DeleteObservedCopy(storage, 1 - first);
                error = null;
                return true;
            }
            catch (Exception cause)
            {
                error = new SaveError(SaveErrorCode.WriteUncertain, "Reopen to resolve checkpoint deletion.", cause);
                return false;
            }
            finally
            {
                _busy = false;
                _document = null;
            }
        }

        /// <summary>
        /// Removes both copies of one profile without requiring a readable checkpoint: each original is
        /// archived first, then deleted and read back. Used for damaged or unopenable slots that
        /// <see cref="TryDelete"/> cannot observe. Callers must reopen before writing a new save.
        /// </summary>
        public static bool TryReset(ISaveStorage storage, out SaveError error)
        {
            error = null;
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!(storage is IDeletableSaveStorage deletable))
                return Fail(SaveErrorCode.NotOpen, "This storage cannot remove checkpoints.", out error);
            try
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    if (storage.Read(slot) == null) continue;
                    storage.Archive(slot);
                    deletable.Delete(slot);
                    if (storage.Read(slot) != null)
                        throw new IOException("Checkpoint still exists after deletion.");
                }
                return true;
            }
            catch (Exception cause)
            {
                error = new SaveError(SaveErrorCode.WriteUncertain,
                    "Reset could not remove every checkpoint; archived copies are kept.", cause);
                return false;
            }
        }

        private void DeleteObservedCopy(IDeletableSaveStorage storage, int slot)
        {
            if (_observed[slot] == null) return;
            storage.Delete(slot);
            if (storage.Read(slot) != null) throw new IOException("Checkpoint still exists after deletion.");
        }

        private void ReadStorageSnapshot()
        {
            _hasStorageSnapshot = false;
            for (int slot = 0; slot < _observed.Length; slot++)
            {
                try { _observed[slot] = _storage.Read(slot); }
                catch (Exception cause)
                {
                    throw new SaveError(SaveErrorCode.StorageReadFailed, "Save slot could not be read.", cause);
                }
            }
            _hasStorageSnapshot = true;
        }

        private JObject[] ReadCandidates()
        {
            var candidates = new JObject[_observed.Length];
            for (int slot = 0; slot < candidates.Length; slot++)
            {
                _damaged[slot] = false;
                if (_observed[slot] == null) continue;
                try { candidates[slot] = SaveEnvelope.Decode(_observed[slot]); }
                catch (SaveError) { throw; }
                catch (Exception cause)
                {
                    _damaged[slot] = true;
                    _recoveryWarnings.Add(new SaveError(SaveErrorCode.Corrupt,
                        "Damaged save slot " + slot + "; original content is preserved.", cause));
                }
                if (candidates[slot] != null) RejectFutureSections(candidates[slot]);
            }
            return candidates;
        }

        private void RejectFutureSections(JObject candidate)
        {
            foreach (var definition in _definitions.Values)
            {
                var record = candidate["sections"][definition.Id] as JObject;
                if (record != null && SaveJson.Integer(record, "version") > definition.Version)
                    throw new SaveError(SaveErrorCode.FutureVersion,
                        "Section requires a newer client: " + definition.Id);
            }
        }

        private void SelectCandidate(JObject[] candidates)
        {
            for (int slot = 0; slot < 2; slot++)
            {
                if (candidates[slot] == null) continue;
                long revision = SaveJson.Integer(candidates[slot], "revision");
                if (_document != null && revision == Revision && !JToken.DeepEquals(_document, candidates[slot]))
                    throw new SaveError(SaveErrorCode.Conflict, "Save slots disagree at the same revision.");
                if (_document == null || revision > Revision)
                {
                    _document = candidates[slot];
                    _activeSlot = slot;
                }
            }
            if (_document == null && (_damaged[0] || _damaged[1]))
                throw new SaveError(SaveErrorCode.Corrupt, "No valid save remains; original slots are preserved.");
        }

        private bool WriteCheckpoint(JObject draft, out SaveError error)
        {
            error = null;
            string content = SaveEnvelope.Encode(draft);
            int target = _activeSlot == 0 ? 1 : 0;
            try
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    if (!string.Equals(_observed[slot], _storage.Read(slot), StringComparison.Ordinal))
                    {
                        CanWrite = false;
                        return Fail(SaveErrorCode.Conflict, "Storage changed; reopen before saving.", out error);
                    }
                }
                if (_archiveBeforeCommit)
                {
                    for (int slot = 0; slot < _observed.Length; slot++)
                        if (_observed[slot] != null) _storage.Archive(slot);
                    _archiveBeforeCommit = false;
                }
                else if (_damaged[target]) _storage.Archive(target);
                _storage.Write(target, content);
                if (!string.Equals(_storage.Read(target), content, StringComparison.Ordinal))
                    throw new IOException("Save readback did not match the checkpoint.");
            }
            catch (Exception cause)
            {
                CanWrite = false;
                error = new SaveError(SaveErrorCode.WriteUncertain, "Reopen to resolve checkpoint persistence.", cause);
                return false;
            }
            _document = draft;
            _observed[target] = content;
            _damaged[target] = false;
            _activeSlot = target;
            return true;
        }

        private static SaveError ConvertError(Exception cause, SaveErrorCode code, string message) =>
            cause as SaveError ?? new SaveError(code, message, cause);

        private static bool Fail(SaveErrorCode code, string message, out SaveError error)
        {
            error = new SaveError(code, message);
            return false;
        }
    }
}
