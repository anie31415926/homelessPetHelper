using HomelessPetHelper.Tests.Support;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class MainGameTests
    {
        private MainGameRoot _root;
        private StationPanelView _station;
        private CityMapView _map;
        private SiegeTrialView _siege;

        private MainSceneStorageScope _storageScope;
        [TearDown] public void ReleaseStorageScope() => _storageScope?.Dispose();

        [UnitySetUp]
        public IEnumerator Load()
        {
            _storageScope = new MainSceneStorageScope();
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            _root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            _station = UnityEngine.Object.FindObjectOfType<StationPanelView>(true);
            _map = UnityEngine.Object.FindObjectOfType<CityMapView>(true);
            _siege = UnityEngine.Object.FindObjectOfType<SiegeTrialView>(true);
            Assert.That(_root.Presenter, Is.Not.Null);
        }

        private SiegeRescueActivity Depart()
        {
            Click(_station, "Restart");
            Click(_map, "Primary"); MainApproachDriver.Complete();
            Assert.That(_siege.gameObject.activeInHierarchy, Is.True);
            Assert.That(_root.Presenter.Session.Activity, Is.TypeOf<SiegeRescueActivity>());
            return (SiegeRescueActivity)_root.Presenter.Session.Activity;
        }

        [UnityTest]
        public IEnumerator MapPreviewsSupportedDepartureAndRejectsUnavailableOrChangedRequestsWithoutSideEffects()
        {
            Click(_station, "Restart");
            var session = _root.Presenter.Session;
            var outing = session.Outing.Session;
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            var copy = new OptionPreviewText(content.MapText);
            Assert.That(MapLabel("Departure"), Is.EqualTo(copy.Get("departure.rescue", 35,
                copy.Get("space.required"))));
            Assert.That(session.Requests.Requests.All(request => request.State == RequestState.Available), Is.True);
            Assert.That(outing.HasDeparted, Is.False);
            Click(_map, "Porch");
            Button primary = FindButton(_map, "Primary");
            Assert.That(primary.IsInteractable(), Is.False);
            Assert.That(MapLabel("Feedback"), Is.EqualTo(copy.Get("unavailable.reunion")));
            primary.onClick.Invoke();
            Assert.That(outing.Stage, Is.EqualTo(OutingStage.MapSelecting));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(session.Requests.Requests.All(request => request.State == RequestState.Available), Is.True);
            Click(_map, "Parking");
            Assert.That(primary.IsInteractable(), Is.True);
            var request = session.Requests.Requests.Single(item => item.PointId == "parking");
            Assert.That(session.Requests.TryTransition(request.RequestId, RequestSignal.Accept, out _), Is.True);
            primary.onClick.Invoke();
            Assert.That(primary.IsInteractable(), Is.False, "Command must recheck a stale enabled button.");
            Assert.That(MapLabel("Feedback"), Is.EqualTo(copy.Get("request.unavailable")));
            Assert.That(outing.HasDeparted, Is.False);
            Assert.That(session.Activity, Is.Null);
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.Zero);
            Click(_map, "Cancel");
            primary.onClick.Invoke();
            Assert.That(_station.gameObject.activeInHierarchy, Is.True, "Hidden map input cannot reopen an outing.");
            Assert.That(outing.IsSettled, Is.True);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        private string MapLabel(string name) => _map.GetComponentsInChildren<Text>(true)
            .Single(label => label.name == name).text;

        [UnityTest]
        public IEnumerator SavedMainControlsCompleteSiegeArrivalCareAndReentryWithOneIndividual()
        {
            yield return Capture(_station, "empty");
            Click(_station, "Restart");
            yield return Capture(_map, "map");
            Click(_map, "Primary"); MainApproachDriver.Complete();
            var activity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            yield return null;
            yield return null;
            yield return Capture(_siege, "siege");
            AssertAnimatedAnimalVisible();
            yield return Win(activity);
            var session = _root.Presenter.Session;
            Assert.That(session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            Assert.That(activity.IsRescueComplete, Is.True);
            yield return Capture(_siege, "won");
            Button staleReturn = FindButton(_siege, "Retry");
            Click(_siege, "Retry");
            Assert.That(session.Station.Animals.Count, Is.EqualTo(1));
            var arrival = session.Station.Animals[0];
            Assert.That(arrival.MatchesCurrentIndividual(activity.Individual), Is.True);
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(35));
            staleReturn.onClick.Invoke();
            Assert.That(session.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(35));
            yield return Capture(_station, "summary");
            Click(_station, "Enter");
            Click(_station, "Animal0");
            Assert.That(FindButton(_station, "Clean").interactable, Is.False);
            Click(_station, "Check");
            yield return Capture(_station, "checked");
            Click(_station, "Clean");
            Canvas.ForceUpdateCanvases();
            for (int y = 2; y < 8; y++)
                for (int x = 2; x < 6; x++) Stroke(new Vector2(x / 10f, y / 10f));
            Assert.That(activity.Individual.Current.Coat.Dirtiness, Is.LessThan(65));
            yield return Capture(_station, "cleaning");
            Click(_station, "Finish");
            Click(_station, "Back");
            var partial = activity.Individual.Current;
            long minutes = session.Clock.Current.ElapsedMinutes;
            Click(_station, "Restart");
            Click(_map, "Cancel");
            Assert.That(session.Station.TryGetCurrentAnimal(arrival.AnimalId, out var current), Is.True);
            Assert.That(current, Is.SameAs(partial));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TimeoutAndVoluntaryReturnDoNotCreateAnAnimalAndRequestCanBeRetried()
        {
            var activity = Depart();
            for (int i = 0; i < 500 && activity.Battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision) Click(_siege, "Submit");
                activity.Battle.Advance(1000);
                yield return null;
            }
            yield return null;
            Assert.That(activity.Battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Timeout));
            Click(_siege, "Retry");
            Assert.That(_root.Presenter.Session.Station.Animals, Is.Empty);
            Assert.That(activity.IsClosed, Is.True);
            activity = Depart();
            Click(_siege, "Retry");
            Assert.That(_root.Presenter.Session.Station.Animals, Is.Empty);
            Assert.That(activity.IsClosed, Is.True);
            Assert.That(_station.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator FocusPauseAndRefusedTransportPreserveBattleAndAllowRetryIncludingAfterWin()
        {
            var activity = Depart();
            _siege.SendMessage("OnApplicationFocus", false);
            int elapsed = activity.Battle.ElapsedMs;
            activity.Battle.Advance(1000);
            Assert.That(activity.Battle.ElapsedMs, Is.EqualTo(elapsed));
            _siege.SendMessage("OnApplicationPause", true);
            _siege.SendMessage("OnApplicationFocus", true);
            Click(_siege, "Pause");
            Assert.That(activity.IsSuspended, Is.True, "Focus recovery cannot override application pause.");
            _siege.SendMessage("OnApplicationPause", false);
            Click(_siege, "Pause");
            Assert.That(activity.IsSuspended, Is.False);
            yield return Win(activity);
            _siege.SendMessage("OnApplicationFocus", false);
            Assert.That(_root.Presenter.Session.Flow.IsSuspended, Is.True);
            _siege.SendMessage("OnApplicationFocus", true);
            Click(_siege, "Pause");
            Assert.That(_root.Presenter.Session.Flow.IsSuspended, Is.False);
            var individual = activity.Individual.Current;
            _root.Presenter.Session.Station.SetSuspended(true);
            Click(_siege, "Retry");
            Assert.That(_root.Presenter.Session.Activity, Is.SameAs(activity));
            Assert.That(activity.Individual.Current, Is.SameAs(individual));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.Zero);
            _root.Presenter.Session.Station.SetSuspended(false);
            Click(_siege, "Retry");
            Assert.That(_root.Presenter.Session.Station.Animals.Count, Is.EqualTo(1));
        }

        private IEnumerator Win(SiegeRescueActivity activity)
        {
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won; i++)
            {
                if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var cell = _siege.GetComponentsInChildren<SiegeChoiceCell>().FirstOrDefault();
                    if (cell != null) cell.GetComponent<Button>().onClick.Invoke();
                    Click(_siege, activity.Battle.Snapshot.Turn % 3 == 0 ? "Top" :
                        activity.Battle.Snapshot.Turn % 3 == 1 ? "Mid" : "Bottom");
                    Click(_siege, "Submit");
                }
                activity.Battle.Advance(1000);
                yield return null;
            }
            yield return null;
            Assert.That(activity.Battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won));
        }

        private void Stroke(Vector2 normalized)
        {
            var rect = (RectTransform)_station.Surface.transform;
            Vector3 local = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, normalized.x),
                Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, normalized.y));
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local)) };
            _station.Surface.OnPointerDown(pointer);
            _station.Surface.OnPointerUp(pointer);
        }

        private void AssertAnimatedAnimalVisible()
        {
            var raw = _siege.GetComponentsInChildren<RawImage>().Single(image => image.name == "AnimatedAnimal");
            var target = (RenderTexture)raw.texture;
            Assert.That(target, Is.Not.Null);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            Assert.That(pixels.GetPixels32().Count(pixel => pixel.a > 150 && pixel.r > pixel.b + 20),
                Is.GreaterThan(100), "The animated animal must actually render, not just hold a texture.");
            UnityEngine.Object.Destroy(pixels);
        }

        private static Button FindButton(Component view, string name)
        {
            if (view is StationPanelView station && station.Scene != null && name.StartsWith("Animal",
                StringComparison.Ordinal)) return station.Scene.Cells[int.Parse(name.Substring(6))].SelectButton;
            return MainApproachDriver.Button(view, name);
        }

        private static void Click(Component view, string name)
        {
            if (view is SiegeTrialView sceneView && sceneView.Scene != null &&
                (name == "Retry" || name == "Pause"))
            { MainApproachDriver.Click(view, name); return; }
            if (name == "Retry" && view is SiegeTrialView siege && siege.Scene != null &&
                !FindButton(view, name).isActiveAndEnabled) MainApproachDriver.Click(view, "Pause");
            var button = FindButton(view, name);
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        private static IEnumerator Capture(Component view, string name)
        {
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("MainCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("MainEvidence");
            Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(540, 960),
                new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                if (view is CityMapView)
                    foreach (Text label in view.GetComponentsInChildren<Text>())
                        if (!string.IsNullOrWhiteSpace(label.text))
                        {
                            LevelUiPlayTests.ScrollIntoView(label);
                            camera.Render();
                            Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(4),
                                "Visible map text: " + label.name + " at " + size);
                        }
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                Assert.That(image.GetPixels32().Count(pixel => pixel.r < 180 && pixel.g < 180),
                    Is.GreaterThan(size.x * size.y / 1000));
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
