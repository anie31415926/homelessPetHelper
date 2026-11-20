using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence
{
    /// <summary>
    /// Explicit DTO contract. Validation returns null on success, otherwise a diagnostic without player data.
    /// Migration key N converts data from N to N+1 on a detached copy; no disk or live-state side effects.
    /// </summary>
    public sealed class SaveSection<T> : SaveSectionDefinition where T : class
    {
        private readonly Func<T> _createDefault;
        private readonly Func<T, string> _validate;
        private readonly Dictionary<int, Func<JObject, JObject>> _migrations;

        public SaveSection(string id, int version, Func<T> createDefault, Func<T, string> validate,
            IDictionary<int, Func<JObject, JObject>> migrations = null) : base(id, version)
        {
            _createDefault = createDefault ?? throw new ArgumentNullException(nameof(createDefault));
            _validate = validate ?? throw new ArgumentNullException(nameof(validate));
            _migrations = migrations == null ? new Dictionary<int, Func<JObject, JObject>>() :
                new Dictionary<int, Func<JObject, JObject>>(migrations);
        }

        internal T Read(JObject record)
        {
            T data = record == null ? _createDefault() : record["data"].ToObject<T>(SaveJson.Serializer());
            Validate(data);
            return data;
        }

        internal JObject Capture(T data)
        {
            Validate(data);
            var snapshot = JObject.FromObject(data, SaveJson.Serializer());
            Validate(snapshot.ToObject<T>(SaveJson.Serializer()));
            return snapshot;
        }

        internal override void Prepare(JObject record)
        {
            long version = SaveJson.Integer(record, "version");
            if (version > Version)
                throw new SaveError(SaveErrorCode.FutureVersion, "Section requires a newer client: " + Id);
            if (version < 1 || !(record["data"] is JObject))
                throw new SaveError(SaveErrorCode.InvalidSection, "Invalid section record: " + Id);
            while (version < Version)
            {
                if (!_migrations.TryGetValue((int)version, out var migrate))
                    throw new SaveError(SaveErrorCode.MissingMigration, "Missing migration for section: " + Id);
                var migrated = migrate((JObject)record["data"].DeepClone()) ??
                    throw new JsonException("Migration returned no data: " + Id);
                record["data"] = migrated.DeepClone();
                record["version"] = ++version;
            }
            Read(record);
        }

        private void Validate(T data)
        {
            if (data == null) throw new SaveError(SaveErrorCode.InvalidSection, "Null section: " + Id);
            string problem = _validate(data);
            if (problem != null) throw new SaveError(SaveErrorCode.InvalidSection, Id + ": " + problem);
        }
    }
}
