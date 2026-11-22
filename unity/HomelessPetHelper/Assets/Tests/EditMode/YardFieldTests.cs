using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class YardFieldTests
    {
        private const string LayoutPath = "Assets/Settings/Game/YardFieldConfig.json";
        private OutingFieldLayout Layout() => new OutingFieldLayout(
            AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath));

        [Test]
        public void PointCatalogSelectsIndependentGeometryAndRejectsBadBindings()
        {
            var yard = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            var data = JsonUtility.FromJson<OutingFieldLayout.Data>(yard.text);
            data.pointId = "parking";
            data.grid.x = 50; data.grid.width = 240;
            var parking = new TextAsset(JsonUtility.ToJson(data));
            try
            {
                var catalog = new OutingFieldCatalog(new[] { yard, parking }, new[] { "yard", "parking" });
                Assert.That(catalog.Get("yard").PointId, Is.EqualTo("yard"));
                Assert.That(catalog.Get("parking").Portal(0), Is.Not.EqualTo(catalog.Get("yard").Portal(0)));
                Assert.That(catalog.Get("porch"), Is.Null, "Unconfigured places retain legacy presentation.");
                Assert.Throws<ArgumentException>(() => new OutingFieldCatalog(new[] { yard, yard },
                    new[] { "yard" }));
                Assert.Throws<ArgumentException>(() => new OutingFieldCatalog(new[] { parking }, new[] { "yard" }));
            }
            finally { UnityEngine.Object.DestroyImmediate(parking); }
        }
        [TestCase(0)]
        [TestCase(1)]
        public void BothOpeningsClearAllBattleRoutesAndSnapshotsSurviveClosing(int seed)
        {
            var layout = Layout(); var session = layout.Opening.Create(Guid.NewGuid(), seed);
            Assert.Throws<InvalidOperationException>(() => layout.ValidateBattleClear(session));
            if (session.Template.Mode == ApproachMode.Puzzle)
            {
                Assert.That(session.TrySlideBox(43, 44, out _), Is.True);
                Assert.That(session.IsComplete, Is.False);
                Assert.That(session.TryPlaceBridge(36, out _), Is.True);
            }
            else
            {
                Clean(session, 43, ApproachTool.Litter); Clean(session, 36, ApproachTool.Weeds);
                Assert.That(session.DebrisAt(45), Is.GreaterThanOrEqualTo(0), "Optional side weeds remain.");
            }
            Assert.That(session.IsComplete, Is.True); Assert.DoesNotThrow(() => layout.ValidateBattleClear(session));
            var snapshot = new FieldSceneSnapshot(session); session.Dispose();
            Assert.That(snapshot.Template.Id, Does.StartWith("field.yard."));
            if (seed == 1)
            {
                Assert.That(snapshot.Bridge, Is.EqualTo(36));
                Assert.That(snapshot.Boxes, Is.EqualTo(new[] { 44 }));
            }
            else Assert.That(snapshot.Remaining.Count(value => value > 0), Is.EqualTo(2));
            var fresh = layout.Opening.Create(Guid.NewGuid(), seed);
            Assert.That(fresh.IsComplete, Is.False); Assert.That(fresh.BridgeCell, Is.EqualTo(-1));
            Assert.Throws<InvalidOperationException>(() => layout.ValidateBattleClear(fresh)); fresh.Dispose();
        }
        private static void Clean(ApproachSession session, int cell, ApproachTool tool)
        {
            for (int i = 0; i < 100 && session.DebrisAt(cell) >= 0; i++)
                Assert.That(session.TryClean(cell, tool, session.MaxStepSeconds, out _), Is.True);
        }
        [Test]
        public void DerivedBattleUsesSharedFrameWithoutChangingOriginalCatalog()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var original = content.CreateSiegeCatalog(); var layout = Layout();
            var battle = content.CreateFieldSiegeCatalog(layout);
            Assert.That(original.Height, Is.EqualTo(360)); Assert.That(battle.Height, Is.EqualTo(layout.Height));
            var animal = layout.CellPoint(layout.Opening.Get(ApproachMode.Puzzle).Animal);
            foreach (var lane in battle.Lanes)
            {
                Assert.That(lane.Points.Count, Is.EqualTo(12));
                var end = lane.PointAt(lane.Length);
                Assert.That(end.X, Is.EqualTo(animal.x).Within(.001));
                Assert.That(end.Y, Is.EqualTo(animal.y).Within(.001));
            }
            Assert.That(Enumerable.Range(0, 3).Select(layout.Portal).Distinct().Count(), Is.EqualTo(3));
        }
        [TestCase("wall")]
        [TestCase("jump")]
        [TestCase("duplicate")]
        [TestCase("nan")]
        public void MalformedGeometryIsRejected(string fault)
        {
            var sourceData = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            var data = JsonUtility.FromJson<OutingFieldLayout.Data>(sourceData.text);
            if (fault == "wall") data.routes[0].cells[4] = 23;
            if (fault == "jump") data.routes[0].cells[4] = 8;
            if (fault == "duplicate") data.routes[1].laneId = data.routes[0].laneId;
            if (fault == "nan") data.grid.x = float.NaN;
            var source = new TextAsset(JsonUtility.ToJson(data));
            try { Assert.Throws<ArgumentException>(() => new OutingFieldLayout(source)); }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        [TestCase("cat", 0u)]
        [TestCase("dog", 0u)]
        [TestCase("cat", 1u)]
        [TestCase("dog", 1u)]
        [TestCase("cat", 20261122u)]
        [TestCase("dog", 20261122u)]
        public void SharedYardCanCompleteExistingBattleRules(string encounter, uint seed)
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var battle = new
                SiegeSession(content.CreateFieldSiegeCatalog(Layout()), seed, "calm", "yard-solvable", encounter);
            for (int step = 0; step < 1000 && battle.Phase != SiegeState.BattlePhase.Won &&
                battle.Phase != SiegeState.BattlePhase.Timeout; step++)
            {
                if (battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var state = battle.Snapshot; var card = state.Hand.First();
                    var order = new SiegeOrder
                    {
                        SessionId = state.SessionId,
                        Turn = state.Turn,
                        DeployCardId = card.Id,
                        DeployLaneId = battle.Catalog.Lanes[state.Turn % 3].Id
                    };
                    if (!battle.TryOrder(order, true, out _, out _))
                        Assert.That(battle.TryOrder(new
                            SiegeOrder
                        { SessionId = state.SessionId, Turn = state.Turn }, true, out _, out _), Is.True);
                }
                battle.Advance(1000);
            }
            Assert.That(battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won), encounter + "/" + seed + ": " +
                battle.Snapshot.Progress);
        }
    }
}
