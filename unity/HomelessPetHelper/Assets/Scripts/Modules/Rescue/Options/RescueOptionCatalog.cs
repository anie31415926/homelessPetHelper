using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HomelessPetHelper.Modules.Rescue.Options
{
    public sealed class RescueOptionCatalog
    {
        private readonly Dictionary<string, RescueOptionContent> _byId =
            new Dictionary<string, RescueOptionContent>(StringComparer.Ordinal);

        public IReadOnlyList<RescueOptionContent> Entries { get; }

        public RescueOptionCatalog(IReadOnlyList<RescueOptionContent> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                throw new ArgumentException("An option catalog must not be empty.", nameof(entries));
            }

            var copy = new List<RescueOptionContent>();
            var groups = new Dictionary<string, RescueOptionKind>(StringComparer.Ordinal);
            foreach (RescueOptionContent entry in entries)
            {
                if (entry == null || _byId.ContainsKey(entry.Id))
                {
                    throw new ArgumentException("Null or duplicate option content.", nameof(entries));
                }

                if (groups.TryGetValue(entry.GroupId, out var kind) && kind != entry.Kind)
                {
                    throw new ArgumentException("An option group cannot mix rescue and flavor kinds: " +
                        entry.GroupId, nameof(entries));
                }

                groups[entry.GroupId] = entry.Kind;
                copy.Add(entry);
                _byId.Add(entry.Id, entry);
            }

            Entries = new ReadOnlyCollection<RescueOptionContent>(copy);
        }

        public RescueOptionContent Get(string id)
        {
            if (id == null || !_byId.TryGetValue(id, out RescueOptionContent entry))
            {
                throw new ArgumentException("Unknown option content: " + id, nameof(id));
            }

            return entry;
        }
    }
}
