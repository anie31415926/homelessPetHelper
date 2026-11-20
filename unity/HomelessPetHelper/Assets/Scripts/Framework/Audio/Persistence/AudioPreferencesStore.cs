using System;
using System.Collections.Generic;
using HomelessPetHelper.Framework.Persistence;

namespace HomelessPetHelper.Framework.Audio.Persistence
{
    /// <summary>
    /// Explicit save adapter. No writes on load or lifecycle callbacks. Failed/corrupt loads block saving until
    /// a successful explicit reload; defaults for the current audio session must never overwrite original data.
    /// </summary>
    public sealed class AudioPreferencesStore
    {
        private readonly SaveService _saves;
        private readonly SaveSection<AudioPreferencesData> _section;
        private AudioPreferences _persisted;
        private bool _loaded;
        public bool CanSave => _loaded && _saves.CanWrite;
        public IReadOnlyList<SaveError> RecoveryWarnings => _saves.RecoveryWarnings;

        public AudioPreferencesStore(ISaveStorage storage)
        {
            _saves = new SaveService(storage);
            _section = new SaveSection<AudioPreferencesData>("audio.preferences", 1,
                () => new AudioPreferencesData(), Validate);
            _saves.Register(_section);
        }

        public bool TryLoad(out AudioPreferences preferences, out SaveError error)
        {
            preferences = null;
            _loaded = false;
            if (!_saves.TryOpen(out _, out error) || !_saves.TryRead(_section, out var data, out error)) return false;
            preferences = data.ToPreferences();
            _persisted = preferences;
            _loaded = true;
            return true;
        }

        public bool TrySave(AudioPreferences preferences, out SaveError error)
        {
            if (preferences == null) throw new ArgumentNullException(nameof(preferences));
            error = null;
            if (!CanSave)
            {
                error = new SaveError(SaveErrorCode.NotOpen, "Reload valid audio preferences before saving.");
                return false;
            }
            if (preferences.Equals(_persisted)) return true;
            var snapshot = AudioPreferencesData.Capture(preferences);
            if (!_saves.TryCommit(writer => writer.Set(_section, snapshot), out error)) return false;
            _persisted = preferences;
            return true;
        }

        private static string Validate(AudioPreferencesData data)
        {
            try { data.ToPreferences(); return null; }
            catch (ArgumentOutOfRangeException) { return "Audio volumes must be finite and within [0, 1]."; }
        }
    }
}
