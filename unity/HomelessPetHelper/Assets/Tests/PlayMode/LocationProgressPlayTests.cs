#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Game.Progress;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class LocationProgressPlayTests
    {
        private MemorySaveStorage _storage;
        private MainSceneStorageScope _scope;
        private MainGameRoot _root;
        private ApproachPanelView _opening;
        private SiegeTrialView _siege;
        private int _seed;

        [SetUp] public void SetUp()
        {
            _storage = new MemorySaveStorage();
            _scope = new MainSceneStorageScope(() => _storage);
            SceneManager.sceneLoaded += Configure;
        }

        [TearDown] public void TearDown()
        {
            SceneManager.sceneLoaded -= Configure;
            _scope.Dispose();
        }

        private void Configure(Scene scene, LoadSceneMode mode)
        {
            foreach (var game in scene.GetRootGameObjects().SelectMany(item =>
                item.GetComponentsInChildren<MainGameRoot>(true))) game.ConfigureOutingSeedSource(() => _seed);
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Main"); yield return null;
            _root = Object.FindObjectOfType<MainGameRoot>();
            _opening = Object.FindObjectOfType<ApproachPanelView>(true);
            _siege = Object.FindObjectOfType<SiegeTrialView>(true);
            Assert.That(_root.Presenter.Progress.CanContinue, Is.True,
                _root.Presenter.Progress.PersistenceError?.ToString());
        }

        private void Depart()
        {
            MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
            var map = Object.FindObjectOfType<CityMapView>();
            MainApproachDriver.Click(map, "Parking");
            MainApproachDriver.Click(map, "Primary");
        }

        private void Choose(ApproachMode mode) => _seed = Enumerable.Range(0, 100)
            .First(value => new System.Random(value).Next(2) == (int)mode);

        [UnityTest] public IEnumerator PuzzleRestoresSameEventCratesAndUndo() => OpeningRoundTrip(ApproachMode.Puzzle);
        [UnityTest] public IEnumerator CleanupRestoresPartialBrushWork() => OpeningRoundTrip(ApproachMode.Cleanup);

        private IEnumerator OpeningRoundTrip(ApproachMode mode)
        {
            Choose(mode); yield return Load(); Depart();
            var animal = _root.Presenter.OutingAnimal;
            var outing = _root.Presenter.Session.Outing.Session.SessionId;
            var request = _root.Presenter.Session.Outing.Session.AcceptedRequestId;
            string template = _opening.Session.Template.Id;
            double remaining = 0;
            int beforeAction = _storage.Writes;
            if (mode == ApproachMode.Puzzle) MainApproachDriver.Drag(_opening, 46, 47);
            else
            {
                var pointer = MainApproachDriver.Pointer(_opening, 45);
                _opening.Board.OnPointerDown(pointer);
                _opening.AdvanceContact(_opening.Session.MaxStepSeconds);
                _opening.Board.OnPointerUp(pointer);
                remaining = _opening.Session.RemainingWork(_opening.Session.DebrisAt(45));
            }
            Assert.That(_storage.Writes, Is.GreaterThan(beforeAction), "Pointer completion is durable before exit.");
            Assert.That(_root.Presenter.Progress.PersistenceError, Is.Null);
            yield return Load();
            Assert.That(_opening.isActiveAndEnabled, Is.True);
            Assert.That(_opening.Session.Template.Id, Is.EqualTo(template));
            Assert.That(_root.Presenter.OutingAnimal.AnimalId, Is.EqualTo(animal.AnimalId));
            Assert.That(_root.Presenter.OutingAnimal.Coat.Dirtiness, Is.EqualTo(animal.Coat.Dirtiness));
            Assert.That(_root.Presenter.Session.Outing.Session.SessionId, Is.EqualTo(outing));
            Assert.That(_root.Presenter.Session.Outing.Session.AcceptedRequestId, Is.EqualTo(request));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
            if (mode == ApproachMode.Puzzle)
            {
                Assert.That(_opening.Session.HasBox(47), Is.True);
                MainApproachDriver.Click(_opening, "Undo");
                Assert.That(_opening.Session.HasBox(46), Is.True);
            }
            else Assert.That(_opening.Session.RemainingWork(_opening.Session.DebrisAt(45)), Is.EqualTo(remaining));
            MainApproachDriver.Solve(_opening);
            MainApproachDriver.Click(_opening, "Continue");
            Assert.That(_siege.isActiveAndEnabled, Is.True, "restored stage=" +
                _root.Presenter.Session.Flow.Stage + "; recovered=" + _root.Presenter.Progress.Recovered);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator UnresolvedReturnRetainsIdentityLayoutAndMovedPropsEvenWhenNewOutingSeedChanges()
        {
            Choose(ApproachMode.Puzzle); yield return Load(); Depart();
            var animalId = _root.Presenter.OutingAnimal.AnimalId;
            var oldOuting = _root.Presenter.Session.Outing.Session.SessionId;
            MainApproachDriver.Drag(_opening, 46, 47);
            MainApproachDriver.Click(_opening, "Pause");
            MainApproachDriver.Click(_opening, "Return");
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.GreaterThan(0));
            yield return Load(); _seed += 100; Depart();
            Assert.That(_opening.Session.Template.Mode, Is.EqualTo(ApproachMode.Puzzle));
            Assert.That(_opening.Session.HasBox(47), Is.True);
            Assert.That(_root.Presenter.OutingAnimal.AnimalId, Is.EqualTo(animalId));
            Assert.That(_root.Presenter.Session.Outing.Session.SessionId, Is.Not.EqualTo(oldOuting));
            Assert.That(_opening.Session.CanUndo, Is.True);
        }

        [UnityTest]
        public IEnumerator BattleAndConfirmedRescueResumeAndStationSettlementCannotRepeatAfterReload()
        {
            Choose(ApproachMode.Puzzle); yield return Load(); Depart();
            var animalId = _root.Presenter.OutingAnimal.AnimalId;
            MainApproachDriver.Complete();
            var activity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            Commit(activity.Battle);
            activity.Battle.Advance(777);
            activity.SetSuspended(true);
            _root.Presenter.FlushFieldProgress();
            var saved = activity.Battle.Snapshot;
            var detachedStorage = new MemorySaveStorage();
            Array.Copy(_storage.Slots, detachedStorage.Slots, 2);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            using (var detached = new MainProgressSession(content.CreateMapConfig(), content.Artwork, detachedStorage,
                content.CreateFieldProgressContent(content.CreateFields())))
            {
                Assert.That(detached.CanContinue, Is.True, detached.PersistenceError?.ToString());
                Assert.That(detached.Session.Flow.Stage, Is.EqualTo(RescueStage.Negotiation));
                Assert.That(((SiegeRescueActivity)detached.Session.Activity).Battle.ElapsedMs,
                    Is.EqualTo(saved.ElapsedMs));
                Assert.That(detached.TryCheckpoint(), Is.True, detached.PersistenceError?.ToString());
            }
            yield return Load();
            activity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            Assert.That(_siege.isActiveAndEnabled, Is.True, "battle restored stage=" +
                _root.Presenter.Session.Flow.Stage + "; recovered=" + _root.Presenter.Progress.Recovered +
                "; activity=" + (activity != null) + "; opening=" + _opening.isActiveAndEnabled);
            Assert.That(activity.Individual.Current.AnimalId, Is.EqualTo(animalId));
            Assert.That(activity.Battle.ElapsedMs, Is.EqualTo(saved.ElapsedMs));
            Assert.That(activity.Battle.Snapshot.Energy, Is.EqualTo(saved.Energy));
            Assert.That(activity.Battle.Snapshot.Hand.Select(card => card.Id),
                Is.EqualTo(saved.Hand.Select(card => card.Id)));
            _root.Presenter.Overlays.Resume();
            for (int tick = 0; tick < 1000 && !activity.IsRescueComplete &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; tick++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision) Commit(activity.Battle);
                activity.Battle.Advance(1000);
            }
            yield return null;
            Assert.That(_root.Presenter.Session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            yield return Load();
            Assert.That(_root.Presenter.Session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            MainApproachDriver.Click(_siege, "Retry");
            long settled = _root.Presenter.Session.Clock.Current.ElapsedMinutes;
            Assert.That(_root.Presenter.Session.Station.Animals.Count, Is.EqualTo(1));
            yield return Load();
            Assert.That(_root.Presenter.Session.Station.Animals.Single().AnimalId, Is.EqualTo(animalId));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(settled));
            Assert.That(_root.Presenter.Session.Activity, Is.Null);
            Assert.That(_root.Presenter.Session.Requests.Requests.Count(item => item.State == RequestState.Resolved),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NewEventAtSafeLocationSkipsOpeningAndKeepsSideCrates()
        {
            Choose(ApproachMode.Puzzle); yield return Load(); Depart();
            var oldAnimal = _root.Presenter.OutingAnimal.AnimalId;
            MainApproachDriver.Complete();
            MainApproachDriver.Click(_siege, "Retry");
            // A real external resolution can retire the abandoned event. No automatic event supply is added.
            var pool = _root.Presenter.Session.Requests;
            var first = pool.Requests.Single(item => item.TemplateId == "ordinary");
            Assert.That(pool.TryTransition(first.RequestId, RequestSignal.HelpedElsewhere, out _), Is.True);
            Assert.That(pool.TryAdd(new HelpRequest(Guid.NewGuid(), "ordinary", "parking", 1), out _), Is.True);
            Assert.That(_root.Presenter.Progress.TryCheckpoint(), Is.True);
            yield return Load(); Depart();
            Assert.That(_opening.isActiveAndEnabled, Is.False);
            Assert.That(_siege.isActiveAndEnabled, Is.True);
            Assert.That(_root.Presenter.OutingAnimal.AnimalId, Is.Not.EqualTo(oldAnimal));
            Assert.That(_root.Presenter.Session.Outing.Session.HasOpenedRoute, Is.False);
            var field = _siege.GetComponent<OutingFieldView>();
            Assert.That(field.Snapshot.Boxes, Does.Contain(47));
            Assert.That(field.Snapshot.Boxes, Does.Contain(46));
        }

        [UnityTest]
        public IEnumerator FailedFieldSaveStopsInputAndKeepsPriorDurableSnapshot()
        {
            Choose(ApproachMode.Puzzle); yield return Load(); Depart();
            var originals = _storage.Slots.ToArray();
            _storage.FailWrites = true;
            MainApproachDriver.Drag(_opening, 46, 47);
            Assert.That(_root.Presenter.Progress.CanContinue, Is.False);
            Assert.That(_root.Presenter.Session.Flow.IsSuspended, Is.True);
            Assert.That(_opening.gameObject.activeInHierarchy, Is.False);
            Assert.That(_storage.Slots, Is.EqualTo(originals));
            _storage.FailWrites = false; yield return Load();
            Assert.That(_opening.Session.HasBox(46), Is.True);
            Assert.That(_opening.Session.HasBox(47), Is.False);
        }

        [UnityTest]
        public IEnumerator FailedReturnSaveRecoversThePendingRescueAndSettlesItOnlyOnce()
        {
            Choose(ApproachMode.Puzzle); yield return Load(); Depart(); MainApproachDriver.Complete();
            var activity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            for (int tick = 0; tick < 1000 && !activity.IsRescueComplete &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; tick++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision) Commit(activity.Battle);
                activity.Battle.Advance(1000);
            }
            yield return null;
            Assert.That(_root.Presenter.Session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            var animalId = activity.Individual.Current.AnimalId;
            _root.Presenter.Overlays.RequestReturn();
            var originals = _storage.Slots.ToArray();
            _storage.FailWrites = true;
            _root.Presenter.Overlays.Confirm();
            Assert.That(_root.Presenter.Progress.CanContinue, Is.False);
            Assert.That(_root.Presenter.Session.Station.Animals.Count, Is.EqualTo(1));
            long expectedTime = _root.Presenter.Session.Clock.Current.ElapsedMinutes;
            Assert.That(_storage.Slots, Is.EqualTo(originals));
            _storage.FailWrites = false; yield return Load();
            Assert.That(_root.Presenter.Session.Station.Animals.Count, Is.Zero);
            Assert.That(_root.Presenter.Session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            MainApproachDriver.Click(_siege, "Retry");
            Assert.That(_root.Presenter.Session.Station.Animals.Single().AnimalId, Is.EqualTo(animalId));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(expectedTime));
            yield return Load();
            Assert.That(_root.Presenter.Session.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(expectedTime));
        }

        [UnityTest]
        public IEnumerator IncompatibleFieldContentKeepsBothOriginalSlotsAndRefusesWrites()
        {
            Choose(ApproachMode.Puzzle); yield return Load(); Depart();
            MainApproachDriver.Drag(_opening, 46, 47);
            _root.Presenter.FlushFieldProgress();
            var originals = _storage.Slots.ToArray();
            int writes = _storage.Writes;
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var references = new SerializedObject(content).FindProperty("_fieldConfigs");
            var sources = Enumerable.Range(0, references.arraySize).Select(index =>
                (TextAsset)references.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
            int changedIndex = Array.FindIndex(sources, item => item.text.Contains("parking.pockets"));
            var replacement = new TextAsset(sources[changedIndex].text.Replace(
                "field.parking.pockets", "field.parking.pockets.changed"));
            sources[changedIndex] = replacement;
            var modified = new OutingFieldCatalog(sources,
                content.CreateMapConfig().Definition.Points.Select(item => item.PointId));
            using (var blocked = new MainProgressSession(content.CreateMapConfig(), content.Artwork, _storage,
                content.CreateFieldProgressContent(modified)))
            {
                Assert.That(blocked.CanContinue, Is.False);
                Assert.That(blocked.TryCheckpoint(), Is.False);
                Assert.That(_storage.Slots, Is.EqualTo(originals));
                Assert.That(_storage.Writes, Is.EqualTo(writes));
            }
            Object.Destroy(replacement);
            yield return Load();
            Assert.That(_opening.Session.HasBox(47), Is.True);
        }

        private static void Commit(SiegeSession battle)
        {
            var state = battle.Snapshot;
            var card = state.Hand.FirstOrDefault(item => battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
            Assert.That(battle.TryOrder(new SiegeOrder
            {
                SessionId = state.SessionId, Turn = state.Turn, DeployCardId = card?.Id,
                DeployLaneId = card == null ? null : new[] { "top", "mid", "bottom" }[state.Turn % 3]
            }, true, out var reason, out _), Is.True, reason);
        }
    }
}
#endif
