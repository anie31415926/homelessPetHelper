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
    public sealed class SiegeCarryTests
    {
        private SiegeCatalogData Data()
        {
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);
            data.rules.playerPeriodMs = 200; data.rules.turnEnergy = 6; data.rules.openingEnergy = 6;
            data.rules.animalBuilds = 0; data.rules.animalRepairs = 0;
            data.rules.sweepDamage = .01; data.rules.slapDamage = .01;
            data.rules.sweepPush = 0; data.rules.slapPush = 0;
            foreach (var pad in data.map.pads) pad.initial = false;
            foreach (var profile in data.animalStates) profile.periodMs = 1000;
            foreach (var unit in data.contentUnits) { unit.hp = 100; unit.speed = 10; }
            var carry = data.encounters[1];
            carry.carryOriginX = 40; carry.carryOriginY = 310; carry.carryRange = 500;
            carry.carryDelayMs = 200; carry.carryDurationMs = 600; carry.carryRecoveryMs = 150;
            carry.carryCooldownMs = 1200; carry.carryProtectionMs = 2000;
            carry.carryDistance = 6; carry.carryMinDistance = 2;
            return data;
        }

        private SiegeSession Session(SiegeCatalogData data = null, string encounter = "dog") =>
            new SiegeSession(new SiegeCatalog(data ?? Data()), 132, "calm", "carry", encounter);

        private void Commit(SiegeSession session, SiegeOrder order = null)
        {
            var state = session.Snapshot;
            order = order ?? new SiegeOrder(); order.SessionId = state.SessionId; order.Turn = state.Turn;
            Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
        }

        private void Next(SiegeSession session)
        {
            for (int i = 0; i < 1000 && session.Phase == BattlePhase.Running; i++) session.Advance(50);
            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Decision));
        }

        private int Deploy(SiegeSession session, string lane = "mid")
        {
            Commit(session, new SiegeOrder { DeployCardId = session.Snapshot.Hand[0].Id, DeployLaneId = lane });
            int id = session.Snapshot.Units.Last().Id;
            Next(session);
            return id;
        }

        private void Until(SiegeSession session, Func<SiegeState, bool> condition, int maxMs = 30000)
        {
            int deadline = session.ElapsedMs + maxMs;
            while (!condition(session.Snapshot) && session.ElapsedMs < deadline)
            {
                if (session.Phase == BattlePhase.Decision) Commit(session);
                session.Advance(50);
                if (session.Phase == BattlePhase.Won || session.Phase == BattlePhase.Timeout) break;
            }
            Assert.That(condition(session.Snapshot), Is.True, "Not reached at " + session.ElapsedMs);
        }

        private void AtWarning(SiegeSession session)
        {
            Until(session, x => x.AnimalState.Intent == IntentKind.Carry);
            if (session.Phase == BattlePhase.Running) Next(session);
        }

        [Test]
        public void InvalidCarryConfigurationIsRejectedAtRuntime()
        {
            foreach (Action<SiegeCatalogData.EncounterDto> change in new Action<SiegeCatalogData.EncounterDto>[]
            {
                x => x.carryRange = double.NaN, x => x.carryOriginY = double.PositiveInfinity,
                x => x.carryDistance = -1, x => x.carryMinDistance = 0,
                x => x.carryProtectionMs = 0, x => x.carryRecoveryMs = 0,
                x => x.carryCooldownMs = 1, x => x.carryDurationMs = -1,
            })
            {
                var data = Data(); change(data.encounters[1]);
                Assert.Throws<ArgumentException>(() => new SiegeCatalog(data));
            }
        }

        [Test]
        public void CarryWaitsForPlayerWindowAndUsesTheSameLockedIdentityOnce()
        {
            var session = Session(); Deploy(session); Deploy(session, "top");
            AtWarning(session);
            var warning = session.Snapshot;
            int id = warning.AnimalState.IntentUnitId;
            session.Advance(1000);
            Assert.That(session.ElapsedMs, Is.EqualTo(warning.ElapsedMs));
            Assert.That(session.Snapshot.Stats.Carries, Is.Zero);
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            var start = session.Snapshot;
            Assert.That(start.AnimalState.CarriedUnitId, Is.EqualTo(id));
            Assert.That(start.AnimalState.GuardUntilMs, Is.LessThanOrEqualTo(start.ElapsedMs));
            Assert.That(start.AnimalState.SkillReadyAtMs(IntentKind.Carry),
                Is.EqualTo(start.AnimalState.Cast.StartedMs
                    + session.Catalog.Content.Encounter("dog").CarryCooldownMs));
            Until(session, x => x.ElapsedMs >= start.ElapsedMs + 100);
            var after = session.Snapshot;
            Assert.That(after.Stats.Carries, Is.EqualTo(1));
            Assert.That(after.Units.Single(x => x.Id == id).Distance,
                Is.LessThan(start.Units.Single(x => x.Id == id).Distance));
            var ally = start.Units.Single(x => x.Id != id);
            Assert.That(after.Units.Single(x => x.Id == ally.Id).Distance, Is.GreaterThan(ally.Distance));
            Assert.That(after.Stats.Delivered, Is.Zero);
        }

        [Test]
        public void PickupApproachAndActiveCarryFreezeForPauseAndDecision()
        {
            var session = Session(); Deploy(session);
            Until(session, x => x.AnimalState.Cast?.Kind == IntentKind.Carry
                && x.AnimalState.Cast.Phase == CastPhase.Windup);
            var before = session.Snapshot;
            session.SetPaused(true); session.Advance(1000); session.SetPaused(false);
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(session.Snapshot.Stats.Carries, Is.Zero);
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            Next(session);
            before = session.Snapshot;
            session.Advance(1000);
            Assert.That(session.Snapshot.Units.Single().Distance, Is.EqualTo(before.Units.Single().Distance));
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Commit(session); session.SetPaused(true); session.Advance(1000); session.SetPaused(false);
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(session.Snapshot.Units.Single().CarriedUntilMs,
                Is.EqualTo(before.Units.Single().CarriedUntilMs));
        }

        [Test]
        public void LandingClampsBeforeSpawnResumesAndKeepsProtectionThroughMerge()
        {
            var data = Data(); data.encounters[1].carryDistance = 1000;
            var session = Session(data); Deploy(session); Deploy(session, "top"); Deploy(session, "bottom");
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            int id = session.Snapshot.AnimalState.CarriedUnitId;
            int end = session.Snapshot.Units.Single(x => x.Id == id).CarriedUntilMs;
            Until(session, x => x.ElapsedMs >= end);
            var landed = session.Snapshot.Units.Single(x => x.Id == id);
            Assert.That(landed.IsCarried, Is.False);
            Assert.That(landed.Distance, Is.EqualTo(2));
            Assert.That(landed.HoldProtectedUntilMs, Is.EqualTo(end + 2000));
            Assert.That(session.Snapshot.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Recovery));
            Next(session);
            var before = session.Snapshot;
            Commit(session, new SiegeOrder { MergeIds = before.Units.Select(x => x.Id).ToArray(),
                AnchorId = before.Units.First(x => x.Id != id).Id });
            Assert.That(session.Snapshot.Units.Single().HoldProtectedUntilMs, Is.EqualTo(end + 2000));
            Until(session, x => x.ElapsedMs >= end + 100);
            Assert.That(session.Snapshot.Units.Single().Distance, Is.GreaterThan(2));
            Assert.That(session.Snapshot.Stats.Carries, Is.EqualTo(1));
        }

        [Test]
        public void ActiveCarryRejectsPaidMergeAndRerouteAtomically()
        {
            var data = Data(); data.contentUnits.Single(x => x.id == "bone").ability = "reroute";
            var session = Session(data); Deploy(session); Deploy(session, "top"); Deploy(session, "bottom");
            Until(session, x => x.AnimalState.CarriedUnitId != 0); Next(session);
            var before = session.Snapshot;
            var orders = new[]
            {
                new SiegeOrder { MergeIds = before.Units.Select(x => x.Id).ToArray(), AnchorId = before.Units[0].Id },
                new SiegeOrder { RerouteUnitId = before.AnimalState.CarriedUnitId, RerouteLaneId = "bottom" },
            };
            foreach (var order in orders)
            foreach (bool commit in new[] { false, true })
            {
                order.SessionId = before.SessionId; order.Turn = before.Turn;
                Assert.That(session.TryOrder(order, commit, out string reason, out int cost), Is.False);
                Assert.That(reason, Is.EqualTo("reason.carried")); Assert.That(cost, Is.Zero);
                Assert.That(session.Snapshot.Energy, Is.EqualTo(before.Energy));
                Assert.That(session.Snapshot.Units.Select(x => x.Id), Is.EqualTo(before.Units.Select(x => x.Id)));
                Assert.That(session.Snapshot.Units.Select(x => x.Distance),
                    Is.EqualTo(before.Units.Select(x => x.Distance)));
                Assert.That(session.Snapshot.Phase, Is.EqualTo(BattlePhase.Decision));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WarningMergeMapsIdentityAndRechecksTheActualAnchorRange(bool outside)
        {
            var data = Data();
            if (outside)
            {
                data.encounters[1].carryRange = 20;
                data.map.lanes.Single(x => x.id == "top").points[0].x = 180;
                data.map.lanes.Single(x => x.id == "bottom").points[0].x = 180;
            }
            var session = Session(data); Deploy(session); Deploy(session, "top"); Deploy(session, "bottom");
            AtWarning(session);
            var before = session.Snapshot;
            Commit(session, new SiegeOrder { MergeIds = before.Units.Select(x => x.Id).ToArray(),
                AnchorId = before.Units.Single(x => x.LaneId == "bottom").Id });
            int id = session.Snapshot.Units.Single().Id;
            Assert.That(session.Snapshot.AnimalState.IntentUnitId, Is.EqualTo(id));
            Until(session, x => x.Stats.Carries + x.Stats.CarryMisses > 0);
            Assert.That(session.Snapshot.Stats.Carries, Is.EqualTo(outside ? 0 : 1));
            Assert.That(session.Snapshot.Stats.CarryMisses, Is.EqualTo(outside ? 1 : 0));
            if (!outside) Assert.That(session.Snapshot.AnimalState.Cast.Target.UnitId, Is.EqualTo(id));
        }

        [Test]
        public void WarningTargetCanRerouteAwayButCannotBeReplacedByAnotherUnit()
        {
            var data = Data(); data.rules.rerouteDurationMs = 3000;
            data.contentUnits.Single(x => x.id == "bone").ability = "reroute";
            var session = Session(data); Deploy(session); Deploy(session, "top"); AtWarning(session);
            int target = session.Snapshot.AnimalState.IntentUnitId;
            Commit(session, new SiegeOrder { RerouteUnitId = target, RerouteLaneId = "bottom" });
            Until(session, x => x.Stats.CarryMisses == 1);
            Assert.That(session.Snapshot.Stats.Carries, Is.Zero);
            Assert.That(session.Snapshot.Units.All(x => !x.IsCarried), Is.True);
            Assert.That(session.Snapshot.AnimalState.SkillReadyAtMs(IntentKind.Carry),
                Is.GreaterThan(session.ElapsedMs));
            Assert.That(session.Snapshot.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Recovery));
        }

        [TestCase(700)]
        [TestCase(1050)]
        public void TargetLostBeforePickupDoesNotSwitchToTheSurvivingAlly(int shotPeriodMs)
        {
            var data = Data();
            data.map.pads.Single(x => x.id == "mid.outer").initial = true;
            data.encounters[1].initialTowerIds = new[] { "kennel" };
            var tower = data.towerTypes.Single(x => x.id == "kennel");
            tower.range = 1000; tower.damage = 40; tower.periodMs = shotPeriodMs;
            var session = Session(data); int target = Deploy(session); int ally = Deploy(session, "top");
            AtWarning(session);
            Assert.That(session.Snapshot.AnimalState.IntentUnitId, Is.EqualTo(target));
            Until(session, x => x.Stats.CarryMisses > 0);
            Assert.That(session.Snapshot.Units.Any(x => x.Id == target), Is.False);
            Assert.That(session.Snapshot.Units.Any(x => x.Id == ally), Is.True);
            Assert.That(session.Snapshot.Stats.Carries, Is.Zero);
        }

        [Test]
        public void FastActionGaugeCannotReleaseDuringCarryOrRecoveryAndSnapshotsStayDetached()
        {
            var data = Data(); foreach (var profile in data.animalStates) profile.periodMs = 100;
            var session = Session(data); Deploy(session);
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            var before = session.Snapshot;
            int end = before.AnimalState.Cast.UntilMs;
            Until(session, x => x.ElapsedMs >= end + 100);
            Assert.That(session.Snapshot.AnimalState.Actions, Is.EqualTo(before.AnimalState.Actions));
            Assert.That(before.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Active));
            Assert.That(before.Units.Single().IsCarried, Is.True);
            Assert.That(before.Units.Single().Distance, Is.EqualTo(before.Units.Single().CarryFromDistance));
            var other = Session(data);
            Assert.That(other.Snapshot.AnimalState.Cast, Is.Null);
            Assert.That(other.Snapshot.AnimalState.SkillReadyAtMs(IntentKind.Carry), Is.Zero);
            Until(session, x => x.AnimalState.Actions > before.AnimalState.Actions);
            Assert.That(session.ElapsedMs, Is.GreaterThanOrEqualTo(end + 150));
        }

        [Test]
        public void NaturalMergeCannotConsumeACarriedMaterialEvenWhenAllThreeAreClose()
        {
            var data = Data(); data.rules.mergeRadius = .5; data.rules.naturalMergeMinDistance = .6;
            foreach (var unit in data.contentUnits) unit.speed = 2;
            data.encounters[1].carryMinDistance = .1; data.encounters[1].carryDistance = 1;
            var session = Session(data); Deploy(session); Deploy(session); Deploy(session);
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            int end = session.Snapshot.AnimalState.Cast.UntilMs;
            bool close = false;
            Until(session, x =>
            {
                if (x.ElapsedMs >= end) return true;
                Assert.That(x.Stats.Merges, Is.Zero);
                close |= x.Units.All(a => x.Units.All(b => session.Position(a).Distance(session.Position(b)) <= .5));
                return false;
            });
            Assert.That(close, Is.True, "The fixture must actually enter natural merge range during carry.");
        }

        [Test]
        public void CatAndLegacyDoNotGainCarry()
        {
            foreach (string encounter in new[] { "cat", null })
            {
                var session = Session(encounter: encounter); Deploy(session);
                Until(session, x => x.ElapsedMs >= 10000);
                Assert.That(session.Snapshot.Stats.Carries, Is.Zero);
                Assert.That(session.Snapshot.AnimalState.Intent, Is.Not.EqualTo(IntentKind.Carry));
            }
        }

        [TestCase("single")]
        [TestCase("push")]
        public void CarriedUnitIsExcludedFromTowerFireKeepsBraceAndShieldAndCannotAttack(string attack)
        {
            var data = Data();
            data.encounters[1].unitIds = new[] { "bone", "yarn", "disc" };
            var bone = data.contentUnits.Single(x => x.id == "bone");
            bone.ability = "brace"; bone.reach = 20;
            var yarn = data.contentUnits.Single(x => x.id == "yarn");
            yarn.speed = .01; yarn.reach = .01; yarn.abilityRadius = 1000; yarn.abilityPeriodMs = 50;
            var pad = data.map.pads.Single(x => x.id == "mid.outer"); pad.initial = true; pad.at = .1;
            data.encounters[1].initialTowerIds = new[] { "kennel" };
            var tower = data.towerTypes.Single(x => x.id == "kennel");
            tower.hp = 10000; tower.range = 1000; tower.periodMs = 500; tower.attack = attack;
            if (attack == "push") { tower.warningMs = 200; tower.radius = 1000; tower.push = 1; }
            var catalog = new SiegeCatalog(data);
            var session = Enumerable.Range(1, 20)
                .Select(seed => new SiegeSession(catalog, (uint)seed, "calm", "carry-fire", "dog"))
                .First(s => s.Snapshot.Hand.Any(x => x.DefinitionId == "bone")
                    && s.Snapshot.Hand.Any(x => x.DefinitionId == "yarn"));
            Commit(session, new SiegeOrder
            {
                DeployCardId = session.Snapshot.Hand.First(x => x.DefinitionId == "bone").Id, DeployLaneId = "mid",
            });
            int id = session.Snapshot.Units.Single().Id; Next(session);
            Commit(session, new SiegeOrder
            {
                DeployCardId = session.Snapshot.Hand.First(x => x.DefinitionId == "yarn").Id, DeployLaneId = "top",
            });
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            var before = session.Snapshot;
            var carried = before.Units.Single(x => x.Id == id);
            Assert.That(before.AnimalState.CarriedUnitId, Is.EqualTo(id));
            Assert.That(before.Towers.Single().Hp, Is.LessThan(10000), "Must attack the tower before pickup.");
            Assert.That(carried.Shield, Is.EqualTo(1));
            int end = carried.CarriedUntilMs;
            Until(session, x => x.ElapsedMs >= end - 50);
            var after = session.Snapshot.Units.Single(x => x.Id == id);
            Assert.That(after.Hp, Is.EqualTo(carried.Hp));
            Assert.That(after.Shield, Is.EqualTo(carried.Shield));
            Assert.That(after.Brace, Is.EqualTo(carried.Brace));
            Assert.That(session.Snapshot.Towers.Single().Hp, Is.EqualTo(before.Towers.Single().Hp));
            Assert.That(session.Snapshot.Units.Single(x => x.Id != id).Hp,
                Is.LessThan(before.Units.Single(x => x.Id != id).Hp), "The tower must still fire at other units.");
        }

        [Test]
        public void ProtectionPreventsAnotherCarryUntilItExpires()
        {
            var data = Data(); data.encounters[1].carryProtectionMs = 6000;
            var session = Session(data); Deploy(session);
            Until(session, x => x.Stats.Carries == 1);
            Until(session, x => x.AnimalState.CarriedUnitId == 0);
            int protection = session.Snapshot.Units.Single().HoldProtectedUntilMs;
            Until(session, x => x.ElapsedMs >= protection - 50);
            Assert.That(session.Snapshot.Stats.Carries, Is.EqualTo(1));
            Until(session, x => x.Stats.Carries == 2);
            Assert.That(session.ElapsedMs, Is.GreaterThanOrEqualTo(protection));
        }

        [Test]
        public void PickupCancelsUnfinishedFirstContactBurstInsteadOfResumingItAfterLanding()
        {
            var data = Data();
            var bone = data.contentUnits.Single(x => x.id == "bone");
            bone.ability = "burst"; bone.burstHits = 3; bone.burstIntervalMs = 2000;
            bone.attackMs = 1000; bone.reach = 20;
            var pad = data.map.pads.Single(x => x.id == "mid.outer"); pad.initial = true; pad.at = .1;
            data.encounters[1].initialTowerIds = new[] { "kennel" };
            var tower = data.towerTypes.Single(x => x.id == "kennel"); tower.hp = 10000; tower.range = .01;
            var session = Session(data); Deploy(session);
            Until(session, x => x.AnimalState.CarriedUnitId != 0);
            double before = session.Snapshot.Towers.Single().Hp;
            Assert.That(before, Is.EqualTo(9998), "Only the first burst hit should precede pickup.");
            int end = session.Snapshot.Units.Single().CarriedUntilMs;
            Until(session, x => x.ElapsedMs >= end);
            Assert.That(session.Snapshot.Towers.Single().Hp, Is.EqualTo(before));
            Until(session, x => x.Towers.Single().Hp < before);
            int nextRegularAttack = session.ElapsedMs + bone.attackMs;
            Until(session, x => x.ElapsedMs >= nextRegularAttack);
            Assert.That(session.Snapshot.Towers.Single().Hp, Is.EqualTo(before - 4),
                "After landing, attacks use the normal period, not the cancelled burst interval.");
        }

        [Test]
        public void UnmodifiedDogEncountersCanActuallyCarry()
        {
            var data = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);
            int carries = 0;
            foreach (uint seed in new uint[] { 19, 132, 7183 })
            foreach (string lane in new[] { "top", "mid", "bottom" })
            foreach (bool merge in new[] { false, true })
            {
                var session = new SiegeSession(new SiegeCatalog(data), seed, "calm", "normal-carry", "dog");
                for (int tick = 0; tick < 3000 && session.Phase != BattlePhase.Won
                    && session.Phase != BattlePhase.Timeout; tick++)
                {
                    if (session.Phase == BattlePhase.Decision)
                    {
                        var s = session.Snapshot;
                        var order = new SiegeOrder();
                        if (s.Hand.Count > 0 && s.Energy >= 2)
                        { order.DeployCardId = s.Hand[0].Id; order.DeployLaneId = lane; }
                        var group = s.Units.Where(x => !x.IsCarried && x.TransitRemainingMs == 0
                                && !string.IsNullOrEmpty(session.Catalog.Unit(x.DefinitionId).UpgradeId))
                            .GroupBy(x => x.DefinitionId).FirstOrDefault(x => x.Count() >= 3);
                        if (merge && group != null && s.Energy >= (order.DeployCardId.HasValue ? 3 : 1))
                        {
                            order.MergeIds = group.Take(3).Select(x => x.Id).ToArray();
                            order.AnchorId = group.First().Id;
                        }
                        Commit(session, order);
                    }
                    session.Advance(50);
                }
                carries += session.Snapshot.Stats.Carries;
                TestContext.Out.WriteLine("Normal carry " + seed + "/" + lane + "/merge=" + merge + ": "
                    + session.Snapshot.Stats.Carries + " carried, " + session.Snapshot.Stats.CarryMisses + " missed, "
                    + session.Snapshot.Phase + ", progress=" + session.Snapshot.Progress);
            }
            Assert.That(carries, Is.GreaterThan(0));
        }
    }
}
