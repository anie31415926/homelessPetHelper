using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class ApproachPlayTests
    {
        private MainSceneStorageScope _scope;
        private MemorySaveStorage _storage;
        private MainGameRoot _root;
        private GameContent _fixtureContent;
        private ApproachPanelView _view;
        private StationPanelView _station;
        private CityMapView _map;
        private int _seed;

        [SetUp]
        public void SetUp()
        {
            _storage = new MemorySaveStorage();
            _scope = new MainSceneStorageScope(() => _storage);
            SceneManager.sceneLoaded += Configure;
        }

        [TearDown]
        public void TearDown()
        {
            SceneManager.sceneLoaded -= Configure;
            _scope.Dispose();
            if (_fixtureContent != null) UnityEngine.Object.DestroyImmediate(_fixtureContent);
        }

        private void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main") return;
            var root = scene.GetRootGameObjects().SelectMany(item =>
                item.GetComponentsInChildren<MainGameRoot>(true)).Single();
            root.ConfigureOutingSeedSource(() => _seed);
            // This fixture deliberately covers the retained legacy opening and original yard tutorial.
            // Formal samples are exercised separately through the actual new composition.
            var binding = new UnityEditor.SerializedObject(root).FindProperty("_content");
            if (_fixtureContent != null) UnityEngine.Object.DestroyImmediate(_fixtureContent);
            var content = UnityEngine.Object.Instantiate((GameContent)binding.objectReferenceValue);
            _fixtureContent = content;
            var data = new UnityEditor.SerializedObject(content);
            var fields = data.FindProperty("_fieldConfigs"); fields.arraySize = 1;
            fields.GetArrayElementAtIndex(0).objectReferenceValue =
                UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Settings/Game/YardFieldConfig.json");
            data.ApplyModifiedPropertiesWithoutUndo();
            binding.objectReferenceValue = content; binding.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private IEnumerator Load(int seed)
        {
            _seed = seed;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            _root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            _view = UnityEngine.Object.FindObjectOfType<ApproachPanelView>(true);
            _station = UnityEngine.Object.FindObjectOfType<StationPanelView>(true);
            _map = UnityEngine.Object.FindObjectOfType<CityMapView>(true);
        }

        private void Depart(bool dog = false)
        {
            MainApproachDriver.Click(_station, "Restart");
            if (dog) MainApproachDriver.Click(_map, "Yard");
            MainApproachDriver.Click(_map, "Primary");
            Assert.That(_view.isActiveAndEnabled, Is.True);
            Assert.That(_root.Presenter.Session.Activity, Is.Null);
            Assert.That(_root.Presenter.Session.Flow.Stage, Is.EqualTo(RescueStage.Approach));
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator PuzzleControlsRequireRealRouteThenEnterCatSiegeAndReturnWithoutRescue()
        {
            yield return Load(1);
            Depart();
            Assert.That(_view.Session.Template.Mode, Is.EqualTo(ApproachMode.Puzzle));
            var owned = _view.Session;
            MainApproachDriver.Button(_view, "Continue").onClick.Invoke();
            Assert.That(_root.Presenter.Session.Activity, Is.Null);
            MainApproachDriver.Drag(_view, 17, 24);
            MainApproachDriver.Click(_view, "Undo");
            Assert.That(owned.HasBox(17), Is.True);
            MainApproachDriver.Click(_view, "Pause");
            MainApproachDriver.Drag(_view, 17, 24);
            Assert.That(owned.HasBox(17), Is.True);
            Assert.That(MainApproachDriver.Button(_view, "Return").IsInteractable(), Is.False);
            MainApproachDriver.Click(_view, "Pause");
            yield return Capture("puzzle");
            MainApproachDriver.Solve(_view);
            yield return Capture("puzzle-opened");
            MainApproachDriver.Click(_view, "Continue");
            Assert.That(owned.IsClosed, Is.True);
            var activity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            Assert.That(activity.Battle.Snapshot.EncounterId, Is.EqualTo("cat"));
            var siege = UnityEngine.Object.FindObjectOfType<SiegeTrialView>();
            MainApproachDriver.Click(siege, "Retry");
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(30));
            Assert.That(_root.Presenter.Session.Station.Animals, Is.Empty);
            Assert.That(_root.Presenter.Session.OpeningRoute, Is.Null);
        }

        [UnityTest]
        public IEnumerator CleanupUsesCapturedPointerCorrectToolAndStopsAtBackgroundBeforeDogSiege()
        {
            yield return Load(0);
            Depart(true);
            Assert.That(_view.Session.Template.Mode, Is.EqualTo(ApproachMode.Cleanup));
            var route = _view.Session;
            MainApproachDriver.Click(_view, "Weeds");
            var blocked = MainApproachDriver.Pointer(_view, 36);
            _view.Board.OnPointerDown(blocked);
            _view.AdvanceContact(.05);
            _view.Board.OnPointerUp(blocked);
            Assert.That(route.RemainingWork(route.DebrisAt(36)), Is.EqualTo(1.1));
            var entry = MainApproachDriver.Pointer(_view, 43);
            _view.Board.OnPointerDown(entry);
            _view.AdvanceContact(.05);
            _view.Board.OnPointerUp(entry);
            Assert.That(route.RemainingWork(route.DebrisAt(43)), Is.EqualTo(.8), "Wrong tool has no effect.");
            MainApproachDriver.Click(_view, "Litter");
            _view.Board.OnPointerDown(entry);
            _view.Board.OnPointerDown(MainApproachDriver.Pointer(_view, 36, 8));
            _view.AdvanceContact(.05);
            Assert.That(route.RemainingWork(route.DebrisAt(43)), Is.EqualTo(.75).Within(1e-10));
            _view.SendMessage("OnApplicationFocus", false);
            _view.AdvanceContact(.05);
            _view.SendMessage("OnApplicationFocus", true);
            Assert.That(route.IsSuspended, Is.True);
            MainApproachDriver.Click(_view, "Pause");
            _view.AdvanceContact(.05);
            Assert.That(route.RemainingWork(route.DebrisAt(43)), Is.EqualTo(.75).Within(1e-10),
                "Resuming does not reuse contact captured before the interruption.");
            yield return Capture("cleanup-partial");
            MainApproachDriver.Solve(_view);
            Assert.That(Enumerable.Range(0, route.Template.Debris.Count)
                .Any(index => route.RemainingWork(index) > 0), Is.True);
            var feedback = _view.GetComponentsInChildren<Text>().Single(text =>
                text.name == (_view.FieldScene.Layout == null ? "Feedback" : "FieldNotice"));
            string opened = feedback.text;
            var cleared = MainApproachDriver.Pointer(_view, 9);
            _view.Board.OnPointerDown(cleared);
            _view.AdvanceContact(.05);
            _view.Board.OnPointerUp(cleared);
            Assert.That(feedback.text, Is.EqualTo(opened), "Completed route feedback stays visible.");
            yield return Capture("cleanup-opened");
            MainApproachDriver.Click(_view, "Continue");
            Assert.That(((SiegeRescueActivity)_root.Presenter.Session.Activity).Battle.Snapshot.EncounterId,
                Is.EqualTo("dog"));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CanceledAndEndedDragsReleaseCleaningContact()
        {
            yield return Load(0);
            Depart();
            var route = _view.Session;
            int debris = route.DebrisAt(45);
            double remaining = route.RemainingWork(debris);
            var pointer = MainApproachDriver.Pointer(_view, 45);
            _view.Board.OnPointerDown(pointer);
            _view.Board.OnCancel(pointer);
            _view.AdvanceContact(.05);
            Assert.That(_view.Board.IsHeld, Is.False);
            Assert.That(route.RemainingWork(debris), Is.EqualTo(remaining));
            _view.Board.OnPointerDown(pointer);
            _view.Board.OnEndDrag(pointer);
            _view.AdvanceContact(.05);
            Assert.That(_view.Board.IsHeld, Is.False);
            Assert.That(route.RemainingWork(debris), Is.EqualTo(remaining));
            _view.Board.OnPointerDown(pointer);
            _view.AdvanceContact(.05);
            _view.Board.OnPointerUp(pointer);
            Assert.That(route.RemainingWork(debris), Is.LessThan(remaining));
        }

        [UnityTest]
        public IEnumerator EarlyReturnSettlesOnceAndOldInputCannotChangeTheNextOuting()
        {
            yield return Load(0);
            Depart();
            var old = _view.Session;
            var pointer = MainApproachDriver.Pointer(_view, 45);
            _view.Board.OnPointerDown(pointer);
            _view.AdvanceContact(.05);
            var staleReturn = MainApproachDriver.Button(_view, "Return");
            MainApproachDriver.Click(_view, "Return");
            staleReturn.onClick.Invoke();
            Assert.That(old.IsClosed, Is.True);
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(15));
            Assert.That(_root.Presenter.Session.Station.Animals, Is.Empty);
            Depart();
            var next = _view.Session;
            Assert.That(next, Is.Not.SameAs(old));
            _view.Board.OnPointerUp(pointer);
            _view.AdvanceContact(.05);
            Assert.That(next.RemainingWork(next.DebrisAt(45)), Is.EqualTo(.24));
            Assert.That(_root.Presenter.Session.Requests.Requests
                .Single(request => request.TemplateId == "ordinary").State, Is.EqualTo(RequestState.Accepted));
        }

        [UnityTest]
        public IEnumerator ExitingDuringOpeningRestoresStationCheckpointWithoutOfflineTimeOrIntake()
        {
            yield return Load(0);
            Depart();
            var old = _view.Session;
            MainApproachDriver.Clean(_view, 45);
            yield return Load(1);
            Assert.That(old.IsClosed, Is.True);
            Assert.That(_view.gameObject.activeInHierarchy, Is.False);
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(_root.Presenter.Session.Station.Animals, Is.Empty);
            Assert.That(_root.Presenter.Session.Requests.Requests
                .All(request => request.State == RequestState.Available),
                Is.True);
            Depart();
            Assert.That(_view.Session.Template.Mode, Is.EqualTo(ApproachMode.Puzzle));
        }

        private IEnumerator Capture(string name)
        {
            var canvas = _view.GetComponent<Canvas>();
            var camera = new GameObject("ApproachCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("ApproachEvidence");
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(432, 960), new Vector2Int(390, 844),
                new Vector2Int(540, 1080), new Vector2Int(540, 960), new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Assert.That(image.GetPixels32().Count(pixel => pixel.r < 210 && pixel.g < 220),
                    Is.GreaterThan(size.x * size.y / 30));
                File.WriteAllBytes(Path.Combine(folder, name + "-" + size.x + "x" + size.y + ".png"),
                    image.EncodeToPNG());
                UnityEngine.Object.Destroy(image);
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            UnityEngine.Object.Destroy(camera.gameObject);
            yield return null;
        }
    }
}
