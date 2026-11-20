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
    public sealed class SiegeContentTests
    {
        private SiegeCatalogData Data() => JsonUtility.FromJson<SiegeCatalogData>(
            AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);

        private SiegeCatalogData QuietData()
        {
            var data = Data();
            data.rules.playerPeriodMs = 1000;
            data.rules.openingEnergy = 6;
            data.rules.animalBuilds = 0;
            foreach (var profile in data.animalStates) profile.periodMs = 1000000;
            foreach (var tower in data.towerTypes) { tower.range = .01; tower.damage = 0; }
            return data;
        }

        private SiegeSession Session(SiegeCatalogData data, string encounter = "cat") =>
            new SiegeSession(new SiegeCatalog(data), 19, "calm", "content", encounter);

        private void Commit(SiegeSession session, SiegeOrder order = null)
        {
            var snapshot = session.Snapshot;
            order = order ?? new SiegeOrder();
            order.SessionId = snapshot.SessionId;
            order.Turn = snapshot.Turn;
            Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
        }

        private void Next(SiegeSession session)
        {
            for (int i = 0; i < 10000 && session.Phase == BattlePhase.Running; i++) session.Advance(50);
            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Decision));
        }

        private int Deploy(SiegeSession session, string definition, string lane = "mid")
        {
            for (int i = 0; i < 30; i++)
            {
                var card = session.Snapshot.Hand.FirstOrDefault(x => x.DefinitionId == definition);
                if (card != null)
                {
                    Commit(session, new SiegeOrder { DeployCardId = card.Id, DeployLaneId = lane });
                    int id = session.Snapshot.Units.Last().Id;
                    return id;
                }
                Commit(session, new SiegeOrder { DiscardCardId = session.Snapshot.Hand[0].Id });
                Next(session);
            }
            Assert.Fail("Supply never produced " + definition);
            return 0;
        }

        [Test]
        public void CatalogRejectsUnknownReferencesAndCopiesEncounterLists()
        {
            var data = Data();
            var catalog = new SiegeCatalog(data);
            data.encounters[0].unitIds[0] = "absent";
            Assert.That(catalog.Content.Encounter("cat").UnitIds[0], Is.EqualTo("fish"));
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(data));
            data = Data();
            data.towerTypes[2].warningMs = 0;
            Assert.Throws<ArgumentException>(() => new SiegeCatalog(data));
            Assert.Throws<ArgumentException>(() => new SiegeSession(catalog, 1, "calm", "id", "absent"));
        }

        [TestCase("cat")]
        [TestCase("dog")]
        public void SupplyIsDeterministicAndContainsOnlyThreeConfiguredBases(string encounter)
        {
            var first = Session(QuietData(), encounter);
            var second = Session(QuietData(), encounter);
            var allowed = first.Catalog.Content.Encounter(encounter).UnitIds;
            for (int turn = 0; turn < 12; turn++)
            {
                var a = first.Snapshot;
                Assert.That(a.Hand.All(x => allowed.Contains(x.DefinitionId)), Is.True);
                Assert.That(a.Hand.Select(x => x.DefinitionId),
                    Is.EqualTo(second.Snapshot.Hand.Select(x => x.DefinitionId)));
                foreach (var s in new[] { first, second })
                {
                    Commit(s, new SiegeOrder { DiscardCardId = s.Snapshot.Hand[0].Id });
                    Next(s);
                }
            }
        }

        [TestCase("cat", "fish", "bouquet")]
        [TestCase("dog", "bone", "boneWheel")]
        public void PaidMergeUsesConfiguredResultAndAnchor(string encounter, string baseId, string upgrade)
        {
            var data = QuietData();
            data.rules.playerPeriodMs = 50;
            foreach (var u in data.contentUnits) u.speed = 1;
            var session = Session(data, encounter);
            int first = Deploy(session, baseId); Next(session);
            int second = Deploy(session, baseId, "top"); Next(session);
            int third = Deploy(session, baseId, "bottom"); Next(session);
            var anchor = session.Snapshot.Units.Single(x => x.Id == first);
            Commit(session, new SiegeOrder { MergeIds = new[] { first, second, third }, AnchorId = first });
            var result = session.Snapshot.Units.Single();
            Assert.That(result.DefinitionId, Is.EqualTo(upgrade));
            Assert.That(result.Distance, Is.EqualTo(anchor.Distance));
            Assert.That(session.Snapshot.Stats.Merges, Is.EqualTo(1));
            Assert.That(session.Snapshot.Effects.Count(x => x.Kind == "mergePart"), Is.EqualTo(3));
        }

        [Test]
        public void SlowZoneReducesMovementAndFreezesWithTheBattle()
        {
            var slow = QuietData();
            var fast = QuietData();
            foreach (var d in new[] { slow, fast })
            {
                var tower = d.towerTypes.Single(x => x.id == "thread");
                tower.range = 1000; tower.radius = 1000; tower.periodMs = 100000; tower.zoneDurationMs = 5000;
            }
            slow.towerTypes.Single(x => x.id == "thread").slowFactor = .5;
            fast.towerTypes.Single(x => x.id == "thread").slowFactor = .95;
            var a = Session(slow); var b = Session(fast);
            Deploy(a, "fish"); Deploy(b, "fish");
            for (int i = 0; i < 10; i++) { a.Advance(50); b.Advance(50); }
            Assert.That(a.Snapshot.Units.Single().Distance, Is.LessThan(b.Snapshot.Units.Single().Distance));
            Assert.That(a.Snapshot.SlowZones.Count, Is.EqualTo(1));
            a.SetPaused(true);
            var before = a.Snapshot;
            a.Advance(1000);
            Assert.That(a.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(a.Snapshot.SlowZones[0].UntilMs, Is.EqualTo(before.SlowZones[0].UntilMs));
        }

        [Test]
        public void PushTowerWaitsForAnActualPlayerDecisionAndLocksGroundNotTarget()
        {
            var data = QuietData();
            data.encounters[0].initialTowerIds = new[] { "scratch" };
            var tower = data.towerTypes.Single(x => x.id == "scratch");
            tower.range = 1000; tower.radius = 1000; tower.warningMs = 50; tower.damage = 2;
            var session = Session(data);
            Deploy(session, "fish");
            session.Advance(50);
            Assert.That(session.Snapshot.Towers.All(x => x.WindingUp), Is.True);
            var point = session.Snapshot.Towers[0].LockedPoint;
            Next(session);
            Assert.That(session.Snapshot.Units.Single().Hp, Is.EqualTo(7));
            Assert.That(session.Snapshot.Towers[0].LockedPoint, Is.EqualTo(point));
            Commit(session); session.Advance(50);
            Assert.That(session.Snapshot.Units.Single().Hp, Is.EqualTo(3));
            Assert.That(session.Snapshot.Effects.Count(x => x.Kind == "push"), Is.EqualTo(2));
        }

        [Test]
        public void DestroyingWindingTowerCancelsItsPendingHit()
        {
            var data = QuietData();
            data.encounters[0].initialTowerIds = new[] { "scratch" };
            var tower = data.towerTypes.Single(x => x.id == "scratch");
            tower.range = 1000; tower.hp = 2; tower.radius = 1000; tower.warningMs = 50; tower.damage = 3;
            var fish = data.contentUnits.Single(x => x.id == "fish");
            fish.reach = 1000; fish.attackMs = 100; fish.damage = 1;
            var session = Session(data);
            Deploy(session, "fish");
            Next(session);
            Commit(session); session.Advance(50);
            Assert.That(session.Snapshot.Towers.Single(x => x.LaneId == "mid").Status,
                Is.EqualTo(TowerStatus.Destroyed));
            Assert.That(session.Snapshot.Effects.Count(x => x.Kind == "push"), Is.EqualTo(1));
            Assert.That(session.Snapshot.Units.Single().Hp, Is.EqualTo(4));
        }

        [Test]
        public void FirstContactBurstHasThreeHitsNotPermanentTripleFire()
        {
            var data = QuietData();
            foreach (var t in data.towerTypes) t.hp = 1000;
            var bone = data.contentUnits.Single(x => x.id == "bone");
            bone.ability = "burst"; bone.burstHits = 3; bone.burstIntervalMs = 200; bone.reach = 1000;
            var session = Session(data, "dog");
            Deploy(session, "bone");
            for (int i = 0; i < 12; i++) session.Advance(50);
            Assert.That(session.Snapshot.Towers.Single(x => x.LaneId == "mid").Hp, Is.EqualTo(994));
            Next(session); Commit(session); session.Advance(500);
            Assert.That(session.Snapshot.Towers.Single(x => x.LaneId == "mid").Hp, Is.EqualTo(994));
        }

        [Test]
        public void RopeAuraGrantsTwoNonStackingBracesOnlyOnce()
        {
            var data = QuietData();
            data.rules.playerPeriodMs = 50;
            var rope = data.contentUnits.Single(x => x.id == "rope");
            rope.ability = "braceAura"; rope.abilityRadius = 1000; rope.auraTargets = 2; rope.auraRearmMs = 8000;
            var session = Session(data, "dog");
            Deploy(session, "bone"); Next(session);
            Deploy(session, "disc"); Next(session);
            Deploy(session, "rope"); Next(session);
            Assert.That(session.Snapshot.Units.All(x => x.Brace == 1), Is.True);
            Assert.That(session.Snapshot.Units.Single(x => x.DefinitionId == "rope").AbilityCharges, Is.Zero);
            Assert.That(session.Snapshot.Log.Count(x => x == "event.braced"), Is.EqualTo(1));
            Commit(session); Next(session);
            Assert.That(session.Snapshot.Log.Count(x => x == "event.braced"), Is.EqualTo(1));
        }

        [Test]
        public void DogLeapFreezesAndAppliesAreaImpactOnlyAtLanding()
        {
            var data = QuietData();
            data.rules.playerPeriodMs = 100;
            foreach (var p in data.animalStates) p.periodMs = 50;
            var session = Session(data, "dog");
            Commit(session); Next(session); Commit(session); session.Advance(50);
            var before = session.Snapshot;
            Assert.That(before.AnimalState.ImpactAtMs, Is.GreaterThan(before.ElapsedMs));
            Assert.That(before.Effects.Any(x => x.Kind == "sweep"), Is.False);
            session.SetPaused(true); session.Advance(1000);
            Assert.That(session.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            session.SetPaused(false);
            for (int i = 0; i < 20 && session.Snapshot.AnimalState.ImpactAtMs > 0; i++)
            {
                if (session.Phase == BattlePhase.Decision) Commit(session);
                session.Advance(50);
            }
            Assert.That(session.Snapshot.AnimalState.ImpactAtMs, Is.Zero);
            Assert.That(session.Snapshot.Effects.Count(x => x.Kind == "sweep"), Is.EqualTo(1));
            Assert.That(session.Snapshot.AnimalState.Actions, Is.EqualTo(2));
        }
    }
}
