using System;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Station;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class ApproachTests
    {
        private ApproachCatalog _catalog;
        private GameContent _content;
        private ApproachCatalogData Data() => JsonUtility.FromJson<ApproachCatalogData>(
            AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/ApproachConfig.json").text);

        [SetUp]
        public void Load()
        {
            _catalog = new ApproachCatalog(Data());
            _content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
        }

        private ApproachSession Open(ApproachMode mode, Guid? id = null) =>
            new ApproachSession(id ?? Guid.NewGuid(), 17, _catalog.Get(mode), _catalog);

        [Test]
        public void CatalogFreezesInputAndSeedSelectionReproducesBothModes()
        {
            var input = Data();
            var catalog = new ApproachCatalog(input);
            input.templates[0].rows[1] = "#######";
            input.templates[0].boxes[0] = 8;
            Assert.That(catalog.Get(ApproachMode.Puzzle).Terrain(8), Is.EqualTo('.'));
            Assert.That(catalog.Get(ApproachMode.Puzzle).Boxes[0], Is.EqualTo(17));
            var modes = new System.Collections.Generic.HashSet<ApproachMode>();
            for (int seed = 0; seed < 40; seed++)
            {
                using var a = catalog.Create(Guid.NewGuid(), seed);
                using var b = catalog.Create(Guid.NewGuid(), seed);
                Assert.That(a.Template, Is.SameAs(b.Template));
                modes.Add(a.Template.Mode);
            }
            Assert.That(modes.Count, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => catalog.Create(Guid.Empty, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.Create(Guid.NewGuid(), -1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        public void InvalidCatalogIsRejected(int defect)
        {
            var data = Data();
            switch (defect)
            {
                case 0: data.schemaVersion++; break;
                case 1: data.templates[1].id = data.templates[0].id; break;
                case 2: data.templates[1].mode = ApproachMode.Puzzle; break;
                case 3: data.templates[0].rows[1] = "bad"; break;
                case 4: data.templates[0].animal = data.templates[0].entrance; break;
                case 5: data.templates[0].boxes[0] = data.templates[0].animal; break;
                case 6: data.templates[1].debris[0].workSeconds = double.NaN; break;
                case 7: data.templates[1].debris[0].tool = ApproachTool.Move; break;
                case 8: data.templates[0].bridgeCells[0] = 0; break;
                case 9: data.maxStepSeconds = double.PositiveInfinity; break;
                case 10: data.templates[1].debris[1].cell = data.templates[1].debris[0].cell; break;
                case 11: data.undoLimit = 0; break;
            }
            Assert.Throws<ArgumentException>(() => new ApproachCatalog(data));
        }

        [Test]
        public void PuzzleBlocksCrossingObstaclesAndSupportsBridgeMoveAndUndo()
        {
            using var route = Open(ApproachMode.Puzzle);
            Assert.That(route.IsComplete, Is.False);
            Assert.That(route.TrySlideBox(17, 16, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.Blocked));
            Assert.That(route.Version, Is.Zero);
            Assert.That(route.TrySlideBox(17, 25, out _), Is.False, "Diagonal movement is invalid.");
            Assert.That(route.TrySlideBox(17, 24, out _), Is.True);
            Assert.That(route.TrySlideBox(24, 23, out _), Is.True);
            Assert.That(route.TrySlideBox(23, 25, out _), Is.True, "Straight slides may exceed one cell.");
            Assert.That(route.TryUndo(out _), Is.True);
            Assert.That(route.HasBox(23), Is.True);
            Assert.That(route.TryPlaceBridge(32, out _), Is.False);
            Assert.That(route.TryPlaceBridge(33, out _), Is.True);
            Assert.That(route.IsComplete, Is.True);
            Assert.That(route.TryUndo(out _), Is.True);
            Assert.That(route.IsComplete, Is.False, "Completion is rechecked after undo.");
        }

        [Test]
        public void CratesNeverCrossTheAnimalEntranceOrAnotherCrate()
        {
            using var route = Open(ApproachMode.Puzzle);
            Assert.That(route.TrySlideBox(36, 8, out _), Is.False);
            Assert.That(route.TrySlideBox(36, 38, out _), Is.False, "Wall in the entire slide path.");
            Assert.That(route.TrySlideBox(-1, 7, out _), Is.False);
            Assert.That(route.HasBox(36), Is.True);
            Assert.That(route.CanUndo, Is.False);
        }

        [Test]
        public void CleanupNeedsCorrectToolAndReachableBoundaryAndOnlyChargesEffectiveContact()
        {
            using var route = Open(ApproachMode.Cleanup);
            int sheltered = route.DebrisAt(9), entry = route.DebrisAt(45);
            double before = route.RemainingWork(entry);
            Assert.That(route.TryClean(9, ApproachTool.Weeds, .05, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.OutOfReach));
            Assert.That(route.TryClean(45, ApproachTool.Weeds, .05, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.WrongTool));
            Assert.That(route.RemainingWork(entry), Is.EqualTo(before));
            Assert.That(route.TryClean(45, ApproachTool.Litter, .05, out _), Is.True);
            Assert.That(route.RemainingWork(entry), Is.EqualTo(before - .05).Within(1e-10));
            Assert.That(route.RemainingWork(sheltered), Is.EqualTo(.8));
            route.SetSuspended(true);
            Assert.That(route.TryClean(45, ApproachTool.Litter, .05, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.Suspended));
            route.SetSuspended(false);
            Complete(route);
            Assert.That(route.IsComplete, Is.True);
            Assert.That(Enumerable.Range(0, route.Template.Debris.Count)
                .Count(i => route.RemainingWork(i) > 0), Is.GreaterThan(0), "A safe passage is enough.");
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(.051)] [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidContactDurationIsAtomic(double seconds)
        {
            using var route = Open(ApproachMode.Cleanup);
            Assert.That(route.TryClean(45, ApproachTool.Litter, seconds, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.InvalidDuration));
            Assert.That(route.Version, Is.Zero);
            Assert.That(route.RemainingWork(route.DebrisAt(45)), Is.EqualTo(.24));
        }

        [TestCase(ApproachMode.Puzzle)] [TestCase(ApproachMode.Cleanup)]
        public void RouteOwnerRejectsPrematureWrongSessionPausedAndRepeatedCompletion(ApproachMode mode)
        {
            using var progress = new MainProgressSession(_content.CreateMapConfig(), _content.Artwork);
            var loop = progress.Session;
            Guid id = Depart(loop);
            using var wrong = Open(mode);
            Assert.That(loop.TryStartOpeningRoute(wrong, out _), Is.False);
            var route = Open(mode, id);
            Assert.That(loop.TryStartOpeningRoute(route, out _), Is.True);
            using var activity = Rescue(id);
            Assert.That(loop.TryEnterRescueAfterOpening(activity, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.RouteNotComplete));
            Assert.That(loop.Flow.Stage, Is.EqualTo(RescueStage.Approach));
            Assert.That(loop.Activity, Is.Null);
            Assert.That(loop.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(loop.TryEnterRouteFreeRescue(activity, out _), Is.False);
            loop.SetSuspended(true);
            Assert.That(route.IsSuspended, Is.True);
            loop.SetSuspended(false);
            Complete(route);
            loop.SetSuspended(true);
            Assert.That(loop.TryEnterRescueAfterOpening(activity, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(OutingAssemblyFailure.Suspended));
            loop.SetSuspended(false);
            using var stale = Rescue(Guid.NewGuid());
            Assert.That(loop.TryEnterRescueAfterOpening(stale, out _), Is.False);
            Assert.That(loop.TryEnterRescueAfterOpening(activity, out _), Is.True);
            Assert.That(loop.OpeningRoute, Is.Null);
            Assert.That(route.IsClosed, Is.True);
            Assert.That(loop.TryEnterRescueAfterOpening(activity, out _), Is.False);
            Assert.That(loop.TryConfirmRescue(out _), Is.False, "A cleared route is not rescue victory.");
            Assert.That(loop.TryReturnUnresolved(out _), Is.True);
            Assert.That(loop.Clock.Current.ElapsedMinutes, Is.EqualTo(30));
            Assert.That(loop.Station.Animals, Is.Empty);
            Assert.That(loop.TryReturnUnresolved(out _), Is.False);
            Assert.That(loop.Clock.Current.ElapsedMinutes, Is.EqualTo(30));
        }

        [TestCase(ApproachMode.Puzzle)] [TestCase(ApproachMode.Cleanup)]
        public void EarlyReturnDisposesRouteAndRescuedReturnSettlesThirtyFiveMinutesOnce(ApproachMode mode)
        {
            using var progress = new MainProgressSession(_content.CreateMapConfig(), _content.Artwork);
            var loop = progress.Session;
            Guid id = Depart(loop);
            var route = Open(mode, id);
            Assert.That(loop.TryStartOpeningRoute(route, out _), Is.True);
            Assert.That(loop.TryReturnUnresolved(out _), Is.True);
            Assert.That(route.IsClosed, Is.True);
            Assert.That(loop.Clock.Current.ElapsedMinutes, Is.EqualTo(15));
            Assert.That(loop.Requests.Requests.Single(r => r.TemplateId == "ordinary").State,
                Is.EqualTo(RequestState.Available));
            id = Depart(loop);
            route = Open(mode, id);
            Assert.That(loop.TryStartOpeningRoute(route, out _), Is.True);
            Complete(route);
            var activity = Rescue(id);
            Assert.That(loop.TryEnterRescueAfterOpening(activity, out _), Is.True);
            Win(activity);
            Assert.That(loop.TryConfirmRescue(out _), Is.True);
            Assert.That(loop.TryReturnRescued(new AnimalIntakeCondition(StationHealthAssessment.Unchecked,
                65, Array.Empty<string>()), out _), Is.True);
            Assert.That(loop.Clock.Current.ElapsedMinutes, Is.EqualTo(15 + 35));
            Assert.That(loop.TryReturnRescued(new AnimalIntakeCondition(StationHealthAssessment.Unchecked,
                65, Array.Empty<string>()), out _), Is.False);
            Assert.That(loop.Clock.Current.ElapsedMinutes, Is.EqualTo(50));
            Assert.That(loop.Station.Animals.Single().MatchesCurrentIndividual(activity.Individual), Is.True);
        }

        [Test]
        public void DisposalStopsFurtherPuzzleAndCleaningCommands()
        {
            using var puzzle = Open(ApproachMode.Puzzle);
            puzzle.Dispose(); puzzle.Dispose();
            Assert.That(puzzle.TrySlideBox(17, 24, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.Closed));
            Assert.That(puzzle.TryUndo(out _), Is.False);
            using var cleanup = Open(ApproachMode.Cleanup);
            cleanup.Dispose();
            Assert.That(cleanup.TryClean(45, ApproachTool.Litter, .05, out failure), Is.False);
            Assert.That(failure, Is.EqualTo(ApproachFailure.Closed));
        }

        private Guid Depart(GameLoopSession loop)
        {
            Guid id = Guid.NewGuid();
            Assert.That(loop.TryOpenMap(id, 17, out _), Is.True);
            Assert.That(loop.Outing.TrySelectAndDepart(id, "parking", out _), Is.True);
            return id;
        }

        private SiegeRescueActivity Rescue(Guid id) => new SiegeRescueActivity(id, 17,
            _content.CreateSiegeCatalog(), _content.CreateMapConfig().GetRescue("ordinary"));

        private static void Complete(ApproachSession route)
        {
            if (route.Template.Mode == ApproachMode.Puzzle)
            {
                Assert.That(route.TrySlideBox(17, 24, out _), Is.True);
                Assert.That(route.TrySlideBox(24, 23, out _), Is.True);
                Assert.That(route.TryPlaceBridge(33, out _), Is.True);
            }
            else foreach (int cell in new[] { 45, 38, 24, 9 })
            {
                int index = route.DebrisAt(cell);
                if (index < 0) continue;
                for (int sample = 0; sample < 120 && route.RemainingWork(index) > 0; sample++)
                    Assert.That(route.TryClean(cell, route.Template.Debris[index].Tool,
                        route.MaxStepSeconds, out _), Is.True);
            }
            Assert.That(route.IsComplete, Is.True);
        }

        private static void Win(SiegeRescueActivity activity)
        {
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var snapshot = activity.Battle.Snapshot;
                    var order = new SiegeOrder { SessionId = snapshot.SessionId, Turn = snapshot.Turn };
                    if (snapshot.Hand.Count > 0 && snapshot.Energy >= 2)
                    { order.DeployCardId = snapshot.Hand[0].Id; order.DeployLaneId = "top"; }
                    Assert.That(activity.Battle.TryOrder(order, true, out var reason, out _), Is.True, reason);
                }
                activity.Battle.Advance(1000);
            }
            Assert.That(activity.Battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won));
            activity.SynchronizeOutcome();
        }
    }
}
