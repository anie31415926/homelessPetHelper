using System;
using System.Collections.Generic;
using UnityEngine;

namespace HomelessPetHelper.Framework.Audio
{
    [CreateAssetMenu(menuName = "Homeless Pet Helper/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioCatalogEntry[] _entries = Array.Empty<AudioCatalogEntry>();

        public AudioCue[] CreateCues()
        {
            if (_entries == null) throw new InvalidOperationException("Audio catalog entries are missing.");
            var result = new AudioCue[_entries.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] == null) throw new InvalidOperationException("Null audio catalog entry at " + i);
                result[i] = _entries[i].CreateCue();
                if (!ids.Add(result[i].Id)) throw new InvalidOperationException("Duplicate audio cue: " + result[i].Id);
            }
            return result;
        }
    }
}
