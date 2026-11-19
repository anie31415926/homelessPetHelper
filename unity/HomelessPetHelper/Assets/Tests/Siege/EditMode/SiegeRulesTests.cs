using System;
using System.Linq;
using HomelessPetHelper.Editor.Siege;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HomelessPetHelper.Modules.Siege.SiegeState;

namespace HomelessPetHelper.Tests.Siege.EditMode
{
    public sealed class SiegeRulesTests
    {
        private SiegeCatalogData Data() =>
            JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text
            );

        private SiegeSession Session(SiegeCatalogData data = null) =>
            new SiegeSession(new SiegeCatalog(data ?? Data()), 19, "calm", "rules");

        private SiegeOrder Empty(SiegeSession session) =>
            new SiegeOrder { SessionId = session.Snapshot.SessionId, Turn = session.Snapshot.Turn };

        private void Commit(SiegeSession session, SiegeOrder order)
        {
            Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
        }

        private void Next(SiegeSession session)
        {
            for (int i = 0; i < 10000 && session.Phase == BattlePhase.Running; i++)
                session.Advance(50);
            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Decision));
        }

        private SiegeCatalogData FastCartData()
        {
            var d = Data();
            d.rules.playerPeriodMs = 50;
            d.rules.openingEnergy = 6;
            foreach (var u in d.units)
                if (u.id != "cart")
                    u.bag = false;
            foreach (var p in d.animalStates)
                p.periodMs = 1000000;
            return d;
        }

        [Test]
        public void CatalogRejectsInvalidAndCopiesMutableSource()
        {
            var d = Data();
            var c = new SiegeCatalog(d);
            double hp = c.Unit("cart").Hp;
            d.units[0].hp = 999;
            d.map.lanes[0].points[0].x = 99;
            Assert.That(c.Unit("cart").Hp, Is.EqualTo(hp));
            Assert.That(c.Lanes[0].Points[0].X, Is.EqualTo(40));
            d.rules.stepMs = 0;
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
            d = Data();
            d.units[0].upgradeId = "missing";
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
            d = Data();
            d.tower.range = double.NaN;
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
            d = Data();
            d.map.lanes[0].points[1] = d.map.lanes[0].points[0];
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
        }

        [Test]
        public void DecisionAndManualPauseFreezeTheEntireBattle()
        {
            var session = Session();
            var before = session.Snapshot;
            session.Advance(1000);
            Assert.That(session.ElapsedMs, Is.Zero);
            Assert.That(session.Snapshot.Hand.Select(x => x.Id), Is.EqualTo(before.Hand.Select(x => x.Id)));
            Commit(session, Empty(session));
            session.Advance(500);
            session.SetPaused(true);
            before = session.Snapshot;
            session.Advance(1000);
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(session.Snapshot.AnimalState.Charge, Is.EqualTo(before.AnimalState.Charge));
            session.SetPaused(false);
            session.Advance(50);
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs + 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Advance(1001));
        }

        [Test]
        public void InvalidAtomicThirdMaterialDoesNotSpendSpawnOrConsumeIdentity()
        {
            var s = Session();
            var snapshot = s.Snapshot;
            var order = Empty(s);
            order.DeployCardId = snapshot.Hand[0].Id;
            order.DeployLaneId = "mid";
            order.MergeIds = new[] { -1, 999, 998 };
            order.AnchorId = -1;
            Assert.That(s.TryOrder(order, true, out _, out int cost), Is.False);
            Assert.That(cost, Is.Zero);
            Assert.That(s.Snapshot.Energy, Is.EqualTo(snapshot.Energy));
            Assert.That(s.Snapshot.Units, Is.Empty);
            Assert.That(s.Snapshot.Hand.Select(x => x.Id), Is.EqualTo(snapshot.Hand.Select(x => x.Id)));
            order.MergeIds = null;
            order.AnchorId = 0;
            Assert.That(s.TryOrder(order, false, out _, out cost), Is.True);
            Assert.That(s.Snapshot.Units, Is.Empty);
            Commit(s, order);
            Assert.That(s.Snapshot.Units[0].Id, Is.EqualTo(4));
            Assert.That(s.TryOrder(order, true, out _, out _), Is.False);
        }

        [Test]
        public void StaleSessionAndTurnOrdersAreRejected()
        {
            var s = Session();
            var old = Empty(s);
            old.SessionId = "old";
            Assert.That(s.TryOrder(old, true, out _, out _), Is.False);
            old = Empty(s);
            Commit(s, old);
            Next(s);
            Assert.That(s.TryOrder(old, true, out _, out _), Is.False);
            Assert.That(s.Snapshot.Turn, Is.EqualTo(2));
        }

        [Test]
        public void GaugeTieGivesPlayerTheDecisionBeforeAnimal()
        {
            var d = Data();
            d.animalStates.Single(x => x.id == "calm").periodMs = d.rules.playerPeriodMs;
            var s = Session(d);
            Commit(s, Empty(s));
            Next(s);
            Assert.That(s.Snapshot.AnimalState.Actions, Is.Zero);
            Assert.That(s.Snapshot.AnimalState.Charge, Is.EqualTo(1).Within(1e-12));
            Commit(s, Empty(s));
            s.Advance(50);
            Assert.That(s.Snapshot.AnimalState.Actions, Is.EqualTo(1));
        }

        [Test]
        public void PaidMergeInheritsInjuryAndKeepsChosenAnchor()
        {
            var d = FastCartData();
            d.tower.range = 1000;
            d.tower.periodMs = 50;
            var s = Session(d);
            for (int i = 0; i < 2; i++)
            {
                var o = Empty(s);
                o.DeployCardId = s.Snapshot.Hand[0].Id;
                o.DeployLaneId = i == 0 ? "mid" : "top";
                Commit(s, o);
                Next(s);
            }
            var before = s.Snapshot;
            var anchor = before.Units[0];
            var order = Empty(s);
            order.DeployCardId = before.Hand[0].Id;
            order.DeployLaneId = "bottom";
            order.MergeIds = new[] { before.Units[0].Id, before.Units[1].Id, -1 };
            order.AnchorId = anchor.Id;
            Commit(s, order);
            var after = s.Snapshot;
            Assert.That(after.Units.Count, Is.EqualTo(1));
            Assert.That(after.Units[0].DefinitionId, Is.EqualTo("ram"));
            Assert.That(after.Units[0].LaneId, Is.EqualTo(anchor.LaneId));
            Assert.That(after.Units[0].Distance, Is.EqualTo(anchor.Distance));
            Assert.That(
                after.Units[0].Hp,
                Is.EqualTo(s.Catalog.Unit("ram").Hp * (before.Units.Sum(x => x.Hp) + 7) / 21).Within(1e-8)
            );
            Assert.That(after.Units[0].Hp, Is.LessThan(17));
            Assert.That(after.Energy, Is.EqualTo(before.Energy - 3));
        }

        [Test]
        public void NaturalMergeDoesNotHappenAtSpawnAndNeedsNoResource()
        {
            var d = FastCartData();
            foreach (var pad in d.map.pads)
                pad.initial = false;
            var s = Session(d);
            for (int i = 0; i < 3; i++)
            {
                var o = Empty(s);
                o.DeployCardId = s.Snapshot.Hand[0].Id;
                o.DeployLaneId = "bottom";
                Commit(s, o);
                Next(s);
            }
            Assert.That(s.Snapshot.Units.Count, Is.EqualTo(3));
            while (s.ElapsedMs < 3500)
            {
                Commit(s, Empty(s));
                Next(s);
            }
            Assert.That(s.Snapshot.Stats.Merges, Is.EqualTo(1));
            Assert.That(s.Snapshot.Units.Single().DefinitionId, Is.EqualTo("ram"));
        }

        [Test]
        public void DiscardConsumesOnlyTheSelectedCardAndRefillsNextWindow()
        {
            var s = Session();
            var before = s.Snapshot;
            var o = Empty(s);
            o.DiscardCardId = before.Hand[1].Id;
            Commit(s, o);
            Assert.That(s.Snapshot.Hand.Count, Is.EqualTo(2));
            Assert.That(s.Snapshot.Energy, Is.EqualTo(before.Energy));
            Next(s);
            Assert.That(s.Snapshot.Hand.Count, Is.EqualTo(3));
            Assert.That(s.Snapshot.Hand.Any(x => x.Id == before.Hand[1].Id), Is.False);
        }

        [Test]
        public void RerouteStartsAtActualOldPointAndConsumesOneCharge()
        {
            var d = FastCartData();
            foreach (var pad in d.map.pads)
                pad.initial = false;
            d.units.Single(x => x.id == "ram").ability = "reroute";
            var s = Session(d);
            for (int i = 0; i < 3; i++)
            {
                var o = Empty(s);
                o.DeployCardId = s.Snapshot.Hand[0].Id;
                o.DeployLaneId = "bottom";
                Commit(s, o);
                Next(s);
            }
            var merge = Empty(s);
            merge.MergeIds = s.Snapshot.Units.Select(x => x.Id).ToArray();
            merge.AnchorId = merge.MergeIds[0];
            Commit(s, merge);
            Next(s);
            var unit = s.Snapshot.Units.Single();
            var point = s.Position(unit);
            var reroute = Empty(s);
            reroute.RerouteUnitId = unit.Id;
            reroute.RerouteLaneId = "top";
            Commit(s, reroute);
            unit = s.Snapshot.Units.Single();
            Assert.That(s.Position(unit).Distance(point), Is.LessThan(1e-8));
            Assert.That(unit.Reroutes, Is.Zero);
            Assert.That(unit.TransitRemainingMs, Is.EqualTo(d.rules.rerouteDurationMs));
        }

        [Test]
        public void WaitReachesTimeoutWithoutOfflineCatchupOrDeath()
        {
            var d = Data();
            d.rules.durationMs = 8000;
            var s = Session(d);
            while (s.Phase == BattlePhase.Decision || s.Phase == BattlePhase.Running)
            {
                if (s.Phase == BattlePhase.Decision)
                    Commit(s, Empty(s));
                s.Advance(1000);
            }
            Assert.That(s.Phase, Is.EqualTo(BattlePhase.Timeout));
            Assert.That(s.ElapsedMs, Is.EqualTo(8000));
            int turn = s.Snapshot.Turn;
            s.Advance(1000);
            Assert.That(s.Snapshot.Turn, Is.EqualTo(turn));
        }
    }
}
