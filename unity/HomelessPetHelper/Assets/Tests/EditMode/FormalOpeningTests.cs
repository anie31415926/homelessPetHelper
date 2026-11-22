using System;
using System.Collections.Generic;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class FormalOpeningTests
    {
        private static OutingFieldLayout Layout(string name) => new OutingFieldLayout(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/" + name + "FieldConfig.json"));
        private static ApproachSession Create(OutingFieldLayout field, ApproachMode mode) =>
            new ApproachSession(Guid.NewGuid(), 1, field.Opening.Get(mode), field.Opening);
        private sealed class Node
        {
            public int[] Boxes; public int Bridge; public List<int[]> Moves;
        }

        // Breadth first search calls the real rule owner for every edge; it cannot silently diverge
        // from free-slide/single-plank validation as an independent imitation of those rules could.
        [TestCase("Parking", 3)] [TestCase("YardFormal", 4)] [TestCase("Porch", 3)]
        public void PuzzleHasRequiredDecisionDepthAndEveryCompletedPositionAllowsBattle(string name, int minimum)
        {
            var field = Layout(name); var template = field.Opening.Get(ApproachMode.Puzzle);
            var queue = new Queue<Node>(); var visited = new HashSet<string>();
            queue.Enqueue(new Node { Boxes = template.Boxes.ToArray(), Bridge = -1, Moves = new List<int[]>() });
            int shortest = int.MaxValue, completed = 0;
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                using var session = Create(field, ApproachMode.Puzzle);
                foreach (var move in node.Moves) Apply(session, move);
                if (session.IsComplete)
                {
                    completed++; shortest = Math.Min(shortest, node.Moves.Count);
                    Assert.DoesNotThrow(() => field.ValidateBattleClear(session));
                }
                foreach (int site in template.BridgeCells) TryNext(new[] { -1, site });
                foreach (int box in node.Boxes)
                    for (int to = 0; to < template.CellCount; to++) TryNext(new[] { box, to });
                void TryNext(int[] move)
                {
                    bool accepted = move[0] < 0 ? session.TryPlaceBridge(move[1], out _) :
                        session.TrySlideBox(move[0], move[1], out _);
                    if (!accepted) return;
                    int[] boxes = node.Boxes.Select(cell => cell == move[0] ? move[1] : cell).ToArray();
                    int bridge = session.BridgeCell;
                    string key = string.Join(",", boxes.OrderBy(cell => cell)) + ":" + bridge;
                    if (visited.Add(key))
                        queue.Enqueue(new Node { Boxes = boxes, Bridge = bridge,
                            Moves = node.Moves.Concat(new[] { move }).ToList() });
                    Assert.That(session.TryUndo(out _), Is.True);
                }
            }
            Assert.That(completed, Is.GreaterThan(0)); Assert.That(shortest, Is.EqualTo(minimum));
        }
        private static void Apply(ApproachSession session, int[] move)
        {
            bool accepted = move[0] < 0 ? session.TryPlaceBridge(move[1], out _) :
                session.TrySlideBox(move[0], move[1], out _);
            Assert.That(accepted, Is.True);
        }

        [TestCase("Parking", new[] { 45, 38 })]
        [TestCase("YardFormal", new[] { 45, 40, 33 })]
        [TestCase("Porch", new[] { 45, 40, 33 })]
        public void CleanupExpandsReachAndLeavesOptionalWork(string name, int[] cells)
        {
            var field = Layout(name); using var session = Create(field, ApproachMode.Cleanup);
            Assert.That(session.CanReachDebris(session.DebrisAt(cells.Last())), Is.False);
            foreach (int cell in cells)
            {
                Assert.That(session.IsComplete, Is.False);
                int debris = session.DebrisAt(cell);
                var tool = session.Template.Debris[debris].Tool;
                while (session.DebrisAt(cell) >= 0)
                    Assert.That(session.TryClean(cell, tool, session.MaxStepSeconds, out _), Is.True);
            }
            Assert.That(session.IsComplete, Is.True);
            Assert.That(session.Template.Debris.Any(item => session.DebrisAt(item.Cell) >= 0), Is.True);
            Assert.DoesNotThrow(() => field.ValidateBattleClear(session));
        }

        [Test]
        public void ReachingAnimalAloneDoesNotCompleteWhenAnotherLaneHasACrate()
        {
            var field = Layout("Yard"); using var session = Create(field, ApproachMode.Puzzle);
            Assert.That(session.TryPlaceBridge(36, out _), Is.True);
            Assert.That(session.TrySlideBox(43, 1, out _), Is.True);
            Assert.That(session.IsReachable(session.Template.Animal), Is.True);
            Assert.That(session.IsComplete, Is.False);
            Assert.That(session.TrySlideBox(1, 43, out _), Is.True);
            Assert.That(session.TrySlideBox(43, 44, out _), Is.True);
            Assert.That(session.IsComplete, Is.True);
        }

        [TestCase("parking")] [TestCase("yard")] [TestCase("porch")]
        public void EditorSampleComposesInMemoryWithoutChangingOriginalRequestRules(string point)
        {
            var original = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            string before = EditorJsonUtility.ToJson(original);
            var sample = Editor.LevelUi.FieldSamplePreview.CreateContent(original, point);
            var mapSource = new SerializedObject(sample).FindProperty("_mapConfig").objectReferenceValue;
            try
            {
                Assert.DoesNotThrow(sample.Validate);
                Assert.That(sample.CreateMapConfig().InitialPointId, Is.EqualTo(point));
                Assert.That(sample.CreateMapConfig().GetRescue(point == "parking" ? "ordinary" : "ordinary-dog"),
                    Is.Not.Null);
                Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(mapSource); UnityEngine.Object.DestroyImmediate(sample); }
        }

        [Test]
        public void SavedContentIncludesFormalSamplesAndKeepsTeachingExplicit()
        {
            var fields = AssetDatabase.LoadAssetAtPath<GameContent>(
                "Assets/Settings/Game/GameContent.asset").CreateFields();
            Assert.That(fields.Layouts.Count(), Is.EqualTo(4));
            Assert.That(fields.Get("yard").LayoutId, Is.EqualTo("yard.staging"));
            Assert.That(fields.Get("yard", introductory: true).LayoutId, Is.EqualTo("yard.main"));
            Assert.That(fields.Get("parking"), Is.Not.Null); Assert.That(fields.Get("porch"), Is.Not.Null);
        }
    }
}
