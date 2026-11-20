using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HomelessPetHelper.Framework.Audio;
using HomelessPetHelper.Framework.Audio.Persistence;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HomelessPetHelper.Tests.Audio.EditMode
{
    public sealed class AudioPreferencesTests
    {
        private sealed class Storage : ISaveStorage
        {
            public readonly string[] Slots = new string[2];
            public int Writes, Archives;
            public bool FailWrite;
            public string Read(int slot) => Slots[slot];
            public void Write(int slot, string content)
            {
                if (FailWrite) throw new IOException("Storage unavailable.");
                Slots[slot] = content;
                Writes++;
            }
            public void Archive(int slot) => Archives++;
        }

        private static AudioPreferences Custom() => new AudioPreferences(new AudioBusSettings(0.9f, true),
            new AudioBusSettings(0.3f), new AudioBusSettings(0.7f, true));

        [Test]
        public void LoadAndUnchangedSaveDoNotWriteButChangesSurviveANewStore()
        {
            var storage = new Storage();
            var store = new AudioPreferencesStore(storage);
            Assert.That(store.TryLoad(out var initial, out _), Is.True);
            Assert.That(initial, Is.EqualTo(AudioPreferences.Default));
            Assert.That(store.TrySave(initial, out _), Is.True);
            Assert.That(storage.Writes, Is.Zero);
            Assert.That(store.TrySave(Custom(), out _), Is.True);
            Assert.That(store.TrySave(Custom(), out _), Is.True);
            Assert.That(storage.Writes, Is.EqualTo(1));
            var restarted = new AudioPreferencesStore(storage);
            Assert.That(restarted.TryLoad(out var restored, out _), Is.True);
            Assert.That(restored, Is.EqualTo(Custom()));
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void CorruptPreferencesNeverGetOverwrittenWithDefaults()
        {
            var storage = new Storage();
            storage.Slots[0] = "damaged preference evidence";
            var store = new AudioPreferencesStore(storage);
            Assert.That(store.TryLoad(out var preferences, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Corrupt));
            Assert.That(preferences, Is.Null);
            Assert.That(store.TrySave(AudioPreferences.Default, out _), Is.False);
            Assert.That(storage.Writes, Is.Zero);
            Assert.That(storage.Slots[0], Is.EqualTo("damaged preference evidence"));
        }

        [Test]
        public void WriteFailureRequiresExplicitReloadBeforeSavingAgain()
        {
            var storage = new Storage();
            var store = new AudioPreferencesStore(storage);
            Assert.That(store.TryLoad(out _, out _), Is.True);
            storage.FailWrite = true;
            Assert.That(store.TrySave(Custom(), out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.WriteUncertain));
            storage.FailWrite = false;
            Assert.That(store.TrySave(Custom(), out _), Is.False);
            Assert.That(store.TryLoad(out var original, out _), Is.True);
            Assert.That(original, Is.EqualTo(AudioPreferences.Default));
            Assert.That(store.TrySave(Custom(), out _), Is.True);
        }

        [TestCase("musicVolume", -1)]
        [TestCase("effectsVolume", 2)]
        public void ValidChecksumDoesNotBypassVolumeRangeValidation(string field, float value)
        {
            var storage = new Storage();
            var store = new AudioPreferencesStore(storage);
            Assert.That(store.TryLoad(out _, out _), Is.True);
            Assert.That(store.TrySave(Custom(), out _), Is.True);
            Rewrite(storage, document => document["sections"]["audio.preferences"]["data"][field] = value);
            var restarted = new AudioPreferencesStore(storage);
            Assert.That(restarted.TryLoad(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(restarted.CanSave, Is.False);
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void FutureVersionAndMissingRequiredFieldsAreNotSilentlyReset()
        {
            var storage = new Storage();
            var store = new AudioPreferencesStore(storage);
            Assert.That(store.TryLoad(out _, out _), Is.True);
            Assert.That(store.TrySave(Custom(), out _), Is.True);
            string before = storage.Slots[0];
            Rewrite(storage, document => document["sections"]["audio.preferences"]["version"] = 2);
            Assert.That(new AudioPreferencesStore(storage).TryLoad(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.FutureVersion));
            storage.Slots[0] = before;
            Rewrite(storage, document => ((JObject)document["sections"]["audio.preferences"]["data"])
                .Remove("masterVolume"));
            Assert.That(new AudioPreferencesStore(storage).TryLoad(out _, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
        }

        [Test]
        public void OlderValidPreferencesRecoverWithoutWritingDuringLoad()
        {
            var storage = new Storage();
            var store = new AudioPreferencesStore(storage);
            Assert.That(store.TryLoad(out _, out _), Is.True);
            Assert.That(store.TrySave(Custom(), out _), Is.True);
            Assert.That(store.TrySave(AudioPreferences.Default, out _), Is.True);
            storage.Slots[1] = "truncated";
            var restarted = new AudioPreferencesStore(storage);
            Assert.That(restarted.TryLoad(out var restored, out _), Is.True);
            Assert.That(restored, Is.EqualTo(Custom()));
            Assert.That(storage.Writes, Is.EqualTo(2));
            Assert.That(storage.Archives, Is.Zero);
            Assert.That(restarted.RecoveryWarnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void MainSessionAppliesImmediatelyButPersistsOnlyAtExplicitCheckpoint()
        {
            var parent = new GameObject("AudioSettingsOwnerTest");
            var storage = new Storage();
            try
            {
                using (var session = new MainAudioSession(parent.transform, null, storage))
                {
                    Assert.That(session.PersistenceError, Is.Null);
                    session.Playback.ApplyPreferences(Custom());
                    Assert.That(session.Playback.Preferences, Is.EqualTo(Custom()));
                    Assert.That(session.HasUnsavedPreferences, Is.True);
                    Assert.That(storage.Writes, Is.Zero);
                    Assert.That(session.TrySavePreferences(out _), Is.True);
                    Assert.That(session.HasUnsavedPreferences, Is.False);
                }
                using (var restarted = new MainAudioSession(parent.transform, null, storage))
                    Assert.That(restarted.Playback.Preferences, Is.EqualTo(Custom()));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        [Test]
        public void FailedMainSessionSaveKeepsLivePreferencesAndOriginalBytes()
        {
            var parent = new GameObject("AudioSettingsOwnerTest");
            var storage = new Storage();
            storage.Slots[0] = "original damaged preferences";
            try
            {
                using (var session = new MainAudioSession(parent.transform, null, storage))
                {
                    Assert.That(session.PersistenceError.Code, Is.EqualTo(SaveErrorCode.Corrupt));
                    session.Playback.ApplyPreferences(Custom());
                    Assert.That(session.TrySavePreferences(out _), Is.False);
                    Assert.That(session.Playback.Preferences, Is.EqualTo(Custom()));
                    Assert.That(session.HasUnsavedPreferences, Is.True);
                    Assert.That(session.TryReloadPreferences(out _), Is.False);
                    Assert.That(session.Playback.Preferences, Is.EqualTo(Custom()));
                }
                Assert.That(storage.Writes, Is.Zero);
                Assert.That(storage.Slots[0], Is.EqualTo("original damaged preferences"));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        private static void Rewrite(Storage storage, Action<JObject> update)
        {
            var document = JObject.Parse(JObject.Parse(storage.Slots[0])["payload"].Value<string>());
            update(document);
            string payload = document.ToString(Formatting.None);
            string hash;
            using (var algorithm = SHA256.Create())
                hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                    .Replace("-", "").ToLowerInvariant();
            storage.Slots[0] = new JObject { ["payload"] = payload, ["sha256"] = hash }.ToString(Formatting.None);
        }
    }
}
