#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.CityMap.Travel;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class MapTravelPlayTests
    {
        private GameObject _owner, _input;
        private MainSceneStorageScope _storage;
        [SetUp] public void Setup() => _storage = new MainSceneStorageScope();
        [TearDown] public void Cleanup()
        {
            if (_owner != null) UnityEngine.Object.DestroyImmediate(_owner);
            if (_input != null) UnityEngine.Object.DestroyImmediate(_input);
            _storage.Dispose();
        }

        [UnityTest]
        public IEnumerator BothDirectionsRunBeforeAndAfterOneCommitAndRestoreInput()
        {
            foreach (var direction in new[] { MapTravelDirection.Enter, MapTravelDirection.Exit })
            {
                var view = CreateView();
                int commits = 0, finished = 0;
                Assert.That(view.TryPlay("mine", direction, () => true,
                    () => { commits++; return true; }, success => { Assert.That(success); finished++; }), Is.True);
                Assert.That(view.IsPlaying, Is.True);
                Assert.That(_input.GetComponent<CanvasGroup>().interactable, Is.False);
                Assert.That(view.TryPlay("mine", direction, () => true, () => true, _ => { }), Is.False);
                for (int i = 0; i < 120 && view.IsPlaying; i++) yield return new WaitForSecondsRealtime(.01f);
                Assert.That(view.IsPlaying, Is.False);
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(finished, Is.EqualTo(1));
                Assert.That(_input.GetComponent<CanvasGroup>().interactable, Is.True);
                Assert.That(_owner.GetComponentsInChildren<CanvasGroup>().Single().alpha, Is.Zero);
                UnityEngine.Object.DestroyImmediate(_owner);
                UnityEngine.Object.DestroyImmediate(_input);
            }
        }

        [UnityTest]
        public IEnumerator LostSourceAndDisabledOwnerNeverCommit()
        {
            var view = CreateView();
            bool valid = true;
            int commits = 0, finishes = 0;
            view.TryPlay("mine", MapTravelDirection.Enter, () => valid,
                () => { commits++; return true; }, success => { Assert.That(success, Is.False); finishes++; });
            valid = false;
            yield return null;
            Assert.That(commits, Is.Zero);
            Assert.That(finishes, Is.EqualTo(1));
            view.TryPlay("mine", MapTravelDirection.Exit, () => true,
                () => { commits++; return true; }, _ => finishes++);
            _owner.SetActive(false);
            Assert.That(commits, Is.Zero);
            Assert.That(finishes, Is.EqualTo(2));
            Assert.That(_input.GetComponent<CanvasGroup>().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator RejectedCommitRestoresCoverAndCanRetry()
        {
            var view = CreateView();
            bool? result = null;
            view.TryPlay("mine", MapTravelDirection.Enter, () => true, () => false, success => result = success);
            for (int i = 0; i < 120 && view.IsPlaying; i++) yield return new WaitForSecondsRealtime(.01f);
            Assert.That(result, Is.False);
            Assert.That(view.IsPlaying, Is.False);
            Assert.That(_owner.GetComponentsInChildren<CanvasGroup>().Single().blocksRaycasts, Is.False);
            Assert.That(view.TryPlay("unconfigured", MapTravelDirection.Exit, () => true,
                () => true, success => result = success), Is.True);
            Assert.That(result, Is.True);
        }

        [UnityTest]
        public IEnumerator MainHasSavedTravelBindingAndDirectDepartureStillWorks()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
            yield return null;
            var root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            Assert.That(Get(root, "_travel"), Is.Not.Null);
            var station = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Modules.Station.UI.StationPanelView>();
            station.GetComponentsInChildren<Button>().Single(button => button.name == "Restart").onClick.Invoke();
            var map = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Modules.Outing.UI.CityMapView>();
            map.GetComponentsInChildren<Button>().Single(button => button.name == "Primary").onClick.Invoke();
            Assert.That(root.Presenter.Session.OpeningRoute, Is.Not.Null);
            Assert.That(map.gameObject.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ZoomResetsAndBackgroundDoesNotAdvanceOrCommit()
        {
            var view = CreateView();
            var surface = (RectTransform)_input.transform;
            surface.anchoredPosition = new Vector2(12, 34);
            var point = ((MapTravelPointBinding[])Get(view, "_points"))[0];
            Set(point, "_zoomSurface", surface);
            var sequence = (MapTravelSequence)Get(point, "_enter");
            var step = ((MapTravelStep[])Get(sequence, "_beforeSwitch"))[0];
            Set(step, "_kind", MapTravelStepKind.ZoomAndFade);
            Set(step, "_durationSeconds", .1f);
            Set(step, "_offset", new Vector2(100, 50));
            view.Validate(new[] { "mine" });
            int commits = 0;
            view.TryPlay("mine", MapTravelDirection.Enter, () => true,
                () => { commits++; return true; }, _ => { });
            _owner.SendMessage("OnApplicationPause", true);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(commits, Is.Zero);
            Assert.That(surface.anchoredPosition, Is.EqualTo(new Vector2(12, 34)));
            _owner.SendMessage("OnApplicationPause", false);
            for (int i = 0; i < 120 && view.IsPlaying; i++) yield return new WaitForSecondsRealtime(.01f);
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(surface.anchoredPosition, Is.EqualTo(new Vector2(12, 34)));
            Assert.That(surface.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator MainExitFreezesFieldUntilOneAuthoritativeReturn()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Main.unity");
            yield return null;
            var root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            var station = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Modules.Station.UI.StationPanelView>();
            station.GetComponentsInChildren<Button>().Single(button => button.name == "Restart").onClick.Invoke();
            var map = UnityEngine.Object.FindObjectOfType<HomelessPetHelper.Modules.Outing.UI.CityMapView>();
            map.GetComponentsInChildren<Button>().Single(button => button.name == "Primary").onClick.Invoke();
            var view = (MapTravelView)Get(root, "_travel");
            var point = new MapTravelPointBinding();
            Set(point, "_pointId", root.Presenter.Session.Outing.Session.SelectedPointId);
            var sequence = new MapTravelSequence();
            var step = new MapTravelStep(); Set(step, "_kind", MapTravelStepKind.Fade);
            Set(step, "_durationSeconds", .1f);
            Set(sequence, "_beforeSwitch", new[] { step });
            Set(point, "_exit", sequence);
            Set(view, "_points", new[] { point });
            view.Validate(new[] { point.PointId });
            var leave = typeof(MainLoopPresenter).GetMethod("ReturnFromApproach",
                BindingFlags.Instance | BindingFlags.NonPublic);
            leave.Invoke(root.Presenter, null);
            Assert.That(root.Presenter.Session.Flow.IsSuspended, Is.True);
            Assert.That(root.Presenter.Session.Outing.Session.IsSettled, Is.False);
            leave.Invoke(root.Presenter, null);
            for (int i = 0; i < 120 && view.IsPlaying; i++) yield return new WaitForSecondsRealtime(.01f);
            Assert.That(root.Presenter.Session.Outing.Session.IsSettled, Is.True);
            Assert.That(station.gameObject.activeSelf, Is.True);
            Assert.That(root.Presenter.Session.Flow.IsSuspended, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private MapTravelView CreateView()
        {
            _owner = new GameObject("Travel", typeof(RectTransform), typeof(MapTravelView));
            var cover = new GameObject("Cover", typeof(RectTransform), typeof(CanvasGroup));
            cover.transform.SetParent(_owner.transform);
            _input = new GameObject("Input", typeof(RectTransform), typeof(CanvasGroup));
            var view = _owner.GetComponent<MapTravelView>();
            Set(view, "_cover", cover.GetComponent<CanvasGroup>());
            Set(view, "_inputRoots", new[] { _input.GetComponent<CanvasGroup>() });
            var point = new MapTravelPointBinding();
            Set(point, "_pointId", "mine");
            foreach (string field in new[] { "_enter", "_exit" })
            {
                var sequence = new MapTravelSequence();
                var before = new MapTravelStep(); Set(before, "_kind", MapTravelStepKind.Fade);
                Set(before, "_durationSeconds", .03f);
                var after = new MapTravelStep(); Set(after, "_kind", MapTravelStepKind.Fade);
                Set(after, "_durationSeconds", .03f); Set(after, "_fromOpacity", 1f); Set(after, "_toOpacity", 0f);
                Set(sequence, "_beforeSwitch", new[] { before }); Set(sequence, "_afterSwitch", new[] { after });
                Set(point, field, sequence);
            }
            Set(view, "_points", new[] { point });
            view.Validate(new[] { "mine" });
            return view;
        }

        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static object Get(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
#endif
