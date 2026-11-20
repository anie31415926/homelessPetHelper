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
    public sealed class SiegeAnimalCastingTests
    {
        private SiegeSession Session(string encounter)
        {
            var d = JsonUtility.FromJson<SiegeCatalogData>(
                AssetDatabase.LoadAssetAtPath<TextAsset>(SiegeTrialAssets.DataPath).text);
            d.rules.playerPeriodMs = 200; d.rules.openingEnergy = 6;
            d.rules.animalBuilds = 0; d.rules.animalRepairs = 0;
            d.rules.slapDamage = .01; d.rules.sweepDamage = .01;
            d.rules.sweepPush = 0; d.rules.slapPush = 0;
            foreach (var p in d.animalStates) p.periodMs = 100;
            foreach (var t in d.towerTypes) { t.range = .01; t.damage = 0; t.hp = 10000; }
            foreach (var u in d.contentUnits) { u.hp = 100; u.speed = 1; }
            var hold = d.encounters[0];
            hold.holdOriginX = 40; hold.holdOriginY = 310; hold.holdRange = 500;
            hold.holdDurationMs = 600; hold.holdRecoveryMs = 150;
            hold.holdCooldownMs = 1000; hold.holdProtectionMs = 2000;
            return new SiegeSession(new SiegeCatalog(d), 132, "calm", "casting", encounter);
        }

        private void Commit(SiegeSession session, bool deploy = false)
        {
            var s = session.Snapshot;
            var order = new SiegeOrder { SessionId = s.SessionId, Turn = s.Turn };
            if (deploy) { order.DeployCardId = s.Hand[0].Id; order.DeployLaneId = "mid"; }
            Assert.That(session.TryOrder(order, true, out string reason, out _), Is.True, reason);
        }

        private void Until(SiegeSession session, Func<SiegeState, bool> predicate)
        {
            for (int i = 0; i < 1000 && !predicate(session.Snapshot); i++)
            {
                if (session.Phase == BattlePhase.Decision) Commit(session);
                session.Advance(50);
            }
            Assert.That(predicate(session.Snapshot), Is.True);
        }

        [TestCase("cat", IntentKind.Hold)]
        [TestCase("dog", IntentKind.Sweep)]
        public void FullGaugeCannotBypassTheCommonPlayerWindow(string encounter, IntentKind skill)
        {
            var s = Session(encounter); Commit(s, true);
            Until(s, x => x.AnimalState.Intent == skill && x.Phase == BattlePhase.Decision);
            var before = s.Snapshot;
            Assert.That(before.AnimalState.Charge, Is.EqualTo(1));
            Assert.That(before.AnimalState.Cast, Is.Null);
            s.Advance(1000);
            Assert.That(s.Snapshot.AnimalState.Actions, Is.EqualTo(before.AnimalState.Actions));
            Assert.That(s.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Commit(s); s.Advance(50);
            Assert.That(s.Snapshot.AnimalState.Actions, Is.EqualTo(before.AnimalState.Actions + 1));
            Assert.That(s.Snapshot.AnimalState.Cast.Kind, Is.EqualTo(skill));
            Assert.That(s.Snapshot.AnimalState.Cast.Target.LaneId, Is.EqualTo(before.AnimalState.IntentLane));
        }

        [Test]
        public void DelayedImpactUsesItsOwnContextResolvesOnceAndFreezesInWindup()
        {
            var s = Session("dog"); Commit(s, true);
            Until(s, x => x.AnimalState.Cast?.Phase == CastPhase.Windup);
            var before = s.Snapshot;
            var cast = before.AnimalState.Cast;
            Assert.That(cast.Kind, Is.EqualTo(IntentKind.Sweep));
            Assert.That(cast.ResolveAtMs - cast.StartedMs, Is.EqualTo(500));
            Assert.That(before.AnimalState.Intent, Is.Not.EqualTo(cast.Kind));
            Assert.That(before.AnimalState.ImpactAtMs, Is.EqualTo(cast.ResolveAtMs));
            s.SetPaused(true); s.Advance(1000);
            Assert.That(s.Snapshot.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Windup));
            Assert.That(s.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            s.SetPaused(false);
            Until(s, x => x.ElapsedMs >= cast.ResolveAtMs);
            var after = s.Snapshot;
            Assert.That(after.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Recovery));
            Assert.That(after.Effects.Single(x => x.Kind == "sweep").From, Is.EqualTo(cast.Point));
            Assert.That(after.AnimalState.ImpactAtMs, Is.Zero);
            int count = after.Log.Count(x => x == "event.sweep");
            Until(s, x => x.ElapsedMs >= cast.ResolveAtMs + 200);
            Assert.That(s.Snapshot.Log.Count(x => x == "event.sweep"), Is.EqualTo(count));
            Assert.That(before.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Windup));
            Assert.That(ReferenceEquals(before.AnimalState.Cast, s.Snapshot.AnimalState.Cast), Is.False);
        }

        [Test]
        public void ActorOccupationRecoveryAndCooldownAreIndependentSharedClocks()
        {
            var s = Session("cat"); Commit(s, true);
            Until(s, x => x.AnimalState.Cast?.Kind == IntentKind.Hold);
            var before = s.Snapshot;
            var cast = before.AnimalState.Cast;
            Assert.That(cast.Phase, Is.EqualTo(CastPhase.Active));
            Assert.That(cast.UntilMs - cast.StartedMs, Is.EqualTo(600));
            Assert.That(before.AnimalState.NextHoldMs, Is.EqualTo(cast.StartedMs + 1000));
            Assert.That(before.AnimalState.SkillReadyAtMs(IntentKind.Hold), Is.EqualTo(before.AnimalState.NextHoldMs));
            Until(s, x => x.ElapsedMs >= cast.UntilMs);
            var released = s.Snapshot;
            Assert.That(released.AnimalState.HeldUnitId, Is.Zero);
            Assert.That(released.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Recovery));
            Assert.That(released.AnimalState.RecoveryUntilMs, Is.EqualTo(cast.UntilMs + 150));
            Assert.That(released.AnimalState.Actions, Is.EqualTo(before.AnimalState.Actions));
            Until(s, x => x.ElapsedMs >= released.AnimalState.RecoveryUntilMs - 50);
            Assert.That(s.Snapshot.AnimalState.Actions, Is.EqualTo(before.AnimalState.Actions));
            Until(s, x => x.AnimalState.Actions > before.AnimalState.Actions);
            Assert.That(s.ElapsedMs, Is.GreaterThanOrEqualTo(released.AnimalState.RecoveryUntilMs));
            Assert.That(before.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Active));
            Until(s, x => x.Stats.Holds >= 2);
            Assert.That(s.Snapshot.AnimalState.NextHoldMs, Is.GreaterThan(before.AnimalState.NextHoldMs));
            Assert.That(before.AnimalState.NextHoldMs, Is.EqualTo(cast.StartedMs + 1000));
        }

        [Test]
        public void SharedSkillHandlersNeverShareCastOrCooldownBetweenSessions()
        {
            var first = Session("cat"); Commit(first, true);
            Until(first, x => x.Stats.Holds == 1);
            var before = first.Snapshot;
            var second = new SiegeSession(first.Catalog, 132, "calm", "second", "cat");
            Assert.That(second.Snapshot.AnimalState.Cast, Is.Null);
            Assert.That(second.Snapshot.AnimalState.NextHoldMs, Is.Zero);
            Assert.That(second.Snapshot.AnimalState.Actions, Is.Zero);
            Commit(second, true);
            Until(second, x => x.Stats.Holds == 1);
            Assert.That(ReferenceEquals(first.Snapshot.AnimalState.Cast, second.Snapshot.AnimalState.Cast), Is.False);
            Until(second, x => x.Stats.Holds == 2);
            Assert.That(first.ElapsedMs, Is.EqualTo(before.ElapsedMs));
            Assert.That(first.Snapshot.AnimalState.Cast.Phase, Is.EqualTo(CastPhase.Active));
            Assert.That(first.Snapshot.AnimalState.NextHoldMs, Is.EqualTo(before.AnimalState.NextHoldMs));
        }
    }
}
