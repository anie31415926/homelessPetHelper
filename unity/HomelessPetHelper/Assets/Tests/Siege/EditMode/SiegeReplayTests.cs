using System;
using System.Linq;
using HomelessPetHelper.Editor.Siege;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.Siege.EditMode
{
    public sealed class SiegeReplayTests
    {
        private SiegeCatalog Catalog() => new SiegeCatalog(JsonUtility.FromJson<SiegeCatalogData>(
            AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text));

        [TestCase("cat", true)]
        [TestCase("dog", true)]
        [TestCase("cat", false)]
        [TestCase("dog", false)]
        public void RestoredInputsPreserveFutureSimulationAcrossCadencePauseAndSkills(string encounter, bool pause)
        {
            var catalog = Catalog();
            var live = new SiegeSession(catalog, 132, "alert", "replay", encounter);
            live.SetPauseAtDecision(pause);
            for (int i = 0; i < 420; i++)
            {
                if (i == 100) live.SetPauseAtDecision(!pause);
                if (i == 240) live.SetPauseAtDecision(pause);
                if (i == 120) { live.SetPaused(true); live.Advance(1000); live.SetPaused(false); }
                Drive(live, i);
            }
            var restored = new SiegeSession(catalog, 132, "alert", "replay", encounter);
            restored.RestoreReplay(live.CaptureReplay());
            restored.SetPauseAtDecision(pause);
            AssertSame(live.Snapshot, restored.Snapshot);
            for (int i = 0; i < 2200; i++)
            {
                Drive(live, i); Drive(restored, i);
                AssertSame(live.Snapshot, restored.Snapshot);
                if (live.Phase == SiegeState.BattlePhase.Won || live.Phase == SiegeState.BattlePhase.Timeout) break;
            }
            Assert.That(restored.Phase, Is.EqualTo(live.Phase));
        }

        [Test]
        public void InvalidReplayIsAtomicAndSnapshotOrdersAreDetached()
        {
            var catalog = Catalog();
            var live = new SiegeSession(catalog, 19, "calm", "replay", "cat");
            var order = new SiegeOrder { SessionId = "replay", Turn = 1 };
            Assert.That(live.TryOrder(order, true, out _, out _), Is.True);
            var replay = live.CaptureReplay();
            order.Turn = 99;
            replay.Steps[0].Order.Turn = 99;
            Assert.That(live.CaptureReplay().Steps[0].Order.Turn, Is.EqualTo(1));
            var fresh = new SiegeSession(catalog, 19, "calm", "replay", "cat");
            Assert.Throws<ArgumentException>(() => fresh.RestoreReplay(new SiegeReplay(new[]
                { new SiegeReplay.Step(1, true, false) }, 0)));
            Assert.That(fresh.Revision, Is.Zero);
            Assert.That(fresh.Snapshot.Turn, Is.EqualTo(1));
            fresh.RestoreReplay(live.CaptureReplay());
            AssertSame(live.Snapshot, fresh.Snapshot);
            Assert.Throws<InvalidOperationException>(() => fresh.RestoreReplay(replay));
        }

        [Test]
        public void RejectsUnboundedTicksNonFiniteRemainderAndForeignOrders()
        {
            var catalog = Catalog();
            foreach (var replay in new[]
            {
                new SiegeReplay(new[] { new SiegeReplay.Step(int.MaxValue, false, false) }, 0),
                new SiegeReplay(Array.Empty<SiegeReplay.Step>(), double.NaN),
                new SiegeReplay(new[] { new SiegeReplay.Step(0, true, false,
                    new SiegeOrder { SessionId = "foreign", Turn = 1 }) }, 0)
            })
            {
                var fresh = new SiegeSession(catalog, 19, "calm", "replay", "cat");
                Assert.Throws<ArgumentException>(() => fresh.RestoreReplay(replay));
                Assert.That(fresh.Revision, Is.Zero);
            }
        }

        private static void Drive(SiegeSession session, int tick)
        {
            var state = session.Snapshot;
            if (state.Phase == SiegeState.BattlePhase.Decision && tick % 7 == 0)
            {
                var card = state.Hand.FirstOrDefault(item =>
                    session.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
                var order = new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                    DeployCardId = card?.Id, DeployLaneId = card == null ? null :
                        new[] { "top", "mid", "bottom" }[state.Turn % 3] };
                Assert.That(session.TryOrder(order, true, out var reason, out _), Is.True, reason);
            }
            session.Advance(53.25);
        }

        private static void AssertSame(SiegeState expected, SiegeState actual)
        {
            Assert.That(actual.ElapsedMs, Is.EqualTo(expected.ElapsedMs));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.Turn, Is.EqualTo(expected.Turn));
            Assert.That(actual.Progress, Is.EqualTo(expected.Progress));
            Assert.That(actual.Energy, Is.EqualTo(expected.Energy));
            Assert.That(actual.PlayerChargeMs, Is.EqualTo(expected.PlayerChargeMs));
            Assert.That(actual.Hand.Select(card => card.Id + ":" + card.DefinitionId),
                Is.EqualTo(expected.Hand.Select(card => card.Id + ":" + card.DefinitionId)));
            Assert.That(actual.Units.Select(unit => (unit.Id, unit.LaneId, unit.Distance, unit.Hp,
                unit.IsCarried, unit.HeldUntilMs)), Is.EqualTo(expected.Units.Select(unit =>
                (unit.Id, unit.LaneId, unit.Distance, unit.Hp, unit.IsCarried, unit.HeldUntilMs))));
            Assert.That(actual.Towers.Select(tower => (tower.Id, tower.Hp, tower.Status)),
                Is.EqualTo(expected.Towers.Select(tower => (tower.Id, tower.Hp, tower.Status))));
            Assert.That(actual.AnimalState.Charge, Is.EqualTo(expected.AnimalState.Charge));
            Assert.That(actual.Log, Is.EqualTo(expected.Log));
        }
    }
}
