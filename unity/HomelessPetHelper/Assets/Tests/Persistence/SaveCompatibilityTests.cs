using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HomelessPetHelper.Framework.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace HomelessPetHelper.Tests.Persistence
{
    public sealed class SaveCompatibilityTests
    {
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class State
        {
            [JsonProperty("value", Required = Required.Always)] public int Value;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class FailingDto
        {
            [JsonProperty("value")] public int Value => throw new IOException("Capture getter failed.");
        }

        private sealed class Storage : ISaveStorage
        {
            public readonly string[] Slots = new string[2];
            public int Writes;
            public string Read(int slot) => Slots[slot];
            public void Write(int slot, string content) { Slots[slot] = content; Writes++; }
            public void Archive(int slot) => Assert.Fail("No damaged archive expected.");
        }

        private static SaveSection<State> Section(int version = 1) => new SaveSection<State>("test", version,
            () => new State(), value => value.Value < 0 ? "Negative value." : null);

        private static void Replace(Storage storage, int slot, string payload)
        {
            string hash;
            using (var algorithm = SHA256.Create())
                hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                    .Replace("-", "").ToLowerInvariant();
            storage.Slots[slot] = new JObject { ["payload"] = payload, ["sha256"] = hash }
                .ToString(Formatting.None);
        }

        private static SaveService Create(Storage storage, SaveSection<State> section)
        {
            var service = new SaveService(storage);
            service.Register(section);
            return service;
        }

        [Test]
        public void FutureSectionInOlderSlotStillBlocksDowngradeWrites()
        {
            var storage = new Storage();
            var section = Section();
            var service = Create(storage, section);
            Assert.That(service.TryOpen(out _, out _), Is.True);
            Assert.That(service.TryCommit(writer => writer.Set(section, new State { Value = 1 }), out _), Is.True);
            Assert.That(service.TryCommit(writer => writer.Set(section, new State { Value = 2 }), out _), Is.True);
            var older = JObject.Parse(JObject.Parse(storage.Slots[0])["payload"].Value<string>());
            older["sections"][section.Id]["version"] = 9;
            Replace(storage, 0, older.ToString(Formatting.None));
            var downgraded = Create(storage, section);
            Assert.That(downgraded.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.FutureVersion));
            Assert.That(downgraded.CanWrite, Is.False);
            Assert.That(storage.Writes, Is.EqualTo(2));
        }

        [TestCase("{\"formatVersion\":1,\"formatVersion\":1,\"revision\":1,\"sections\":{}}")]
        [TestCase("{\"formatVersion\":1,\"revision\":0,\"sections\":{}}")]
        [TestCase("{\"formatVersion\":1,\"revision\":1,\"sections\":{}} true")]
        [TestCase("{\"formatVersion\":1,\"revision\":1,\"sections\":[]}")]
        public void AmbiguousOrMalformedPayloadsNeverBecomeDefaults(string payload)
        {
            var storage = new Storage();
            Replace(storage, 0, payload);
            var service = Create(storage, Section());
            Assert.That(service.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Corrupt));
            Assert.That(service.RecoveryWarnings.Count, Is.EqualTo(1));
            Assert.That(service.RecoveryWarnings[0].InnerException, Is.Not.Null);
            Assert.That(storage.Writes, Is.Zero);
        }

        [Test]
        public void TypeMetadataIsRejectedRatherThanInterpretedAsRuntimeType()
        {
            var storage = new Storage();
            Replace(storage, 0, "{\"formatVersion\":1,\"revision\":1,\"sections\":{" +
                "\"test\":{\"version\":1,\"data\":{\"$type\":\"untrusted.Type\",\"value\":3}}}}");
            var service = Create(storage, Section());
            Assert.That(service.TryOpen(out _, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(storage.Writes, Is.Zero);
        }

        [Test]
        public void FailedCaptureDoesNotTouchDiskAndServiceCanRetry()
        {
            var storage = new Storage();
            var section = Section();
            var service = Create(storage, section);
            Assert.That(service.TryOpen(out _, out _), Is.True);
            Assert.That(service.TryCommit(writer =>
            {
                writer.Set(section, new State { Value = 10 });
                throw new IOException("Capture failed.");
            }, out var error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(error.InnerException, Is.TypeOf<IOException>());
            Assert.That(storage.Writes, Is.Zero);
            Assert.That(service.Revision, Is.Zero);
            Assert.That(service.CanWrite, Is.True);
            Assert.That(service.TryCommit(writer => writer.Set(section, new State { Value = 2 }), out _), Is.True);
        }

        [Test]
        public void SharedDefaultFactoryCannotLeakMutableStateAcrossReads()
        {
            var shared = new State { Value = 3 };
            var section = new SaveSection<State>("shared", 1, () => shared, value => null);
            var service = Create(new Storage(), section);
            Assert.That(service.TryOpen(out _, out _), Is.True);
            Assert.That(service.TryRead(section, out var read, out _), Is.True);
            read.Value = 20;
            Assert.That(shared.Value, Is.EqualTo(3));
            Assert.That(service.TryRead(section, out read, out _), Is.True);
            Assert.That(read.Value, Is.EqualTo(3));
        }

        [Test]
        public void UnregisteredEquivalentDefinitionCannotReadOrWriteASection()
        {
            var storage = new Storage();
            var service = Create(storage, Section());
            Assert.That(service.TryOpen(out _, out _), Is.True);
            Assert.That(service.TryRead(Section(), out _, out var readError), Is.False);
            Assert.That(readError.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(service.TryCommit(writer => writer.Set(Section(), new State()), out _), Is.False);
            Assert.That(storage.Writes, Is.Zero);
        }

        [Test]
        public void SerializationFailureDoesNotReturnAPartialDto()
        {
            var service = new SaveService(new Storage());
            var section = new SaveSection<FailingDto>("failing", 1, () => new FailingDto(), data => null);
            service.Register(section);
            Assert.That(service.TryOpen(out _, out _), Is.True);
            Assert.That(service.TryRead(section, out var data, out var error), Is.False);
            Assert.That(data, Is.Null);
            Assert.That(error.Code, Is.EqualTo(SaveErrorCode.InvalidSection));
            Assert.That(error.InnerException, Is.Not.Null);
        }

        [Test]
        public void ReadsCannotReenterAnActiveCheckpoint()
        {
            var section = Section();
            var service = Create(new Storage(), section);
            Assert.That(service.TryOpen(out _, out _), Is.True);
            Assert.That(service.TryCommit(writer =>
            {
                Assert.That(service.TryRead(section, out var data, out var error), Is.False);
                Assert.That(data, Is.Null);
                Assert.That(error.Code, Is.EqualTo(SaveErrorCode.Busy));
                writer.Set(section, new State { Value = 2 });
            }, out _), Is.True);
        }
    }
}
