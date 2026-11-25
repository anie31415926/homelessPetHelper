using System;
using System.Linq;
using HomelessPetHelper.Framework.Persistence;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class LocationProgressTests
    {
        private GameContent Content => AssetDatabase.LoadAssetAtPath<GameContent>(
            "Assets/Settings/Game/GameContent.asset");

        [Test]
        public void OpeningCheckpointPreservesMovedCratesBridgeAndUndoAndDoesNotShareMutableArrays()
        {
            var field = Content.CreateFields().Find("parking", "parking.pockets");
            var catalog = field.Opening;
            using var source = new ApproachSession(Guid.NewGuid(), 3, catalog.Get(ApproachMode.Puzzle), catalog);
            Assert.That(source.TrySlideBox(46, 47, out _), Is.True);
            Assert.That(source.TrySlideBox(45, 46, out _), Is.True);
            Assert.That(source.TryPlaceBridge(38, out _), Is.True);
            var checkpoint = source.CaptureCheckpoint();
            using var restored = new ApproachSession(Guid.NewGuid(), 3, catalog.Get(ApproachMode.Puzzle), catalog);
            restored.RestoreCheckpoint(checkpoint);
            Assert.That(restored.IsComplete, Is.True);
            Assert.That(restored.HasBox(47), Is.True);
            Assert.That(restored.TryUndo(out _), Is.True);
            Assert.That(restored.BridgeCell, Is.EqualTo(-1));
            Assert.That(source.BridgeCell, Is.EqualTo(38));
            Assert.That(restored.TryUndo(out _), Is.True);
            Assert.That(restored.HasBox(45), Is.True);
        }

        [Test]
        public void CleanupRestoresFractionalRemainingWorkAndRejectsInvalidStateWithoutMutation()
        {
            var catalog = Content.CreateFields().Find("parking", "parking.pockets").Opening;
            using var source = new ApproachSession(Guid.NewGuid(), 3, catalog.Get(ApproachMode.Cleanup), catalog);
            int debris = source.DebrisAt(45);
            Assert.That(source.TryClean(45, ApproachTool.Litter, source.MaxStepSeconds, out _), Is.True);
            using var restored = new ApproachSession(Guid.NewGuid(), 3, catalog.Get(ApproachMode.Cleanup), catalog);
            var snapshot = source.CaptureCheckpoint();
            var invalid = snapshot.RemainingWork.ToArray(); invalid[debris] = double.NaN;
            Assert.Throws<ArgumentException>(() => restored.RestoreCheckpoint(
                new ApproachCheckpoint(snapshot.Boxes, snapshot.BridgeCell, invalid)));
            Assert.That(restored.Version, Is.Zero);
            restored.RestoreCheckpoint(snapshot);
            Assert.That(restored.RemainingWork(debris), Is.EqualTo(source.RemainingWork(debris)));
        }

        [Test]
        public void VersionOneStationMigratesOnReadWithoutWritingOrLosingIdentityCapacityAndTime()
        {
            var storage = new MemorySaveStorage();
            var content = Content;
            using (var current = new MainProgressSession(content.CreateMapConfig(), content.Artwork, storage))
                Assert.That(current.TryCheckpoint(), Is.True);
            var raw = new SaveService(storage);
            var section = new SaveSection<JObject>("main.station", 5, () => new JObject(), _ => null);
            raw.Register(section); Assert.That(raw.TryOpen(out _, out _), Is.True);
            Assert.That(raw.TryRead(section, out var data, out _), Is.True);
            data.Remove("Fields");
            data.Remove("Memorials");
            data.Remove("Dispatch");
            var oldStorage = new MemorySaveStorage();
            var old = new SaveService(oldStorage);
            var oldSection = new SaveSection<JObject>("main.station", 1, () => data, _ => null);
            old.Register(oldSection); Assert.That(old.TryOpen(out _, out _), Is.True);
            Assert.That(old.TryCommit(writer => writer.Set(oldSection, data), out _), Is.True);
            int writes = oldStorage.Writes;
            using var migrated = new MainProgressSession(content.CreateMapConfig(), content.Artwork, oldStorage);
            Assert.That(migrated.CanContinue, Is.True, migrated.PersistenceError?.ToString());
            Assert.That(oldStorage.Writes, Is.EqualTo(writes));
            Assert.That(migrated.Session.Requests.Requests.Select(item => item.RequestId.ToString("N")),
                Is.EqualTo(data["Requests"].Select(item => (string)item["Id"])));
            Assert.That(migrated.TryCheckpoint(), Is.True);
        }

        [Test]
        public void AdditionalRealRequestInstancesRoundTripWithoutReinitializingThePool()
        {
            var storage = new MemorySaveStorage();
            var content = Content;
            var id = Guid.NewGuid();
            using (var first = new MainProgressSession(content.CreateMapConfig(), content.Artwork, storage))
            {
                Assert.That(first.Session.Requests.TryAdd(new HelpRequest(id, "ordinary", "parking", 1), out _),
                    Is.True);
                Assert.That(first.TryCheckpoint(), Is.True, first.PersistenceError?.ToString());
            }
            using var restored = new MainProgressSession(content.CreateMapConfig(), content.Artwork, storage);
            Assert.That(restored.CanContinue, Is.True, restored.PersistenceError?.ToString());
            Assert.That(restored.Session.Requests.TryGet(id, out var request), Is.True);
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
            Assert.That(restored.Session.Requests.Requests.Count, Is.EqualTo(5));
        }
    }
}
