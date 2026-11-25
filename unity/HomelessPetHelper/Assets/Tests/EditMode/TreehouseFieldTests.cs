using System;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests
{
    public sealed class TreehouseFieldTests
    {
        private static OutingFieldLayout Layout() => new OutingFieldLayout(AssetDatabase.LoadAssetAtPath<TextAsset>(
            "Assets/Settings/Game/TreehouseFieldConfig.json"));

        private static ApproachSession Open(OutingFieldLayout layout, ApproachMode mode) =>
            layout.Opening.Create(Guid.NewGuid(), Enumerable.Range(0, 30).First(seed =>
                new System.Random(seed).Next(2) == (int)mode));

        private static void Clean(ApproachSession session, int cell)
        {
            for (int step = 0; step < 25 && session.DebrisAt(cell) >= 0; step++)
                Assert.That(session.TryClean(cell, ApproachTool.Litter, session.MaxStepSeconds, out var failure),
                    Is.True, $"{cell}: {failure}");
            Assert.That(session.DebrisAt(cell), Is.LessThan(0));
        }

        [Test]
        public void RevealsKittenUpstairsAndOnlyOpensDetourAfterClearingItsClutter()
        {
            var layout = Layout();
            using var opening = Open(layout, ApproachMode.Cleanup);
            Assert.That(opening.IsAnimalRevealed, Is.False);
            Assert.That(opening.TryClean(5, ApproachTool.Litter, .01, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.OutOfReach));
            foreach (int cell in new[] { 65, 57, 50, 3 }) Clean(opening, cell);
            Assert.That(opening.IsComplete, Is.False);
            Clean(opening, 5);
            Assert.That(opening.IsAnimalRevealed, Is.True);
            Assert.That(opening.IsComplete, Is.True);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var battle = content.CreateFieldSiegeCatalog(layout, opening);
            Assert.That(battle.Lanes.Where(lane => lane.Open).Select(lane => lane.Id), Is.EqualTo(new[] { "top" }));
            var goal = battle.Lanes[0].PointAt(battle.Lanes[0].Length);
            Assert.That(goal.X, Is.EqualTo(layout.CellPoint(50).x).Within(.01));
            Assert.That(goal.Y, Is.EqualTo(layout.CellPoint(50).y).Within(.01));
            Clean(opening, 51);
            var widened = content.CreateFieldSiegeCatalog(layout, opening);
            Assert.That(widened.Lanes.Select(lane => lane.Open), Is.EqualTo(new[] { true, true }));
            Assert.That(widened.Lanes[1].Length, Is.GreaterThan(widened.Lanes[0].Length));
        }

        [Test]
        public void NearRouteCanWinInRealtimeAndDetourEvadesTheActualOpeningTower()
        {
            var layout = Layout();
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            using var opening = Open(layout, ApproachMode.Cleanup);
            foreach (int cell in new[] { 65, 57, 50, 3, 5 }) Clean(opening, cell);
            var catalog = content.CreateFieldSiegeCatalog(layout, opening);
            var battle = new SiegeSession(catalog, 19, "calm", "treehouse-short", "cat");
            battle.EnableRealtime();
            var tower = battle.Snapshot.Towers.Single();
            var towerPoint = SiegeGeometry.Position(catalog, tower);
            double range = catalog.Content.Tower(tower.DefinitionId).Range;
            var direct = catalog.Lanes[0]; var detour = catalog.Lanes[1];
            Assert.That(towerPoint.Distance(direct.PointAt(direct.Project(towerPoint))), Is.LessThan(range));
            Assert.That(towerPoint.Distance(detour.PointAt(detour.Project(towerPoint))), Is.GreaterThan(range));
            var entry = direct.PointAt(0);
            Assert.That(SiegeDeployment.CanDeploy(catalog, entry), Is.True);
            Assert.That(SiegeDeployment.CanDeploy(catalog, new SiegePoint(entry.X, 250)), Is.False);
            for (int tick = 0; tick < 1500 && battle.IsAdvancing; tick++)
            {
                var state = battle.Snapshot;
                var card = state.Hand.FirstOrDefault(item => catalog.Unit(item.DefinitionId).Cost <= state.Energy);
                if (card != null)
                    Assert.That(battle.TryOrder(new SiegeOrder { SessionId = state.SessionId, Turn = state.Turn,
                        DeployCardId = card.Id, DeployPoint = entry }, true, out var reason, out _), Is.True, reason);
                battle.Advance(100);
            }
            Assert.That(battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won), $"progress {battle.Snapshot.Progress}");
            var restored = new SiegeSession(catalog, 19, "calm", "treehouse-short", "cat");
            restored.RestoreReplay(battle.CaptureReplay());
            Assert.That(restored.Snapshot.Progress, Is.EqualTo(battle.Snapshot.Progress));
        }

        [Test]
        public void SlidingRightBoxFirstOpensTheStairsAndUndoRestoresTheArrangement()
        {
            var layout = Layout();
            using var opening = Open(layout, ApproachMode.Puzzle);
            Assert.That(opening.TrySlideBox(57, 59, out _), Is.False);
            Assert.That(opening.TrySlideBox(59, 60, out _), Is.True);
            Assert.That(opening.TrySlideBox(57, 59, out _), Is.True);
            Assert.That(opening.IsComplete, Is.True);
            layout.ValidateBattleClear(opening);
            Assert.That(opening.TryUndo(out _), Is.True);
            Assert.That(opening.IsComplete, Is.False);
            Assert.That(opening.TryUndo(out _), Is.True);
            Assert.That(new FieldSceneSnapshot(opening).Boxes, Is.EqualTo(new[] { 57, 59 }));
        }
    }
}
