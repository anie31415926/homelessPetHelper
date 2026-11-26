using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Framework.Persistence.WeChat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.Persistence
{
    public sealed class SaveServiceTests
    {
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class Progress
        {
            [JsonProperty("completed", Required = Required.Always)] public int Completed;
        }

        private sealed class MemoryStorage : ISaveStorage
        {
            public readonly string[] Slots = new string[2];
            public readonly List<string> Archives = new List<string>();
            public int Writes;
            public bool ReadFails;
            public bool ArchiveFails;
            public bool FailAfterWrite;
            public bool TruncateWrite;
            public string Read(int slot)
            {
                if (ReadFails) throw new IOException("Read denied.");
                return Slots[slot];
            }
            public void Write(int slot, string content)
            {
                Writes++;
                Slots[slot] = TruncateWrite ? content.Substring(0, content.Length / 2) : content;
                if (FailAfterWrite) throw new IOException("Completion unknown.");
            }
            public void Archive(int slot)
            {
                if (ArchiveFails) throw new IOException("Archive denied.");
                Archives.Add(Slots[slot]);
            }
        }

        private sealed class ResettableStorage : IDeletableSaveStorage
        {
            public readonly string[] Slots = new string[2];
            public readonly List<string> Archives = new List<string>();
            public bool FailArchive;
            public string Read(int slot) => Slots[slot];
            public void Write(int slot, string content) => Slots[slot] = content;
            public void Delete(int slot) => Slots[slot] = null;
            public void Archive(int slot)
            {
                if (FailArchive) throw new IOException("Archive denied.");
                Archives.Add(Slots[slot]);
            }
        }

        private static SaveSection<Progress> Section(string id = "progress", int version = 1,
            IDictionary<int, Func<JObject, JObject>> migrations = null) => new SaveSection<Progress>(id,
                version, () => new Progress(), data => data.Completed < 0 ? "Negative progress." : null, migrations);

        private static SaveService Service(ISaveStorage storage, params SaveSection<Progress>[] sections)
        {
            var service = new SaveService(storage);
            foreach (var section in sections) service.Register(section);
            return service;
        }

        private static void Open(SaveService service, SaveOpenStatus expected)
        {
            Assert.That(service.TryOpen(out var status, out var error), Is.True, error?.ToString());
            Assert.That(status, Is.EqualTo(expected));
        }

        private static int Read(SaveService service, SaveSection<Progress> section)
        {
            Assert.That(service.TryRead(section, out var data, out var error), Is.True, error?.ToString());
            return data.Completed;
        }

        private static void Commit(SaveService service, SaveSection<Progress> section, int value)
        {
            Assert.That(service.TryCommit(writer => writer.Set(section, new Progress { Completed = value }),
                out var error), Is.True, error?.ToString());
        }

        [Test]
        public void DeleteRemovesBothFileCheckpointsAndAllowsTheProfileToBeReused()
        {
            string parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(Path.Combine(parent, "target"));
                var other = new FileSaveStorage(Path.Combine(parent, "other"));
                other.Write(0, "unrelated profile");
                var section = Section();
                var save = Service(storage, section);
                Open(save, SaveOpenStatus.New);
                Commit(save, section, 1);
                Commit(save, section, 2);

                Open(save, SaveOpenStatus.Loaded);
                Assert.That(save.TryDelete(out var error), Is.True, error?.ToString());
                Assert.That(storage.Read(0), Is.Null);
                Assert.That(storage.Read(1), Is.Null);
                Assert.That(other.Read(0), Is.EqualTo("unrelated profile"));
                Open(save, SaveOpenStatus.New);
                Commit(save, section, 3);
                Assert.That(save.Revision, Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(parent)) Directory.Delete(parent, true);
            }
        }

        [Test]
        public void ResetArchivesBothCopiesBeforeRemovingAnUnreadableProfile()
        {
            var storage = new ResettableStorage();
            storage.Slots[0] = "first copy";
            storage.Slots[1] = "torn copy";
            Assert.That(SaveService.TryReset(storage, out var error), Is.True, error?.ToString());
            Assert.That(storage.Slots, Is.All.Null);
            Assert.That(storage.Archives, Is.EqualTo(new[] { "first copy", "torn copy" }));
            var section = Section();
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.New);
            Commit(service, section, 5);
            Assert.That(storage.Slots[0], Is.Not.Null);
        }

        [Test]
        public void FailedResetArchiveKeepsBothOriginals()
        {
            var storage = new ResettableStorage { FailArchive = true };
            storage.Slots[0] = "keep me";
            storage.Slots[1] = "keep me too";
            Assert.That(SaveService.TryReset(storage, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.WriteUncertain));
            Assert.That(storage.Slots, Is.EqualTo(new[] { "keep me", "keep me too" }));
        }

        [Test]
        public void StorageWithoutDeletionSupportCannotReset()
        {
            var storage = new MemoryStorage();
            storage.Slots[0] = "unremovable";
            Assert.That(SaveService.TryReset(storage, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.NotOpen));
            Assert.That(storage.Slots[0], Is.EqualTo("unremovable"));
        }

        [Test]
        public void NewProfileDoesNotWriteUntilExplicitCommitAndSurvivesNewService()
        {
            var storage = new MemoryStorage();
            var section = Section();
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.New);
            Assert.That(storage.Writes, Is.Zero);
            Assert.That(Read(service, section), Is.Zero);
            Commit(service, section, 3);
            Assert.That(storage.Writes, Is.EqualTo(1));
            var restarted = Service(storage, section);
            Open(restarted, SaveOpenStatus.Loaded);
            Assert.That(Read(restarted, section), Is.EqualTo(3));
        }

        [Test]
        public void MultipleSectionsCommitTogetherAndReadsAreDetached()
        {
            var storage = new MemoryStorage();
            var progress = Section();
            var settings = Section("settings");
            var service = Service(storage, progress, settings);
            Open(service, SaveOpenStatus.New);
            Assert.That(service.TryCommit(writer =>
            {
                writer.Set(progress, new Progress { Completed = 2 });
                writer.Set(settings, new Progress { Completed = 7 });
            }, out _), Is.True);
            Assert.That(storage.Writes, Is.EqualTo(1));
            Assert.That(service.TryRead(progress, out var detached, out _), Is.True);
            detached.Completed = 99;
            Assert.That(Read(service, progress), Is.EqualTo(2));
            var restarted = Service(storage, progress, settings);
            Open(restarted, SaveOpenStatus.Loaded);
            Assert.That(Read(restarted, progress), Is.EqualTo(2));
            Assert.That(Read(restarted, settings), Is.EqualTo(7));
        }

        [Test]
        public void FailedSecondSectionAbortsTheWholeCheckpoint()
        {
            var storage = new MemoryStorage();
            var first = Section();
            var second = Section("other");
            var service = Service(storage, first, second);
            Open(service, SaveOpenStatus.New);
            Commit(service, first, 1);
            string before = storage.Slots[0];
            Assert.That(service.TryCommit(writer =>
            {
                writer.Set(first, new Progress { Completed = 9 });
                writer.Set(second, new Progress { Completed = -1 });
            }, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(Read(service, first), Is.EqualTo(1));
            Assert.That(storage.Slots[0], Is.EqualTo(before));
            Assert.That(storage.Slots[1], Is.Null);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TornLatestSlotFallsBackAndPreservesDamagedEvidenceBeforeReuse(int unused)
        {
            var storage = new MemoryStorage();
            var section = Section();
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.New);
            Commit(service, section, 2);
            Commit(service, section, 3);
            if (unused == 1) Commit(service, section, 4);
            int damagedSlot = unused == 0 ? 1 : 0;
            storage.Slots[damagedSlot] = "truncated original evidence";
            var restarted = Service(storage, section);
            Open(restarted, SaveOpenStatus.Recovered);
            Assert.That(Read(restarted, section), Is.EqualTo(unused == 0 ? 2 : 3));
            Assert.That(storage.Archives, Is.Empty, "Open must remain read-only.");
            Commit(restarted, section, 5);
            Assert.That(storage.Archives, Is.EqualTo(new[] { "truncated original evidence" }));
        }

        [Test]
        public void BothDamagedSlotsBlockDefaultsAndAllWrites()
        {
            var storage = new MemoryStorage();
            storage.Slots[0] = "bad";
            storage.Slots[1] = "";
            var service = Service(storage, Section());
            Assert.That(service.TryOpen(out var status, out var error), Is.False);
            Assert.That(status, Is.EqualTo(SaveOpenStatus.Blocked));
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Corrupt));
            Assert.That(service.TryCommit(_ => { }, out _), Is.False);
            Assert.That(storage.Writes, Is.Zero);
            Assert.That(storage.Slots, Is.EqualTo(new[] { "bad", "" }));
        }

        [Test]
        public void ReadFailureIsNotMistakenForANewProfile()
        {
            var storage = new MemoryStorage { ReadFails = true };
            var service = Service(storage, Section());
            Assert.That(service.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.StorageReadFailed));
            Assert.That(service.CanWrite, Is.False);
        }

        [Test]
        public void ChecksumDetectsAccidentalModification()
        {
            var storage = Seed(out var section);
            storage.Slots[0] = storage.Slots[0].Replace("completed\\\":1", "completed\\\":2");
            Assert.That(Service(storage, section).TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Corrupt));
        }

        [Test]
        public void UnknownSectionsAndDocumentMetadataSurviveOtherModuleUpdates()
        {
            var storage = Seed(out var first);
            Rewrite(storage, 0, document =>
            {
                document["futureMetadata"] = "keep";
                document["sections"]["not-installed"] = new JObject
                {
                    ["version"] = 73, ["data"] = new JObject { ["untouched"] = 8 }
                };
            });
            var service = Service(storage, first);
            Open(service, SaveOpenStatus.Loaded);
            Commit(service, first, 2);
            var result = Payload(storage.Slots[1]);
            Assert.That(result["futureMetadata"].Value<string>(), Is.EqualTo("keep"));
            Assert.That(result["sections"]["not-installed"]["version"].Value<int>(), Is.EqualTo(73));
        }

        [Test]
        public void UnknownFieldsInKnownDtoBlockInsteadOfDisappearing()
        {
            var storage = Seed(out var section);
            Rewrite(storage, 0, document => document["sections"][section.Id]["data"]["future"] = 9);
            var service = Service(storage, section);
            Assert.That(service.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(service.CanWrite, Is.False);
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void MissingRequiredDtoFieldBlocksInsteadOfLoadingZero()
        {
            var storage = Seed(out var section);
            Rewrite(storage, 0, document =>
                ((JObject)document["sections"][section.Id]["data"]).Remove("completed"));
            Assert.That(Service(storage, section).TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FutureEnvelopeOrKnownSectionVersionBlocksEvenWithOlderBackup(bool envelope)
        {
            var storage = Seed(out var section);
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.Loaded);
            Commit(service, section, 2);
            Rewrite(storage, 1, document =>
            {
                if (envelope) document["formatVersion"] = 99;
                else document["sections"][section.Id]["version"] = 99;
            });
            var restarted = Service(storage, section);
            Assert.That(restarted.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.FutureVersion));
            Assert.That(restarted.CanWrite, Is.False);
            Assert.That(storage.Writes, Is.EqualTo(2));
        }

        [Test]
        public void MigrationRunsOnCopiesAndIsOnlyPersistedAtExplicitCommit()
        {
            var storage = Seed(out _);
            var section = Section(version: 3, migrations: new Dictionary<int, Func<JObject, JObject>>
            {
                [1] = data => { data["completed"] = data["completed"].Value<int>() + 2; return data; },
                [2] = data => { data["completed"] = data["completed"].Value<int>() + 3; return data; }
            });
            string original = storage.Slots[0];
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.Loaded);
            Assert.That(Read(service, section), Is.EqualTo(6));
            Assert.That(storage.Slots[0], Is.EqualTo(original));
            Assert.That(storage.Writes, Is.EqualTo(1));
            Open(service, SaveOpenStatus.Loaded);
            Assert.That(Read(service, section), Is.EqualTo(6),
                "Reopen repeats from the original, not migrated memory.");
            Assert.That(service.TryCommit(_ => { }, out _), Is.True);
            var restarted = Service(storage, section);
            Open(restarted, SaveOpenStatus.Loaded);
            Assert.That(Read(restarted, section), Is.EqualTo(6));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MissingOrThrowingMigrationLeavesOriginalSaveUntouched(bool missing)
        {
            var storage = Seed(out _);
            string original = storage.Slots[0];
            var migrations = new Dictionary<int, Func<JObject, JObject>>();
            if (!missing) migrations[1] = data =>
            {
                data["completed"] = 99;
                throw new IOException("Migration failed.");
            };
            var service = Service(storage, Section(version: 2, migrations: migrations));
            Assert.That(service.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(missing ?
                SaveErrorCode.MissingMigration : SaveErrorCode.InvalidSection));
            Assert.That(storage.Slots[0], Is.EqualTo(original));
            Assert.That(service.CanWrite, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UncertainWriteBlocksUntilReopenAndSelectsLatestValidCheckpoint(bool truncate)
        {
            var storage = Seed(out var section);
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.Loaded);
            storage.TruncateWrite = truncate;
            storage.FailAfterWrite = !truncate;
            Assert.That(service.TryCommit(writer => writer.Set(section, new Progress { Completed = 7 }),
                out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.WriteUncertain));
            Assert.That(service.CanWrite, Is.False);
            Assert.That(service.TryCommit(_ => { }, out _), Is.False);
            storage.TruncateWrite = false;
            storage.FailAfterWrite = false;
            Open(service, truncate ? SaveOpenStatus.Recovered : SaveOpenStatus.Loaded);
            Assert.That(Read(service, section), Is.EqualTo(truncate ? 1 : 7));
        }

        [Test]
        public void FailedDamagedArchiveBlocksWriteAndKeepsAllOriginalEvidence()
        {
            var storage = Seed(out var section);
            storage.Slots[1] = "damage";
            storage.ArchiveFails = true;
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.Recovered);
            Assert.That(service.TryCommit(writer => writer.Set(section, new Progress { Completed = 5 }),
                out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.WriteUncertain));
            Assert.That(storage.Slots[1], Is.EqualTo("damage"));
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void StaleServiceCannotOverwriteAnotherWriter()
        {
            var storage = Seed(out var section);
            var stale = Service(storage, section);
            var current = Service(storage, section);
            Open(stale, SaveOpenStatus.Loaded);
            Open(current, SaveOpenStatus.Loaded);
            Commit(current, section, 4);
            Assert.That(stale.TryCommit(writer => writer.Set(section, new Progress { Completed = 9 }),
                out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Conflict));
            Assert.That(storage.Writes, Is.EqualTo(2));
            Assert.That(stale.CanWrite, Is.False);
        }

        [Test]
        public void SameRevisionDifferentPayloadIsAnExplicitConflict()
        {
            var storage = Seed(out var section);
            storage.Slots[1] = storage.Slots[0];
            Rewrite(storage, 1, document => document["sections"][section.Id]["data"]["completed"] = 22);
            Assert.That(Service(storage, section).TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Conflict));
        }

        [Test]
        public void WriterCannotEscapeTransactionAndReentryCannotWrite()
        {
            var storage = new MemoryStorage();
            var section = Section();
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.New);
            SaveWriter escaped = null;
            Assert.That(service.TryCommit(writer =>
            {
                escaped = writer;
                Assert.That(service.TryCommit(_ => { }, out var nested), Is.False);
                Assert.That(nested.Code, Is.EqualTo(SaveErrorCode.Busy));
                Assert.That(service.TryOpen(out _, out var reopen), Is.False);
                Assert.That(reopen.Code, Is.EqualTo(SaveErrorCode.Busy));
                writer.Set(section, new Progress { Completed = 3 });
            }, out _), Is.True);
            Assert.Throws<InvalidOperationException>(() => escaped.Set(section, new Progress()));
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateOrLateRegistrationIsRejected()
        {
            var section = Section();
            var service = Service(new MemoryStorage(), section);
            Assert.Throws<ArgumentException>(() => service.Register(Section()));
            Open(service, SaveOpenStatus.New);
            Assert.Throws<InvalidOperationException>(() => service.Register(Section("other")));
        }

        [Test]
        public void RevisionOverflowDoesNotWriteOrInvokeCapture()
        {
            var storage = Seed(out var section);
            Rewrite(storage, 0, document => document["revision"] = long.MaxValue);
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.Loaded);
            Assert.That(service.TryCommit(_ => Assert.Fail("Capture should not run."), out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.RevisionOverflow));
            Assert.That(storage.Writes, Is.EqualTo(1));
        }

        [Test]
        public void RealFilesSurviveRestartAndKeepVerifiedDamagedCopy()
        {
            string directory = Path.Combine(Path.GetTempPath(), "hph-save-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new FileSaveStorage(directory);
                var section = Section();
                var service = Service(storage, section);
                Open(service, SaveOpenStatus.New);
                Commit(service, section, 4);
                Commit(service, section, 8);
                File.WriteAllText(Path.Combine(directory, "save-1.json"), "damaged file");
                var restarted = Service(new FileSaveStorage(directory), section);
                Open(restarted, SaveOpenStatus.Recovered);
                Assert.That(Read(restarted, section), Is.EqualTo(4));
                Commit(restarted, section, 6);
                var archives = Directory.GetFiles(directory, "*.damaged-*");
                Assert.That(archives.Length, Is.EqualTo(1));
                Assert.That(File.ReadAllText(archives[0]), Is.EqualTo("damaged file"));
                var final = Service(new FileSaveStorage(directory), section);
                Open(final, SaveOpenStatus.Loaded);
                Assert.That(Read(final, section), Is.EqualTo(6));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void WeChatAdapterNeverPretendsEditorMemoryIsPersistentStorage()
        {
            var service = Service(new WeChatSaveStorage("tests"), Section());
            Assert.That(service.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.StorageReadFailed));
            Assert.That(error.InnerException, Is.TypeOf<PlatformNotSupportedException>());
        }

        private static MemoryStorage Seed(out SaveSection<Progress> section)
        {
            section = Section();
            var storage = new MemoryStorage();
            var service = Service(storage, section);
            Open(service, SaveOpenStatus.New);
            Commit(service, section, 1);
            return storage;
        }

        private static JObject Payload(string envelope) =>
            JObject.Parse(JObject.Parse(envelope)["payload"].Value<string>());

        private static void Rewrite(MemoryStorage storage, int slot, Action<JObject> change)
        {
            var payload = Payload(storage.Slots[slot]);
            change(payload);
            string text = payload.ToString(Formatting.None);
            string hash;
            using (var algorithm = SHA256.Create())
                hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", "").ToLowerInvariant();
            storage.Slots[slot] = new JObject { ["payload"] = text, ["sha256"] = hash }.ToString(Formatting.None);
        }
    }
}
