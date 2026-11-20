using System;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence
{
    public abstract class SaveSectionDefinition
    {
        public string Id { get; }
        public int Version { get; }

        protected SaveSectionDefinition(string id, int version)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new ArgumentException("A stable section ID of at most 128 characters is required.");
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            Id = id;
            Version = version;
        }

        internal abstract void Prepare(JObject record);
    }
}
