using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HomelessPetHelper.Modules.Siege.UI
{
    public sealed class SiegeText
    {
        [Serializable]
        private sealed class Table
        {
            public Entry[] entries = null;
        }

        [Serializable]
        private sealed class Entry
        {
            public string key = null,
                value = null;
        }

        private readonly Dictionary<string, string> _entries = new Dictionary<string, string>();

        public SiegeText(TextAsset source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var table = JsonUtility.FromJson<Table>(source.text);
            if (table?.entries == null)
                throw new ArgumentException("Missing siege text entries.");
            foreach (var item in table.entries)
            {
                if (
                    item == null
                    || string.IsNullOrWhiteSpace(item.key)
                    || string.IsNullOrWhiteSpace(item.value)
                    || _entries.ContainsKey(item.key)
                )
                    throw new ArgumentException("Invalid siege text.");
                _entries.Add(item.key, item.value);
            }
        }

        public string Get(string key) =>
            _entries.TryGetValue(key, out string value)
                ? value
                : throw new ArgumentException("Missing siege text: " + key);

        public string Format(string key, params object[] values) =>
            string.Format(CultureInfo.InvariantCulture, Get(key), values);

        public void Validate(SiegeCatalog c)
        {
            foreach (var u in c.Units)
            {
                Get(u.NameKey);
                Get(u.DetailKey);
            }
            foreach (var p in c.Profiles)
                Get(p.NameKey);
            foreach (var lane in c.Lanes)
                Get(lane.NameKey);
            foreach (var encounter in c.Content.Encounters)
            {
                Get(encounter.NameKey);
                Get(encounter.GuardNameKey);
                Get(encounter.SweepNameKey);
                if (encounter.HoldDurationMs > 0) Get(encounter.HoldNameKey);
            }
            foreach (var tower in c.Content.Towers)
                Get(tower.NameKey);
        }
    }
}
