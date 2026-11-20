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
    public sealed class SiegeHoldTests
    {
        private SiegeCatalogData Data()
        {
            var d = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);
            d.rules.playerPeriodMs = 200; d.rules.turnEnergy = 6; d.rules.openingEnergy = 6;
            d.rules.animalBuilds = 0; d.rules.animalRepairs = 0; d.rules.sweepPush = 0;
            d.rules.sweepDamage = .01; d.rules.slapDamage = .01;
            foreach (var p in d.animalStates) p.periodMs = 1000;
            foreach (var t in d.towerTypes) { t.hp = 10000; t.damage = 0; t.range = .01; }
            foreach (var u in d.contentUnits) { u.hp = 100; u.speed = 2; }
            var hold = d.encounters[0];
            hold.holdOriginX = 40; hold.holdOriginY = 310; hold.holdRange = 500;
            hold.holdDurationMs = 600; hold.holdRecoveryMs = 150;
            hold.holdCooldownMs = 1000; hold.holdProtectionMs = 2000;
            return d;
        }

        private SiegeSession Session(SiegeCatalogData data = null) =>
            new SiegeSession(new SiegeCatalog(data ?? Data()), 132, "calm", "hold", "cat");

        private void Commit(SiegeSession session, SiegeOrder order = null)
        {
            var s = session.Snapshot;
            order = order ?? new SiegeOrder(); order.SessionId = s.SessionId; order.Turn = s.Turn;
            Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
        }

        private void Next(SiegeSession session)
        {
            for (int i = 0; i < 10000 && session.Phase == BattlePhase.Running; i++) session.Advance(50);
            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Decision));
        }

        private int Deploy(SiegeSession session, string lane = "mid")
        {
            Commit(session, new SiegeOrder { DeployCardId = session.Snapshot.Hand[0].Id, DeployLaneId = lane });
            int id = session.Snapshot.Units.Last().Id; Next(session); return id;
        }

        private void Until(SiegeSession session, Func<SiegeState, bool> condition, int maxMs = 30000)
        {
            int limit = session.ElapsedMs + maxMs;
            while (!condition(session.Snapshot) && session.ElapsedMs < limit)
            {
                if (session.Phase == BattlePhase.Decision) Commit(session);
                session.Advance(50);
            }
            Assert.That(condition(session.Snapshot), Is.True, "Condition not reached by " + session.ElapsedMs);
        }

        private void AtHoldWindow(SiegeSession s)
        {
            Until(s, x => x.AnimalState.Intent == IntentKind.Hold);
            if (s.Phase == BattlePhase.Running) Next(s);
            Assert.That(s.Snapshot.AnimalState.Intent, Is.EqualTo(IntentKind.Hold));
        }

        [Test]
        public void HoldConfigurationRejectsInvalidDurationsAndNonFiniteGeometry()
        {
            var d = Data(); d.encounters[0].holdProtectionMs = 0;
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
            d = Data(); d.encounters[0].holdRange = double.NaN;
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
            d = Data(); d.encounters[0].holdCooldownMs = 1;
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(d));
        }

        [Test]
        public void WarningLocksIdentityWaitsForDecisionAndDoesNotFreezeOtherUnits()
        {
            var s = Session(); Deploy(s); Deploy(s, "top");
            AtHoldWindow(s);
            int id = s.Snapshot.AnimalState.IntentUnitId;
            int holds = s.Snapshot.Stats.Holds;
            var before = s.Snapshot; s.Advance(1000);
            Assert.That(s.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(s.Snapshot.Stats.Holds, Is.EqualTo(holds));
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.EqualTo(id));
            var start = s.Snapshot;
            if (s.Phase == BattlePhase.Decision) Commit(s);
            s.Advance(50);
            Assert.That(s.Snapshot.Units.Single(x => x.Id == id).Distance,
                Is.EqualTo(start.Units.Single(x => x.Id == id).Distance));
            int ally = start.Units.First(x => x.Id != id).Id;
            Assert.That(s.Snapshot.Units.Single(x => x.Id == ally).Distance,
                Is.GreaterThan(start.Units.Single(x => x.Id == ally).Distance));
            Assert.That(s.Snapshot.AnimalState.GuardUntilMs, Is.LessThanOrEqualTo(s.ElapsedMs));
            Assert.That(s.Snapshot.Units.Single(x => x.Id == id).Hp, Is.EqualTo(100));
        }

        [Test]
        public void HeldStopsAttackingAndPauseFreezesUntilReleaseThenProtectionSurvives()
        {
            var data = Data();
            foreach (var u in data.contentUnits) u.reach = 1000;
            var s = Session(data); Deploy(s);
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            var before = s.Snapshot;
            s.SetPaused(true); s.Advance(1000);
            Assert.That(s.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(s.Snapshot.AnimalState.HoldUntilMs, Is.EqualTo(before.AnimalState.HoldUntilMs));
            s.SetPaused(false);
            int end = before.AnimalState.HoldUntilMs;
            Until(s, x => x.ElapsedMs >= end - 50);
            Assert.That(s.Snapshot.Towers.Select(x => x.Hp), Is.EqualTo(before.Towers.Select(x => x.Hp)));
            Until(s, x => x.AnimalState.HeldUnitId == 0);
            var unit = s.Snapshot.Units.Single();
            Assert.That(unit.HoldProtectedUntilMs, Is.GreaterThan(s.ElapsedMs));
            Until(s, x => x.ElapsedMs >= end + 1000);
            Assert.That(s.Snapshot.Towers.Sum(x => x.Hp), Is.LessThan(before.Towers.Sum(x => x.Hp)));
        }

        [Test]
        public void ActiveHoldMergeInheritsTimeProtectionAndOwnerAtAnyAnchor()
        {
            var s = Session(); int one = Deploy(s); int two = Deploy(s, "top"); Deploy(s, "bottom");
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            if (s.Phase == BattlePhase.Running) Next(s);
            var before = s.Snapshot;
            int anchor = before.AnimalState.HeldUnitId == one ? two : one;
            var ids = before.Units.Select(x => x.Id).ToArray();
            Commit(s, new SiegeOrder { MergeIds = ids, AnchorId = anchor });
            var after = s.Snapshot; var merged = after.Units.Single();
            Assert.That(merged.DefinitionId, Is.EqualTo("bouquet"));
            Assert.That(merged.HeldUntilMs, Is.EqualTo(before.AnimalState.HoldUntilMs));
            Assert.That(merged.HoldProtectedUntilMs, Is.EqualTo(before.Units.Max(x => x.HoldProtectedUntilMs)));
            Assert.That(after.AnimalState.HeldUnitId, Is.EqualTo(merged.Id));
            Assert.That(after.AnimalState.Cast.Target.UnitId, Is.EqualTo(merged.Id));
            double distance = merged.Distance; s.Advance(50);
            Assert.That(s.Snapshot.Units.Single().Distance, Is.EqualTo(distance));
        }

        [Test]
        public void WarningTargetMergeTracksResult()
        {
            var s = Session(); Deploy(s); Deploy(s, "top"); Deploy(s, "bottom");
            AtHoldWindow(s);
            var before = s.Snapshot;
            Commit(s, new SiegeOrder { MergeIds = before.Units.Select(x => x.Id).ToArray(),
                AnchorId = before.Units.Last().Id });
            int result = s.Snapshot.Units.Single().Id;
            Assert.That(s.Snapshot.AnimalState.IntentUnitId, Is.EqualTo(result));
            Until(s, x => x.Stats.Holds > 0);
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.EqualTo(result));
        }

        [Test]
        public void WarningMergeToOutOfRangeAnchorMissesInsteadOfRetargeting()
        {
            var data = Data(); data.encounters[0].holdRange = 20;
            data.map.lanes.Single(x => x.id == "top").points[0].x = 180;
            data.map.lanes.Single(x => x.id == "bottom").points[0].x = 180;
            var s = Session(data); Deploy(s); Deploy(s, "top"); Deploy(s, "bottom");
            AtHoldWindow(s);
            var before = s.Snapshot;
            Assert.That(before.AnimalState.IntentLane, Is.EqualTo("mid"));
            Commit(s, new SiegeOrder { MergeIds = before.Units.Select(x => x.Id).ToArray(),
                AnchorId = before.Units.Single(x => x.LaneId == "bottom").Id });
            Until(s, x => x.Stats.HoldMisses > 0);
            Assert.That(s.Snapshot.Stats.Holds, Is.Zero);
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.Zero);
            Assert.That(s.Snapshot.Units.Single().HeldUntilMs, Is.Zero);
        }

        [Test]
        public void NaturalMergeAlsoKeepsAnExistingHold()
        {
            var data = Data(); data.rules.mergeRadius = .5; data.rules.naturalMergeMinDistance = .6;
            var s = Session(data); Deploy(s); Deploy(s); Deploy(s);
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            int end = s.Snapshot.AnimalState.HoldUntilMs;
            Until(s, x => x.Stats.Merges > 0);
            Assert.That(s.ElapsedMs, Is.LessThan(end));
            Assert.That(s.Snapshot.Units.Single().HeldUntilMs, Is.EqualTo(end));
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.EqualTo(s.Snapshot.Units.Single().Id));
        }

        [Test]
        public void HeldRerouteIsRejectedAtomicallyAndPreviewDoesNotSpend()
        {
            var data = Data(); data.contentUnits.Single(x => x.id == "fish").ability = "reroute";
            var s = Session(data); Deploy(s);
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            if (s.Phase == BattlePhase.Running) Next(s);
            var before = s.Snapshot;
            var order = new SiegeOrder { SessionId = before.SessionId, Turn = before.Turn,
                RerouteUnitId = before.Units.Single().Id, RerouteLaneId = "top" };
            foreach (bool commit in new[] { false, true })
            {
                Assert.That(s.TryOrder(order, commit, out string reason, out int cost), Is.False);
                Assert.That(reason, Is.EqualTo("reason.held")); Assert.That(cost, Is.Zero);
                Assert.That(s.Snapshot.Energy, Is.EqualTo(before.Energy));
                Assert.That(s.Snapshot.Units.Single().Reroutes, Is.EqualTo(1));
                Assert.That(s.Snapshot.Phase, Is.EqualTo(BattlePhase.Decision));
            }
        }

        [Test]
        public void DeadTargetReleasesOwnerAndCannotRetargetAnotherUnit()
        {
            var d = Data();
            d.encounters[0].holdDurationMs = 12000; d.encounters[0].holdCooldownMs = 14000;
            foreach (var t in d.towerTypes)
            { t.attack = "single"; t.range = 1000; t.damage = .6; t.periodMs = 50; }
            foreach (var u in d.contentUnits) u.hp = 100;
            var s = Session(d); Deploy(s); Deploy(s, "top");
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            int target = s.Snapshot.AnimalState.HeldUnitId;
            int plannedEnd = s.Snapshot.AnimalState.HoldUntilMs;
            Until(s, x => x.Units.All(u => u.Id != target));
            Assert.That(s.ElapsedMs, Is.LessThan(plannedEnd));
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.Zero);
            Assert.That(s.Snapshot.Log, Does.Contain("event.holdEnd"));
        }

        [Test]
        public void WarningTargetDiesBeforePressAndDoesNotRetargetAnEligibleAlly()
        {
            var data = Data();
            foreach (var pad in data.map.pads) pad.initial = pad.id == "mid.outer";
            data.encounters[0].initialTowerIds = new[] { "paper" };
            var tower = data.towerTypes.Single(x => x.id == "paper");
            tower.range = 1000; tower.damage = 40; tower.periodMs = 700;
            var s = Session(data); int target = Deploy(s); int ally = Deploy(s, "top");
            AtHoldWindow(s);
            Assert.That(s.Snapshot.AnimalState.IntentUnitId, Is.EqualTo(target));
            Until(s, x => x.Stats.HoldMisses > 0);
            Assert.That(s.Snapshot.Units.Any(x => x.Id == target), Is.False);
            Assert.That(s.Snapshot.Units.Any(x => x.Id == ally), Is.True);
            Assert.That(s.Snapshot.Stats.Holds, Is.Zero);
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.Zero);
        }

        [Test]
        public void ShieldAndBraceDoNotPreventHoldAndHeldPushDoesNotConsumeBrace()
        {
            var data = Data();
            data.encounters[0].holdDurationMs = 3000; data.encounters[0].holdCooldownMs = 4000;
            foreach (var pad in data.map.pads) pad.initial = pad.id == "mid.outer";
            data.encounters[0].initialTowerIds = new[] { "scratch" };
            data.contentUnits.Single(x => x.id == "fish").ability = "brace";
            var yarn = data.contentUnits.Single(x => x.id == "yarn");
            yarn.abilityRadius = 1000; yarn.abilityPeriodMs = 50;
            var tower = data.towerTypes.Single(x => x.id == "scratch");
            tower.range = 1000; tower.radius = 1000; tower.damage = 1;
            tower.push = 100; tower.periodMs = 500; tower.warningMs = 2500;
            var s = Session(data); int target = Deploy(s);
            Commit(s, new SiegeOrder { DeployCardId = s.Snapshot.Hand.Single(x => x.DefinitionId == "yarn").Id,
                DeployLaneId = "top" });
            Until(s, x => x.AnimalState.HeldUnitId != 0);
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.EqualTo(target));
            var held = s.Snapshot.Units.Single(x => x.Id == target);
            Assert.That(held.Shield, Is.EqualTo(1)); Assert.That(held.Brace, Is.EqualTo(1));
            Until(s, x => x.Effects.Any(e => e.Kind == "push"));
            var hit = s.Snapshot.Units.Single(x => x.Id == target);
            Assert.That(hit.Hp, Is.EqualTo(held.Hp));
            Assert.That(hit.Shield, Is.Zero);
            Assert.That(hit.Distance, Is.EqualTo(held.Distance));
            Assert.That(hit.Brace, Is.EqualTo(1));
            Assert.That(s.Snapshot.AnimalState.HeldUnitId, Is.EqualTo(target));
        }

        [Test]
        public void ProtectedTargetCannotBeHeldAgainBeforeItsProtectionExpires()
        {
            var s = Session(); Deploy(s);
            Until(s, x => x.Stats.Holds == 1);
            int protectedUntil = s.Snapshot.Units.Single().HoldProtectedUntilMs;
            Until(s, x => x.ElapsedMs >= protectedUntil - 50);
            Assert.That(s.Snapshot.Stats.Holds, Is.EqualTo(1));
            Assert.That(s.Snapshot.AnimalState.Intent, Is.Not.EqualTo(IntentKind.Hold));
            Until(s, x => x.Stats.Holds >= 2);
            Assert.That(s.ElapsedMs, Is.GreaterThanOrEqualTo(protectedUntil));
        }

        [Test]
        public void DogAndLegacyDoNotReceiveCatHold()
        {
            var c = new SiegeCatalog(Data());
            foreach (string id in new[] { "dog", null })
            {
                var s = new SiegeSession(c, 132, "calm", "other", id);
                Deploy(s); Until(s, x => x.ElapsedMs >= 10000);
                Assert.That(s.Snapshot.Stats.Holds, Is.Zero);
                Assert.That(s.Snapshot.AnimalState.Intent, Is.Not.EqualTo(IntentKind.Hold));
            }
        }

        [Test]
        public void NormalCatEncounterCanActuallyUseItsHold()
        {
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);
            int successes = 0;
            foreach (uint seed in new uint[] { 19, 132, 7183 })
            foreach (string lane in new[] { "top", "mid", "bottom" })
            {
                var s = new SiegeSession(new SiegeCatalog(data), seed, "calm", "normal-hold", "cat");
                for (int i = 0; i < 3000 && s.Phase != BattlePhase.Won && s.Phase != BattlePhase.Timeout; i++)
                {
                    if (s.Phase == BattlePhase.Decision)
                    {
                        var snapshot = s.Snapshot;
                        var order = new SiegeOrder();
                        if (snapshot.Hand.Count > 0 && snapshot.Energy >= 2)
                        {
                            order.DeployCardId = snapshot.Hand[0].Id;
                            order.DeployLaneId = lane;
                        }
                        Commit(s, order);
                    }
                    s.Advance(50);
                }
                successes += s.Snapshot.Stats.Holds;
                TestContext.Progress.WriteLine("Normal hold " + seed + "/" + lane + ": "
                    + s.Snapshot.Stats.Holds + " held, " + s.Snapshot.Stats.HoldMisses + " missed");
            }
            Assert.That(successes, Is.GreaterThan(0), "No hold succeeded in nine unmodified normal encounters.");
        }
    }
}
