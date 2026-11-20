using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace HomelessPetHelper.Framework.Persistence
{
    /// <summary>Valid only during TryCommit. Set captures a detached DTO; it does not write to storage.</summary>
    public sealed class SaveWriter
    {
        private readonly JObject _sections;
        private readonly IReadOnlyDictionary<string, SaveSectionDefinition> _definitions;
        private bool _active = true;

        internal SaveWriter(JObject sections, IReadOnlyDictionary<string, SaveSectionDefinition> definitions)
        {
            _sections = sections;
            _definitions = definitions;
        }

        public void Set<T>(SaveSection<T> section, T data) where T : class
        {
            if (!_active) throw new InvalidOperationException("The save transaction has ended.");
            if (section == null || !_definitions.TryGetValue(section.Id, out var registered) ||
                !ReferenceEquals(section, registered))
                throw new ArgumentException("Register this exact section definition before opening the save.");
            var record = _sections[section.Id] as JObject ?? new JObject();
            record["data"] = section.Capture(data);
            record["version"] = section.Version;
            _sections[section.Id] = record;
        }

        internal void Close() => _active = false;
    }
}
