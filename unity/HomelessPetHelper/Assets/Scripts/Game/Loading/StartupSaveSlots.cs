using System;
using System.Collections.Generic;
using System.IO;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Framework.Persistence.WeChat;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HomelessPetHelper.Game.Loading
{
    internal static class StartupSaveSlots
    {
        internal const int Count = 3;
        internal const string ProgressSection = "main.station";
        internal const int ProgressVersion = 6;
        internal static readonly SaveSection<JObject> Summary = new SaveSection<JObject>(
            "main.summary", 1, () => new JObject(), _ => null);

        internal enum State { Empty, Saved, Unavailable }
        internal readonly struct Info
        {
            public readonly State Status;
            public readonly DateTimeOffset? SavedAt;
            public readonly int? Rescued;
            public Info(State status, DateTimeOffset? savedAt = null, int? rescued = null)
            { Status = status; SavedAt = savedAt; Rescued = rescued; }
        }

        internal static (int slot, bool create, ISaveStorage storage)? Pending { get; set; }
        internal static bool EntryFailed { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Pending = null; EntryFailed = false; }

        internal static ISaveStorage OpenStorage(int slot)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            const string key = "homeless-pet.main.v1";
            return new WeChatSaveStorage(slot == 0 ? key : key + ".profile-" + slot);
#else
            const string folder = "main-progress";
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath,
                slot == 0 ? folder : folder + "-profile-" + slot));
#endif
        }

        internal static Info Inspect(ISaveStorage storage)
        {
            // Read a checked, detached snapshot. Listing a slot never performs gameplay migrations or writes.
            var versions = new Dictionary<int, Func<JObject, JObject>>();
            for (int version = 1; version < ProgressVersion; version++) versions.Add(version, data => data);
            var progress = new SaveSection<JObject>(ProgressSection, ProgressVersion, () => new JObject(),
                _ => null, versions);
            var saves = new SaveService(storage);
            saves.Register(progress);
            saves.Register(Summary);
            if (!saves.TryOpen(out var status, out _)) return new Info(State.Unavailable);
            if (status == SaveOpenStatus.New) return new Info(State.Empty);
            if (!saves.TryRead(progress, out var data, out _) || !data.HasValues ||
                !saves.TryRead(Summary, out var summary, out _)) return new Info(State.Unavailable);
            var timestamp = summary["savedUtc"];
            DateTimeOffset? savedAt = timestamp?.Type == JTokenType.String &&
                DateTimeOffset.TryParseExact(timestamp.Value<string>(), "O", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : (DateTimeOffset?)null;
            int? rescued = data["Residents"] is JArray residents
                ? residents.Count + ((data["Memorials"] as JArray)?.Count ?? 0) : null;
            return new Info(State.Saved, savedAt, rescued);
        }
    }
}
