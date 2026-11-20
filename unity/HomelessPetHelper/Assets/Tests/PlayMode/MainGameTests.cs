using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
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

        [UnitySetUp]
        public IEnumerator Load()
        {
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
            Click(_map, "Primary");
            Assert.That(_siege.gameObject.activeInHierarchy, Is.True);
            Assert.That(_root.Presenter.Session.Activity, Is.TypeOf<SiegeRescueActivity>());
            return (SiegeRescueActivity)_root.Presenter.Session.Activity;
        }

        [UnityTest]
        public IEnumerator SavedMainControlsCompleteSiegeArrivalCareAndReentryWithOneIndividual()
        {
            yield return Capture(_station, "empty");
            Click(_station, "Restart");
            yield return Capture(_map, "map");
            Click(_map, "Primary");
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
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(25));
            staleReturn.onClick.Invoke();
            Assert.That(session.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(25));
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

        private static Button FindButton(Component view, string name) => view.GetComponentsInChildren<Button>(true)
            .Single(button => button.name == name);

        private static void Click(Component view, string name)
        {
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
