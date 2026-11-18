using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Modules.Rescue.Options;
using UnityEngine;

namespace HomelessPetHelper.Modules.Rescue.UI
{
    // Parses explicit TextAssets once; no asset lookup or mutable gameplay state lives here.
    public sealed class RescueOptionContentLoader
    {
        [Serializable]
        private sealed class OptionFile
        {
            public int schemaVersion = 0;
            public OptionRow[] options = null;
        }

        [Serializable]
        private sealed class OptionRow
        {
            public string id = null;
            public string groupId = null;
            public string kind = null;
            public string titleKey = null;
            public string feedbackKey = null;
        }

        [Serializable]
        private sealed class TextFile
        {
            public int schemaVersion = 0;
            public TextRow[] entries = null;
        }

        [Serializable]
        private sealed class TextRow
        {
            public string key = null;
            public string value = null;
        }

        private readonly Dictionary<string, string> _text = new Dictionary<string, string>(StringComparer.Ordinal);
        public RescueOptionCatalog Catalog { get; }

        public RescueOptionContentLoader(TextAsset options, TextAsset text)
        {
            if (options == null || text == null)
            {
                throw new ArgumentException("Both option and localized text assets are required.");
            }

            OptionFile definitions = JsonUtility.FromJson<OptionFile>(options.text);
            TextFile localized = JsonUtility.FromJson<TextFile>(text.text);
            if (definitions == null || localized == null || definitions.schemaVersion != 1 ||
                localized.schemaVersion != 1 || definitions.options == null || localized.entries == null)
            {
                throw new ArgumentException("Invalid or unsupported rescue option content schema.");
            }

            Catalog = new RescueOptionCatalog(definitions.options.Select(ParseOption).ToArray());
            foreach (TextRow row in localized.entries)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.key) || row.key != row.key.Trim() ||
                    string.IsNullOrWhiteSpace(row.value) || _text.ContainsKey(row.key))
                {
                    throw new ArgumentException("Invalid or duplicate rescue option text key.");
                }

                // This first schema is literal text, not a format-string or rich-text scripting language.
                if (row.value.IndexOfAny(new[] { '{', '}', '<', '>' }) >= 0)
                {
                    throw new ArgumentException("Option text must be literal text: " + row.key);
                }

                _text.Add(row.key, row.value);
            }

            foreach (RescueOptionContent entry in Catalog.Entries)
            {
                GetText(entry.TitleKey);
                if (entry.Kind == RescueOptionKind.Flavor)
                {
                    GetText(entry.FeedbackKey);
                }
            }
        }

        public string GetText(string key)
        {
            if (key == null || !_text.TryGetValue(key, out string value))
            {
                throw new ArgumentException("Missing rescue option text: " + key, nameof(key));
            }

            return value;
        }

        private static RescueOptionContent ParseOption(OptionRow row)
        {
            if (row == null || !Enum.TryParse(row.kind, out RescueOptionKind kind) ||
                kind.ToString() != row.kind)
            {
                throw new ArgumentException("Unknown rescue option kind.");
            }

            return new RescueOptionContent(row.id, row.groupId, kind, row.titleKey, row.feedbackKey);
        }
    }
}
