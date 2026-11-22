using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Settings;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class StrategyDecisionTests
    {
        private static SiegeSession Battle() => new SiegeSession(AssetDatabase.LoadAssetAtPath<GameContent>(
            "Assets/Settings/Game/GameContent.asset").CreateSiegeCatalog(), 1, "calm", "cadence", "cat");
        private static void Advance(SiegeSession battle, int milliseconds)
        {
            for (int elapsed = 0; elapsed < milliseconds; elapsed += 100)
                battle.Advance(System.Math.Min(100, milliseconds - elapsed));
        }
        private static SiegeOrder Wait(SiegeSession battle) => new SiegeOrder
        { SessionId = battle.Snapshot.SessionId, Turn = battle.Snapshot.Turn };

        [Test]
        public void DefaultDecisionPausesButContinuousDecisionAdvancesAndCanTimeout()
        {
            var battle = Battle();
            Advance(battle, 1000);
            Assert.That(battle.ElapsedMs, Is.Zero);
            battle.SetPauseAtDecision(false);
            Advance(battle, battle.Catalog.Rules.DurationMs);
            Assert.That(battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Timeout));
            Assert.That(battle.Snapshot.Turn, Is.EqualTo(1));
        }

        [Test]
        public void ContinuousReadyTurnGrantsOnceAndAcceptsExactlyOneFreshOrder()
        {
            var battle = Battle(); battle.SetPauseAtDecision(false);
            Assert.That(battle.TryOrder(Wait(battle), true, out _, out _), Is.True);
            Advance(battle, battle.Catalog.Rules.PlayerPeriodMs);
            var ready = battle.Snapshot;
            Assert.That(ready.Phase, Is.EqualTo(SiegeState.BattlePhase.Decision));
            Assert.That(ready.Turn, Is.EqualTo(2));
            Advance(battle, battle.Catalog.Rules.PlayerPeriodMs * 2);
            var waiting = battle.Snapshot;
            Assert.That(waiting.Turn, Is.EqualTo(ready.Turn));
            Assert.That(waiting.Hand.Select(card => card.Id), Is.EqualTo(ready.Hand.Select(card => card.Id)));
            Assert.That(waiting.Energy, Is.EqualTo(ready.Energy));
            Assert.That(waiting.PlayerChargeMs, Is.EqualTo(battle.Catalog.Rules.PlayerPeriodMs));
            var order = Wait(battle);
            Assert.That(battle.TryOrder(order, true, out _, out _), Is.True);
            Assert.That(battle.TryOrder(order, true, out _, out _), Is.False);
            Assert.That(battle.Snapshot.PlayerChargeMs, Is.Zero);
            Advance(battle, battle.Catalog.Rules.PlayerPeriodMs);
            Assert.That(battle.TryOrder(order, true, out var reason, out _), Is.False);
            Assert.That(reason, Is.EqualTo("reason.stale"));
        }

        [Test]
        public void ExplicitPauseGuidanceHoldAndSwitchingCadenceRemainIndependent()
        {
            var battle = Battle(); battle.SetPauseAtDecision(false);
            battle.SetDecisionHold(true); Advance(battle, 1000);
            Assert.That(battle.ElapsedMs, Is.Zero);
            battle.SetDecisionHold(false); battle.SetPaused(true); Advance(battle, 1000);
            Assert.That(battle.ElapsedMs, Is.Zero);
            battle.SetPaused(false); Advance(battle, 1000);
            Assert.That(battle.ElapsedMs, Is.EqualTo(1000));
            battle.SetPauseAtDecision(true); Advance(battle, 1000);
            Assert.That(battle.ElapsedMs, Is.EqualTo(1000));
            Assert.That(battle.TryOrder(Wait(battle), true, out _, out _), Is.True);
            Advance(battle, battle.Catalog.Rules.PlayerPeriodMs * 2);
            Assert.That(battle.ElapsedMs, Is.EqualTo(1000 + battle.Catalog.Rules.PlayerPeriodMs));
        }

        [Test]
        public void AnimalStillCastsResolvesAndRepeatsWhilePlayerWaitsInContinuousDecision()
        {
            var battle = Battle(); battle.SetPauseAtDecision(false);
            Advance(battle, 40000);
            Assert.That(battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Decision));
            Assert.That(battle.Snapshot.Turn, Is.EqualTo(1));
            Assert.That(battle.Snapshot.AnimalState.Actions, Is.GreaterThanOrEqualTo(3),
                "Later decision-gated skills also need to run without a submitted turn.");
            Assert.That(battle.Snapshot.AnimalState.GuardLane, Is.Not.Null,
                "Beginning a skill must also resolve it in Decision.");
            battle.SetPaused(true); int actions = battle.Snapshot.AnimalState.Actions;
            Advance(battle, 5000); Assert.That(battle.Snapshot.AnimalState.Actions, Is.EqualTo(actions));
            Assert.That(battle.IsAdvancing, Is.False);
        }

        [Test]
        public void PlayPreferenceDefaultsPersistsAndRetriesWithoutLosingLiveValue()
        {
            var storage = new MemorySaveStorage();
            var preferences = new PlaySettingsSession(storage);
            Assert.That(preferences.PauseAtDecision, Is.True);
            Assert.That(storage.Writes, Is.Zero);
            int changed = 0; preferences.Changed += _ => changed++;
            storage.FailWrites = true;
            Assert.That(preferences.TrySave(false), Is.False);
            Assert.That(preferences.PauseAtDecision, Is.True);
            Assert.That(changed, Is.Zero);
            storage.FailWrites = false;
            Assert.That(preferences.TrySave(false), Is.True);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(new PlaySettingsSession(storage).PauseAtDecision, Is.False);
        }

        [Test]
        public void CorruptPreferenceIsPreservedAndCannotOverwriteOriginals()
        {
            var storage = new MemorySaveStorage(); storage.Slots[0] = "broken-original";
            var preferences = new PlaySettingsSession(storage);
            Assert.That(preferences.PersistenceError, Is.Not.Null);
            Assert.That(preferences.TrySave(false), Is.False);
            Assert.That(storage.Slots[0], Is.EqualTo("broken-original"));
            Assert.That(storage.Writes, Is.Zero);
        }
    }
}
