using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HomelessPetHelper.Game.Text
{
    public sealed class OptionPreviewText
    {
        [Serializable]
        private sealed class Table
        {
            public Entry[] entries = Array.Empty<Entry>();
        }

        [Serializable]
        private sealed class Entry
        {
            public string key = string.Empty;
            public string value = string.Empty;
        }

        private readonly Dictionary<string, string> _entries = new Dictionary<string, string>();

        public OptionPreviewText(TextAsset source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Table table = JsonUtility.FromJson<Table>(source.text);
            if (table == null || table.entries == null)
            {
                throw new ArgumentException("Invalid preview text table.", nameof(source));
            }

            foreach (Entry entry in table.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) ||
                    string.IsNullOrWhiteSpace(entry.value) || _entries.ContainsKey(entry.key))
                {
                    throw new ArgumentException("Invalid or duplicate preview text key.", nameof(source));
                }

                _entries.Add(entry.key, entry.value);
            }
        }

        public bool TryGet(string key, out string value) => _entries.TryGetValue(key, out value);

        public string Get(string key, params object[] values)
        {
            if (!_entries.TryGetValue(key, out string value))
            {
                throw new ArgumentException("Missing preview text key: " + key, nameof(key));
            }

            return string.Format(CultureInfo.CurrentCulture, value, values);
        }
    }
}
