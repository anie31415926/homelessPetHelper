using System;
using System.Diagnostics;
using System.Linq;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.Siege.EditMode
{
    public sealed class SiegeRealtimeTests
    {
        private SiegeCatalogData Data() => JsonUtility.FromJson<SiegeCatalogData>(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Siege/SiegeTrialData.json").text);

        private SiegeSession Session(SiegeCatalogData data = null)
        {
            var battle = new SiegeSession(new SiegeCatalog(data ?? Data()), 19, "calm", "realtime", "cat");
            battle.EnableRealtime();
            return battle;
        }

        private SiegePoint Start(SiegeSession battle) => battle.Catalog.Lanes[0].PointAt(0);
        private SiegeOrder Order(SiegeSession battle, SiegePoint point) => new SiegeOrder
        {
            SessionId = battle.Snapshot.SessionId, Turn = battle.Snapshot.Turn,
            DeployCardId = battle.Snapshot.Hand[0].Id, DeployPoint = point
        };

        private void Advance(SiegeSession battle, int milliseconds)
        { for (int time = 0; time < milliseconds; time += 50) battle.Advance(50); }

        [Test]
        public void DirectDeploy_ExactPoint_ConsumesOnce_Refills_AndDoesNotResetResourceClock()
        {
            var battle = Session();
            Advance(battle, 500);
            var origin = Start(battle);
            var point = new SiegePoint(origin.X + 3, origin.Y - 2);
            var order = Order(battle, point);
            int before = battle.Snapshot.Energy;
            Assert.That(battle.TryOrder(order, false, out var reason, out int cost), Is.True, reason);
            Assert.That(battle.Snapshot.Units, Is.Empty);
            Assert.That(battle.TryOrder(order, true, out reason, out _), Is.True, reason);
            var state = battle.Snapshot;
            Assert.That(state.Energy, Is.EqualTo(before - cost));
            Assert.That(state.Hand.Count, Is.EqualTo(battle.Catalog.Rules.HandSize));
            Assert.That(battle.Position(state.Units[0]).Distance(point), Is.LessThan(.000001));
            Assert.That(state.ResourceChargeMs, Is.EqualTo(500));
            Assert.That(battle.TryOrder(order, true, out _, out _), Is.False);
            Assert.That(battle.Snapshot.Energy, Is.EqualTo(state.Energy));
            Assert.That(battle.Snapshot.Units.Count, Is.EqualTo(1));
        }

        [Test]
        public void DeploymentRejectsOutsideNonFiniteInsufficientPausedAndTerminalWithoutSideEffects()
        {
            var data = Data(); data.rules.openingEnergy = 0;
            var battle = Session(data);
            foreach (var point in new[] { Start(battle), new SiegePoint(-1, 0), new SiegePoint(double.NaN, 1),
                battle.Catalog.Lanes[0].PointAt(battle.Catalog.Lanes[0].Length) })
            {
                var before = battle.Snapshot;
                Assert.That(battle.TryOrder(Order(battle, point), true, out _, out _), Is.False);
                Assert.That(battle.Snapshot.Energy, Is.EqualTo(before.Energy));
                Assert.That(battle.Snapshot.Hand.Select(card => card.Id),
                    Is.EqualTo(before.Hand.Select(card => card.Id)));
                Assert.That(battle.Snapshot.Units, Is.Empty);
            }
            battle.SetPaused(true); Advance(battle, 5000);
            Assert.That(battle.ElapsedMs, Is.Zero);
            battle.SetPaused(false); Advance(battle, data.rules.durationMs);
            Assert.That(battle.TryOrder(Order(battle, Start(battle)), true, out _, out _), Is.False);
        }

        [Test]
        public void ResourcesAdvanceWithoutOrders_Cap_AndStopForPauseAndTutorial()
        {
            var battle = Session();
            Advance(battle, 5000);
            Assert.That(battle.Snapshot.Energy, Is.EqualTo(battle.Catalog.Rules.EnergyCap));
            battle.SetDecisionHold(true);
            int elapsed = battle.ElapsedMs;
            Advance(battle, 3000);
            Assert.That(battle.ElapsedMs, Is.EqualTo(elapsed));
            Assert.That(battle.TryOrder(Order(battle, Start(battle)), true, out var reason, out _), Is.True, reason);
            battle.SetDecisionHold(false); battle.SetPauseAtDecision(true);
            Assert.That(battle.IsAdvancing, Is.True);
        }

        [Test]
        public void DefenderSpendsRenewableResources_UsesUniqueCells_AndFastConstruction()
        {
            var battle = Session(); int initial = battle.Snapshot.Towers.Count;
            Advance(battle, 6000);
            var state = battle.Snapshot;
            Assert.That(state.Towers.Count, Is.EqualTo(initial + 1));
            Assert.That(state.DefenderEnergy, Is.Zero);
            var built = state.Towers.Last();
            Assert.That(built.Status, Is.EqualTo(SiegeState.TowerStatus.Building));
            Assert.That(built.ReadyAtMs - state.ElapsedMs, Is.EqualTo(600));
            Advance(battle, 600);
            Assert.That(battle.Snapshot.Towers.Last().Status, Is.EqualTo(SiegeState.TowerStatus.Active));
            Advance(battle, 6000);
            var occupied = battle.Snapshot.Towers.Where(tower => tower.Status != SiegeState.TowerStatus.Destroyed)
                .Select(tower => SiegeDeployment.Cell(battle.Catalog, SiegeGeometry.Position(battle.Catalog, tower)));
            Assert.That(occupied.Distinct().Count(), Is.EqualTo(occupied.Count()));
        }

        [Test]
        public void ReplayPreservesFreeCoordinatesAndResources_AndUpgradesLegacyAtExplicitBoundary()
        {
            var original = Session();
            original.TryOrder(Order(original, Start(original)), true, out _, out _);
            Advance(original, 7000);
            var restored = new SiegeSession(original.Catalog, 19, "calm", "realtime", "cat");
            restored.RestoreReplay(original.CaptureReplay());
            Assert.That(restored.ElapsedMs, Is.EqualTo(original.ElapsedMs));
            Assert.That(restored.Snapshot.ResourceChargeMs, Is.EqualTo(original.Snapshot.ResourceChargeMs));
            Assert.That(restored.Snapshot.DefenderEnergy, Is.EqualTo(original.Snapshot.DefenderEnergy));
            Assert.That(restored.Snapshot.Hand.Select(card => card.Id),
                Is.EqualTo(original.Snapshot.Hand.Select(card => card.Id)));
            foreach (var unit in original.Snapshot.Units)
                Assert.That(restored.Position(restored.Snapshot.Units.Single(item => item.Id == unit.Id))
                    .Distance(original.Position(unit)), Is.LessThan(.000001));
            Assert.That(restored.Snapshot.Energy, Is.EqualTo(original.Snapshot.Energy));
            Assert.That(restored.Snapshot.Towers.Count, Is.EqualTo(original.Snapshot.Towers.Count));
            Assert.That(restored.Realtime, Is.True);
            var legacy = new SiegeSession(original.Catalog, 19, "calm", "realtime", "cat");
            legacy.TryOrder(new SiegeOrder { SessionId = "realtime", Turn = 1 }, true, out _, out _);
            Advance(legacy, 500); legacy.EnableRealtime(); Advance(legacy, 500);
            var upgraded = new SiegeSession(original.Catalog, 19, "calm", "realtime", "cat");
            upgraded.RestoreReplay(legacy.CaptureReplay());
            Assert.That(upgraded.ElapsedMs, Is.EqualTo(1000));
            Assert.That(upgraded.Realtime, Is.True);
        }

        [Test]
        public void ActualFieldDeploymentAudit()
        {
            var content = AssetDatabase.LoadAssetAtPath<HomelessPetHelper.Game.GameContent>(
                "Assets/Settings/Game/GameContent.asset");
            foreach (var field in content.CreateFields().Layouts)
            foreach (string encounter in new[] { "cat", "dog" })
            {
                var battle = new SiegeSession(content.CreateFieldSiegeCatalog(field), 19,
                    "calm", "audit", encounter);
                battle.EnableRealtime();
                int deployed = 0;
                for (int step = 0; step < 120 && battle.IsAdvancing; step++)
                {
                    var state = battle.Snapshot;
                    var card = state.Hand.FirstOrDefault(item =>
                        battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
                    if (card != null)
                    {
                        var lane = battle.Catalog.Lanes[deployed % 3];
                        var point = lane.PointAt(lane.Length * .2);
                        if (!SiegeDeployment.CanDeploy(battle.Catalog, point)) point = lane.PointAt(0);
                        if (battle.TryOrder(new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                            DeployCardId = card.Id, DeployPoint = point }, true, out _, out _)) deployed++;
                    }
                    Advance(battle, 1000);
                }
                TestContext.WriteLine(field.LayoutId + "/" + encounter + ": " + battle.Phase +
                    ", progress=" + battle.Snapshot.Progress + ", deployed=" + deployed);
            }
        }

        [Test]
        public void CapacityRejectsWithoutConsumingAndPresentationBufferCannotMutateLiveState()
        {
            var data = Data(); data.rules.maxTroops = 3;
            data.rules.openingEnergy = data.rules.energyCap = 100;
            var battle = Session(data);
            for (int index = 0; index < 3; index++)
                Assert.That(battle.TryOrder(Order(battle, Start(battle)), true, out _, out _), Is.True);
            var before = battle.Snapshot;
            Assert.That(battle.TryOrder(Order(battle, Start(battle)), true, out var reason, out _), Is.False);
            Assert.That(reason, Is.EqualTo("field.unitLimit"));
            Assert.That(battle.Snapshot.Energy, Is.EqualTo(before.Energy));
            var presentation = new SiegeState(); battle.CopyPresentationTo(presentation);
            Assert.That(presentation.Units.Count, Is.EqualTo(3));
            Assert.That(presentation.Units, Is.Not.SameAs(before.Units));
            Assert.That(presentation.Units[0], Is.Not.SameAs(before.Units[0]));
        }

        [Test]
        public void DeploymentPreviewCostDoesNotGrowWithTroopCount()
        {
            var data = Data(); data.rules.openingEnergy = data.rules.energyCap = 100;
            foreach (var unit in data.contentUnits) { unit.cost = 1; unit.speed = .1; unit.damage = .01; }
            var battle = Session(data);
            for (int index = 0; index < 90; index++)
                Assert.That(battle.TryOrder(Order(battle, Start(battle)), true, out var reason, out _),
                    Is.True, reason);
            var order = Order(battle, Start(battle));
            battle.TryOrder(order, false, out _, out _);
            long before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var timer = Stopwatch.StartNew();
            for (int index = 0; index < 10000; index++) battle.TryOrder(order, false, out _, out _);
            timer.Stop();
            long bytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before;
            TestContext.WriteLine("90 units, 10000 previews: " + timer.Elapsed.TotalMilliseconds +
                "ms; " + bytes + " bytes");
            // Heap deltas are observational: collections and Editor work prevent per-call allocation claims.
            timer.Restart(); before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (int index = 0; index < 1000; index++) GC.KeepAlive(battle.Snapshot);
            long snapshotBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before; timer.Stop();
            TestContext.WriteLine("90-unit full-copy reference, 1000 snapshots: " +
                timer.Elapsed.TotalMilliseconds + "ms; " + snapshotBytes + " bytes");
            var buffer = new SiegeState(); battle.CopyPresentationTo(buffer);
            timer.Restart(); before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            for (int index = 0; index < 1000; index++) battle.CopyPresentationTo(buffer);
            long copyBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before; timer.Stop();
            TestContext.WriteLine("90-unit reusable presentation, 1000 copies: " +
                timer.Elapsed.TotalMilliseconds + "ms; " + copyBytes + " bytes");
            timer.Restart(); before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            Advance(battle, 20000);
            long simulationBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before; timer.Stop();
            TestContext.WriteLine("90-unit initial stress, 400 ticks: " +
                timer.Elapsed.TotalMilliseconds + "ms; " + simulationBytes + " bytes");
        }
    }
}
