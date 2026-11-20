using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace HomelessPetHelper.Framework.Audio.Persistence
{
    [Preserve, JsonObject(MemberSerialization.OptIn)]
    internal sealed class AudioPreferencesData
    {
        [Preserve, JsonProperty("masterVolume", Required = Required.Always)] public float MasterVolume = 1;
        [Preserve, JsonProperty("musicVolume", Required = Required.Always)] public float MusicVolume = 1;
        [Preserve, JsonProperty("effectsVolume", Required = Required.Always)] public float EffectsVolume = 1;
        [Preserve, JsonProperty("masterMuted", Required = Required.Always)] public bool MasterMuted;
        [Preserve, JsonProperty("musicMuted", Required = Required.Always)] public bool MusicMuted;
        [Preserve, JsonProperty("effectsMuted", Required = Required.Always)] public bool EffectsMuted;

        [Preserve]
        public AudioPreferencesData() { }

        public AudioPreferences ToPreferences() => new AudioPreferences(
            new AudioBusSettings(MasterVolume, MasterMuted), new AudioBusSettings(MusicVolume, MusicMuted),
            new AudioBusSettings(EffectsVolume, EffectsMuted));

        public static AudioPreferencesData Capture(AudioPreferences preferences) => new AudioPreferencesData
        {
            MasterVolume = preferences.Master.Volume,
            MusicVolume = preferences.Music.Volume,
            EffectsVolume = preferences.Effects.Volume,
            MasterMuted = preferences.Master.IsMuted,
            MusicMuted = preferences.Music.IsMuted,
            EffectsMuted = preferences.Effects.IsMuted
        };
    }
}
