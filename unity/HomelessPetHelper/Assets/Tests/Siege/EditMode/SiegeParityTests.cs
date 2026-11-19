using System;
using System.Linq;
using HomelessPetHelper.Editor.Siege;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.Siege.EditMode
{
    public sealed class SiegeParityTests
    {
        [Serializable]
        public sealed class Fixture
        {
            public Case[] cases;
        }

        [Serializable]
        public sealed class Case
        {
            public uint seed;
            public string profile,
                policy;
            public Step[] steps;
        }

        [Serializable]
        public sealed class Step
        {
            public Order order;
            public Expected expected;
        }

        [Serializable]
        public sealed class Order
        {
            public int turn,
                deployId,
                anchor,
                rerouteId;
            public string lane,
                destination;
            public int[] ids;
        }

        [Serializable]
        public sealed class Expected
        {
            public string phase;
            public int elapsed,
                turn,
                energy,
                deployed,
                merges,
                lost,
                delivered,
                repairs;
            public double progress;
            public Card[] hand;
            public Unit[] units;
            public Tower[] towers;
            public Animal animal;
        }

        [Serializable]
        public sealed class Card
        {
            public int id;
            public string definition;
        }

        [Serializable]
        public sealed class Unit
        {
            public int id,
                shield,
                brace,
                reroutes,
                transit;
            public string definition,
                lane;
            public double distance,
                hp;
        }

        [Serializable]
        public sealed class Tower
        {
            public string id,
                status;
            public double hp;
            public int ready;
        }

        [Serializable]
        public sealed class Area
        {
            public double x,
                y,
                radius;
        }

        [Serializable]
        public sealed class Animal
        {
            public double charge;
            public int actions,
                seen,
                until,
                recovery,
                builds,
                repairs,
                repairReady;
            public string intent,
                lane,
                pad,
                guard,
                repairId;
            public Area area;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        [TestCase(13)]
        [TestCase(14)]
        [TestCase(15)]
        [TestCase(16)]
        [TestCase(17)]
        public void UnityMatchesHtmlAtEveryRecordedCheckpoint(int index)
        {
            var catalog = new SiegeCatalog(
                JsonUtility.FromJson<SiegeCatalogData>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text
                )
            );
            var fixture = JsonUtility
                .FromJson<Fixture>(
                    AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Tests/Siege/Fixtures/SiegeParity.json").text
                )
                .cases[index];
            var session = new SiegeSession(catalog, fixture.seed, fixture.profile, "parity");
            foreach (var step in fixture.steps)
            {
                if (step.order != null && step.order.turn > 0)
                {
                    var o = step.order;
                    var command = new SiegeOrder
                    {
                        SessionId = "parity",
                        Turn = o.turn,
                        DeployCardId = o.deployId > 0 ? o.deployId : (int?)null,
                        DeployLaneId = o.deployId > 0 ? o.lane : null,
                        MergeIds = o.ids.Length > 0 ? o.ids : null,
                        AnchorId = o.anchor,
                        RerouteUnitId = o.rerouteId > 0 ? o.rerouteId : (int?)null,
                        RerouteLaneId = o.rerouteId > 0 ? o.destination : null,
                    };
                    Assert.That(session.TryOrder(command, true, out string reason, out _), Is.True, reason);
                }
                session.Advance(1000);
                Compare(session.Snapshot, step.expected);
            }
        }

        private void Compare(SiegeState s, Expected e)
        {
            Assert.That(s.Phase.ToString().ToLowerInvariant(), Is.EqualTo(e.phase));
            Assert.That(s.ElapsedMs, Is.EqualTo(e.elapsed));
            Assert.That(s.Turn, Is.EqualTo(e.turn));
            Assert.That(s.Energy, Is.EqualTo(e.energy));
            Near(s.Progress, e.progress);
            Assert.That(
                new[] { s.Stats.Deployed, s.Stats.Merges, s.Stats.Lost, s.Stats.Delivered, s.Stats.Repairs },
                Is.EqualTo(new[] { e.deployed, e.merges, e.lost, e.delivered, e.repairs })
            );
            Assert.That(s.Hand.Select(x => x.Id), Is.EqualTo(e.hand.Select(x => x.id)));
            Assert.That(s.Hand.Select(x => x.DefinitionId), Is.EqualTo(e.hand.Select(x => x.definition)));
            Assert.That(s.Units.Count, Is.EqualTo(e.units.Length));
            for (int i = 0; i < e.units.Length; i++)
            {
                var a = s.Units[i];
                var b = e.units[i];
                Assert.That(
                    new[] { a.Id, a.Shield, a.Brace, a.Reroutes, a.TransitRemainingMs },
                    Is.EqualTo(new[] { b.id, b.shield, b.brace, b.reroutes, b.transit })
                );
                Assert.That(a.DefinitionId, Is.EqualTo(b.definition));
                Assert.That(a.LaneId, Is.EqualTo(b.lane));
                Near(a.Distance, b.distance);
                Near(a.Hp, b.hp);
            }
            Assert.That(s.Towers.Count, Is.EqualTo(e.towers.Length));
            for (int i = 0; i < e.towers.Length; i++)
            {
                var a = s.Towers[i];
                var b = e.towers[i];
                Assert.That(a.Id, Is.EqualTo(b.id));
                Assert.That(a.Status.ToString().ToLowerInvariant(), Is.EqualTo(b.status));
                Near(a.Hp, b.hp);
                Assert.That(a.ReadyAtMs, Is.EqualTo(b.ready));
            }
            var animal = s.AnimalState;
            var expected = e.animal;
            Near(animal.Charge, expected.charge);
            Assert.That(
                new[]
                {
                    animal.Actions,
                    animal.SeenTurn,
                    animal.GuardUntilMs,
                    animal.RecoveryUntilMs,
                    animal.BuildsLeft,
                    animal.RepairsLeft,
                },
                Is.EqualTo(
                    new[]
                    {
                        expected.actions,
                        expected.seen,
                        expected.until,
                        expected.recovery,
                        expected.builds,
                        expected.repairs,
                    }
                )
            );
            Assert.That(animal.Intent.ToString().ToLowerInvariant(), Is.EqualTo(expected.intent));
            Assert.That(animal.IntentLane, Is.EqualTo(expected.lane));
            Assert.That(animal.IntentPad ?? "", Is.EqualTo(expected.pad));
            Assert.That(animal.GuardLane ?? "", Is.EqualTo(expected.guard));
            Assert.That(animal.RepairTowerId ?? "", Is.EqualTo(expected.repairId));
            if (animal.RepairTowerId != null)
                Assert.That(animal.RepairReadyAtMs, Is.EqualTo(expected.repairReady));
            if (animal.Intent == SiegeState.IntentKind.Sweep)
            {
                Near(animal.SweepCenter.X, expected.area.x);
                Near(animal.SweepCenter.Y, expected.area.y);
                Near(animal.SweepRadius, expected.area.radius);
            }
        }

        private static void Near(double actual, double expected) =>
            Assert.That(actual, Is.EqualTo(expected).Within(1e-7));
    }
}
