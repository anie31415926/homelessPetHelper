#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class SiegeLoadoutPlayTests
    {
        private const string DataPath = "Assets/Settings/Siege/SiegeLoadoutTrial.json";
        private const string ScenePath = "Assets/EditorScenes/Siege/SC_SiegeTrial.unity";

        [Serializable]
        private sealed class TrialData { public SiegeCatalogData catalog = null; }

        private static SiegeSession Create(string encounter, Action<SiegeCatalogData> setup = null)
        {
            var data = JsonUtility.FromJson<TrialData>(AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath).text);
            setup?.Invoke(data.catalog);
            var battle = new SiegeSession(new SiegeCatalog(data.catalog), 20261124, "calm", "three-in-hand", encounter);
            battle.EnableRealtime();
            return battle;
        }

        private static SiegeOrder Deploy(SiegeSession session, string id)
        {
            var state = session.Snapshot;
            return new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                DeployCardId = state.Hand.Single(card => card.DefinitionId == id).Id,
                DeployPoint = session.Catalog.Lanes[0].PointAt(0) };
        }

        private static void Commit(SiegeSession session, SiegeOrder order, int price)
        {
            Assert.That(session.TryOrder(order, true, out var reason, out int cost), Is.True, reason);
            Assert.That(cost, Is.EqualTo(price));
        }

        [Test]
        public void RotationCommitsInOrderAndTwoMaterialMergeReplays()
        {
            var battle = Create("breakthrough");
            Assert.That(battle.Snapshot.Hand.Select(card => card.DefinitionId),
                Is.EqualTo(new[] { "trialFish", "trialFeather", "heavyCart" }));
            Assert.That(battle.Snapshot.NextCardDefinitionId, Is.EqualTo("trialYarn"));

            Commit(battle, Deploy(battle, "trialFish"), 2);
            var blocked = Deploy(battle, "heavyCart");
            Assert.That(battle.TryOrder(blocked, true, out var reason, out _), Is.False);
            Assert.That(reason, Is.EqualTo("reason.energy"));
            Assert.That(battle.Snapshot.NextCardDefinitionId, Is.EqualTo("clip"));
            Commit(battle, Deploy(battle, "trialFeather"), 1);
            battle.Advance(1000);
            Commit(battle, Deploy(battle, "trialYarn"), 2);
            Assert.That(battle.Snapshot.Hand.Select(card => card.DefinitionId),
                Is.EqualTo(new[] { "heavyCart", "clip", "trialFish" }));
            battle.Advance(1000); battle.Advance(1000);
            Commit(battle, Deploy(battle, "trialFish"), 2);
            var before = battle.Snapshot;
            int[] ids = before.Units.Where(unit => unit.DefinitionId == "trialFish").Select(unit => unit.Id).ToArray();
            Assert.That(ids.Length, Is.EqualTo(2));
            battle.Advance(1000);
            Commit(battle, new SiegeOrder { SessionId = before.SessionId, Turn = battle.Snapshot.Turn,
                MergeIds = ids, AnchorId = ids[0] }, 1);

            var after = battle.Snapshot;
            Assert.That(after.Units.Count(unit => unit.DefinitionId == "bouquet"), Is.EqualTo(1));
            var restored = new SiegeSession(battle.Catalog, 20261124, "calm", after.SessionId, "breakthrough");
            restored.RestoreReplay(battle.CaptureReplay());
            Assert.That(restored.Snapshot.Units.Select(unit => unit.DefinitionId),
                Is.EqualTo(after.Units.Select(unit => unit.DefinitionId)));
            Assert.That(restored.Snapshot.NextCardDefinitionId, Is.EqualTo(after.NextCardDefinitionId));
            var presentation = new SiegeState();
            battle.CopyPresentationTo(presentation);
            Assert.That(presentation.NextCardDefinitionId, Is.EqualTo(after.NextCardDefinitionId));
        }

        [Test]
        public void ExpensiveCardsSpendTheirEffectsOnlyWhenAnEligibleTargetExists()
        {
            var charge = Create("breakthrough", data =>
            {
                foreach (var pad in data.map.pads) pad.initial = pad.id == "mid.outer";
                data.contentUnits.Single(unit => unit.id == "heavyCart").reach = 500;
            });
            double initialHp = charge.Snapshot.Towers.Single().Hp;
            Commit(charge, Deploy(charge, "heavyCart"), 4);
            charge.Advance(50);
            Assert.That(charge.Snapshot.Towers.Single().Hp, Is.EqualTo(initialHp - 8));
            charge.Advance(1000);
            Assert.That(charge.Snapshot.Towers.Single().Hp, Is.EqualTo(initialHp - 10));

            var escort = Create("sustain", data =>
            {
                foreach (var pad in data.map.pads) pad.initial = pad.id == "mid.outer";
                var defense = data.towerTypes.Single(tower => tower.id == "thread");
                defense.attack = "single";
                defense.damage = 1;
                defense.range = 500;
            });
            Commit(escort, Deploy(escort, "trialFish"), 2);
            escort.Advance(1000);
            double injured = escort.Snapshot.Units.Single().Hp;
            Assert.That(injured, Is.LessThan(escort.Catalog.Unit("trialFish").Hp));
            Commit(escort, Deploy(escort, "scarf"), 3);
            escort.Advance(50);
            Assert.That(escort.Snapshot.Units.Single(unit => unit.DefinitionId == "trialFish").Hp,
                Is.EqualTo(escort.Catalog.Unit("trialFish").Hp));
            Assert.That(escort.Snapshot.Units.Single(unit => unit.DefinitionId == "scarf").AbilityCharges,
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BothMenuEntriesRenderTheRealtimeFieldAndNextCard()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters());
            var view = Object.FindObjectOfType<SiegeTrialView>();
            Assert.That(view, Is.Not.Null);
            try
            {
                foreach (string mode in new[] { "Sustain", "Breakthrough" })
                {
                    string menu = "Homeless Pet Helper/Preview/Five Card Siege/" + mode;
                    Assert.That(EditorApplication.ExecuteMenuItem(menu), Is.True);
                    yield return null;
                    Assert.That(view.Scene.isActiveAndEnabled, Is.True);
                    var labels = view.GetComponentsInChildren<Text>();
                    Assert.That(labels.Any(label => label.text.StartsWith("下一张：")), Is.True);
                    LogAssert.NoUnexpectedReceived();
                }
            }
            finally { view.gameObject.SetActive(false); }
        }
    }
}
#endif
