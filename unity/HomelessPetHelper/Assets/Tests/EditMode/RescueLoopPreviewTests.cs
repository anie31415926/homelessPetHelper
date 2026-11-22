using HomelessPetHelper.Game.CityMap;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Game.Station;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Bootstrap;
using HomelessPetHelper.Editor.CityMap;
using HomelessPetHelper.Editor.RescueCase;
using HomelessPetHelper.Editor.OptionPreview;
using HomelessPetHelper.Editor.StationPreview;
using HomelessPetHelper.Editor.StationPreview.Loop;
using HomelessPetHelper.Modules.Animals;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Rescue.UI;
using HomelessPetHelper.Modules.Station.Cleaning;
using HomelessPetHelper.Modules.Station.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.EditMode
{
    public sealed class RescueLoopPreviewTests
    {
        private RescueLoopPresenter _loop;
        private StationPanelView _station;
        private CityMapView _map;
        private RescueCaseView _rescue;

        [UnityTest]
        public IEnumerator IndependentCarePermissionsMatchEachButtonAndRefusalIsVisible()
        {
            yield return new EnterPlayMode();
            yield return Load();
            Click(_station, "Restart");
            Click(_map, "Primary");
            CompleteRescue();
            AnimalIndividual individual = _loop.Trial.Individual;
            Assert.That(individual.TryApplyCarePermissions(individual.Current,
                new AnimalCarePermissions(true, false, true)), Is.True);
            Click(_rescue, "Advance");
            Click(_station, "Enter");
            Click(_station, "Animal0");
            Assert.That(Button(_station, "Clean").IsInteractable(), Is.False, "Unchecked.");
            Assert.That(Button(_station, "Groom").IsInteractable(), Is.False, "Unchecked.");
            Click(_station, "Check");
            Assert.That(Button(_station, "Groom").IsInteractable(), Is.True);
            Assert.That(Button(_station, "Clean").IsInteractable(), Is.False);
            // Even a queued invocation of a now-disabled button must revalidate the current permissions.
            Button(_station, "Clean").onClick.Invoke();
            Assert.That(VisibleText(_station), Does.Contain("它暂时不接受清洗"));
            Click(_station, "Groom");
            Click(_station, "Finish");
            Assert.That(individual.TryApplyCarePermissions(individual.Current,
                new AnimalCarePermissions(true, true, false)), Is.True);
            Click(_station, "Pet");
            Assert.That(Button(_station, "Clean").IsInteractable(), Is.True);
            Assert.That(Button(_station, "Groom").IsInteractable(), Is.False);
            Click(_station, "Clean");
            Button(_station, "Groom").onClick.Invoke();
            Assert.That(VisibleText(_station), Does.Contain("它暂时不接受梳理"));
            Click(_station, "Finish");
        }

        [UnityTest]
        public IEnumerator StationClockUsesConfiguredDayBoundary()
        {
            yield return new EnterPlayMode();
            yield return Load();
            _loop.Dispose();
            var config = new CityMapPreviewConfig(AssetDatabase.LoadAssetAtPath<TextAsset>(CityMapAssets.ConfigPath));
            AssertClock(config, 30, 35, "第 2 天  00:05");
            AssertClock(config, 1, long.MaxValue, "第 9223372036854775808 天  00:00");
        }

        private void AssertClock(CityMapPreviewConfig config, int minutesPerDay, long elapsed, string expected)
        {
            var requests = config.CreateRequestPool();
            using (var shortDay = new GameLoopSession(2, minutesPerDay,
                config.CreateRules(new HomelessPetHelper.Modules.Outing.PointUnlockState(), requests),
                requests, config.TimePolicy))
            using (var presenter = new StationPreviewSession(_station,
                AssetDatabase.LoadAssetAtPath<TextAsset>(StationPreviewAssets.TextPath),
                HomelessPetHelper.Editor.RuntimePreviewContent.Load().Artwork,
                HomelessPetHelper.Editor.RuntimePreviewContent.Load().Font, shortDay))
            {
                Assert.That(shortDay.Clock.TryAdvance(shortDay.Clock.Current.Version, elapsed, out _, out _), Is.True);
                presenter.ShowSharedStation(false);
                Assert.That(shortDay.Clock.Current.DayIndex, Is.EqualTo(elapsed / minutesPerDay));
                Assert.That(shortDay.Clock.Current.MinuteOfDay, Is.EqualTo(elapsed % minutesPerDay));
                string clock = _station.GetComponentsInChildren<Text>().Single(text => text.name == "Clock").text;
                Assert.That(clock, Is.EqualTo(expected));
            }
        }

        [UnityTest]
        public IEnumerator StaleReturnShowsReasonAndRepeatedAttemptsKeepAnimalProgressAndTime()
        {
            yield return new EnterPlayMode();
            yield return Load();
            Click(_station, "Restart");
            Click(_map, "Primary");
            CompleteRescue();
            var trial = _loop.Trial;
            var animal = trial.Individual.Current;
            Assert.That(_loop.Session.Clock.TryAdvance(_loop.Session.Clock.Current.Version, 1, out _, out _), Is.True);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Click(_rescue, "Advance");
                Assert.That(_loop.Session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
                Assert.That(_loop.Session.Station.Animals, Is.Empty);
                Assert.That(_loop.Trial, Is.SameAs(trial));
                Assert.That(trial.Individual.Current, Is.SameAs(animal));
                Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(1));
                Assert.That(VisibleText(_rescue), Does.Contain("时间记录已变化"));
            }
            yield return CaptureSixRatios(_rescue, "return-refused");
        }

        [UnityTest]
        public IEnumerator TransientReturnRefusalCanRetryWithoutDroppingOrDuplicatingTheRescuedAnimal()
        {
            yield return new EnterPlayMode();
            yield return Load();
            Click(_station, "Restart");
            Click(_map, "Primary");
            CompleteRescue();
            var trial = _loop.Trial;
            Guid animalId = trial.Individual.Current.AnimalId;
            _loop.Session.Station.SetSuspended(true);
            Click(_rescue, "Advance");
            Assert.That(VisibleText(_rescue), Does.Contain("救助站当前无法接收操作"));
            Assert.That(_loop.Trial, Is.SameAs(trial));
            Assert.That(_loop.Session.Station.Animals, Is.Empty);
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.Zero);
            _loop.Session.Station.SetSuspended(false);
            Click(_rescue, "Advance");
            Assert.That(_loop.Session.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(_loop.Session.Station.Animals[0].AnimalId, Is.EqualTo(animalId));
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(25));
        }

        [UnityTest]
        public IEnumerator UnresolvedReturnAndStationCareRefusalsAreVisibleAndRemainRetryable()
        {
            yield return new EnterPlayMode();
            yield return Load();
            Click(_station, "Restart");
            Click(_map, "Primary");
            _loop.Session.Clock.SetSuspended(true);
            Click(_rescue, "Restart");
            Assert.That(VisibleText(_rescue), Does.Contain("本次时间结算未通过"));
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.Zero);
            Assert.That(_loop.Session.Activity, Is.Not.Null);
            _loop.Session.Clock.SetSuspended(false);
            Click(_rescue, "Restart");
            Assert.That(_station.gameObject.activeSelf, Is.True);
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(20));

            Click(_station, "Restart");
            Click(_map, "Primary");
            CompleteRescue();
            Click(_rescue, "Advance");
            Click(_station, "Enter");
            Click(_station, "Animal0");
            _loop.Session.Flow.SetSuspended(true);
            Click(_station, "Check");
            Assert.That(_loop.Session.Flow.Stage, Is.EqualTo(RescueStage.Care));
            Assert.That(VisibleText(_station), Does.Contain("当前已暂停"));
            _loop.Session.Flow.SetSuspended(false);
            Click(_station, "Check");
            Assert.That(_loop.Session.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Click(_station, "Back");
            _loop.Session.Station.SetSuspended(true);
            Click(_station, "Restart");
            Assert.That(_station.gameObject.activeSelf, Is.True);
            Assert.That(VisibleText(_station), Does.Contain("救助站当前无法接收操作"));
            _loop.Session.Station.SetSuspended(false);
            Click(_station, "Restart");
            Assert.That(_map.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void EveryLoopAndCareRefusalHasLocalizedFeedback()
        {
            var text = new OptionPreviewText(
                AssetDatabase.LoadAssetAtPath<TextAsset>(StationPreviewAssets.TextPath));
            foreach (OutingAssemblyFailure failure in Enum.GetValues(typeof(OutingAssemblyFailure)))
                if (failure != OutingAssemblyFailure.None)
                    Assert.That(text.Get("loop.failure." + failure), Is.Not.Empty);
            foreach (StationCoatCareFailure failure in Enum.GetValues(typeof(StationCoatCareFailure)))
                if (failure != StationCoatCareFailure.None)
                    Assert.That(text.Get("care.failure." + failure), Is.Not.Empty);
        }

        private static string VisibleText(Component view) =>
            string.Join("\n", view.GetComponentsInChildren<Text>().Select(text => text.text));

        [UnityTest]
        public IEnumerator SavedControlsJoinRealRescueCareAndNextOutingWithoutLosingIdentityOrPartialWork()
        {
            yield return new EnterPlayMode();
            yield return Load();
            Assert.That(_loop.Session.Station.Animals, Is.Empty);
            Click(_station, "Restart");
            Assert.That(_map.gameObject.activeSelf, Is.True);
            Assert.That(VisibleText(_map), Does.Contain("25"), "Route-free preview uses its actual time policy.");
            yield return CaptureSixRatios(_map, "map");
            Click(_map, "Primary");
            Assert.That(_rescue.gameObject.activeSelf, Is.True);
            yield return CaptureSixRatios(_rescue, "rescue");
            Guid animalId = _loop.Trial.Individual.Current.AnimalId;
            CompleteRescue();
            Assert.That(_loop.Session.Flow.Stage, Is.EqualTo(RescueStage.Rescued));
            Click(_rescue, "Advance");
            Assert.That(_station.gameObject.activeSelf, Is.True);
            yield return CaptureSixRatios(_station, "arrival");
            Assert.That(_loop.Session.Station.Animals.Count, Is.EqualTo(1));
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(25));
            AnimalSnapshot arrival = _loop.Session.LastArrival.Arrival.AnimalAtArrival;
            Assert.That(arrival.AnimalId, Is.EqualTo(animalId));
            Assert.That(arrival.Disposition.IsHungry, Is.False);
            Click(_station, "Enter");
            Assert.That(Button(_station, "Restart").interactable, Is.False);
            Click(_station, "Animal0");
            Click(_station, "Check");
            yield return CaptureSixRatios(_station, "care");
            Assert.That(_loop.Session.Flow.Stage, Is.EqualTo(RescueStage.Station));
            Click(_station, "Clean");
            Canvas.ForceUpdateCanvases();
            for (int y = 2; y < 8; y++)
                for (int x = 2; x < 6; x++) Stroke(new Vector2(x / 10f, y / 10f));
            _loop.Session.Station.TryGetCurrentAnimal(animalId, out var partial);
            Assert.That(partial.Coat.Dirtiness, Is.LessThan(arrival.Coat.Dirtiness));
            Assert.That(partial.Coat.LooseFur, Is.EqualTo(arrival.Coat.LooseFur));
            long before = _loop.Session.Clock.Current.ElapsedMinutes;
            Toggle pause = _station.GetComponentInChildren<Toggle>(true);
            pause.isOn = true;
            Stroke(new Vector2(0.5f, 0.5f));
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(before));
            pause.isOn = false;
            Click(_station, "Back");
            long after = _loop.Session.Clock.Current.ElapsedMinutes;
            Assert.That(after, Is.GreaterThan(before));
            Click(_station, "History");
            Assert.That(_loop.Session.LastArrival.Arrival.AnimalAtArrival, Is.SameAs(arrival));
            Click(_station, "Enter");
            Click(_station, "Restart");
            Click(_map, "Cancel");
            Assert.That(_station.gameObject.activeSelf, Is.True);
            _loop.Session.Station.TryGetCurrentAnimal(animalId, out var reopened);
            Assert.That(reopened, Is.SameAs(partial));
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(after));
            Click(_station, "Animal0");
            Click(_station, "Clean");
            Click(_station, "Finish");
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(after), "No duplicate work charge.");
            Click(_station, "Back");
            yield return CaptureSixRatios(_station, "station-resident");
        }

        [UnityTest]
        public IEnumerator UnresolvedExplorationAndReunionKeepHonestBoundariesAndListenersDoNotMultiply()
        {
            yield return new EnterPlayMode();
            yield return Load();
            for (int index = 0; index < 3; index++)
            {
                Click(_station, "Restart");
                _map.SendMessage("OnApplicationFocus", false);
                Assert.That(Button(_map, "Primary").IsInteractable(), Is.False);
                _map.SendMessage("OnApplicationFocus", true);
                Click(_map, "Porch");
                Assert.That(_map.GetComponentsInChildren<Text>().Single(text => text.name == "Departure").text,
                    Does.Contain("5").And.Not.Contain("25"), "Reunion preview only supports arrival and return.");
                Click(_map, "Primary");
                Assert.That(_loop.Session.Activity, Is.Null, "No fake owner handoff or kitten substitution.");
                Click(_map, "Primary");
                Assert.That(_station.gameObject.activeSelf, Is.True);
                Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo((index + 1) * 5));
            }
            Click(_station, "Restart");
            Click(_map, "Yard");
            Click(_map, "Primary");
            Assert.That(_loop.Session.Activity, Is.Null);
            Click(_map, "Primary");
            Click(_station, "Restart");
            Click(_map, "Parking");
            Click(_map, "Primary");
            Click(_rescue, "Assistance");
            Assert.That(_loop.Session.Station.Animals, Is.Empty);
            Click(_rescue, "Advance");
            Assert.That(_station.gameObject.activeSelf, Is.True);
            Assert.That(_loop.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(40));
            Assert.That(_loop.Session.Map.TrySelectPoint("parking", out _, out _), Is.True);
            Click(_station, "Restart");
            Click(_map, "Primary");
            Assert.That(_loop.Trial, Is.Not.Null);
            Click(_rescue, "Restart");
            Assert.That(_station.gameObject.activeSelf, Is.True);
            Assert.That(_loop.Session.Station.Animals, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MultipleResidentsShareSceneAndCapacityNeverForcesEviction()
        {
            yield return new EnterPlayMode();
            yield return Load();
            for (int index = 0; index < 2; index++)
            {
                if (index > 0)
                {
                    // Explicit test fixture arrival, not a production request scheduler.
                    Assert.That(_loop.Session.Requests.TryAdd(new HomelessPetHelper.Modules.Outing.HelpRequest(
                        Guid.NewGuid(), "ordinary", "parking", 1), out _), Is.True);
                }
                Click(_station, "Restart");
                Click(_map, "Primary");
                CompleteRescue();
                Click(_rescue, "Advance");
                Click(_station, "Enter");
                Click(_station, "Animal" + index);
                Click(_station, "Check");
                Click(_station, "Back");
            }
            Assert.That(_loop.Session.Station.IsFull, Is.True);
            Guid first = _loop.Session.Station.Animals[0].AnimalId;
            Click(_station, "Restart");
            Click(_map, "Yard");
            Assert.That(Button(_map, "Primary").interactable, Is.False);
            Button(_map, "Primary").onClick.Invoke();
            Assert.That(_loop.Session.Activity, Is.Null);
            Assert.That(_loop.Session.Outing.Session.HasDeparted, Is.False);
            Assert.That(_loop.Session.Station.Animals.Count, Is.EqualTo(2));
            Assert.That(_loop.Session.Station.Animals[0].AnimalId, Is.EqualTo(first));
            Click(_map, "Cancel");
            Assert.That(_station.Scene.Count, Is.EqualTo(2));
            Assert.That(_station.Scene.Cells.Take(2).Select(cell => cell.AnimalId),
                Is.EqualTo(_loop.Session.Station.Animals.Select(animal => animal.AnimalId)));
            // The inactive legacy roster remains compatible, but no longer takes room on the home screen.
            _station.EnsureRosterSize(7);
            Assert.That(_station.Animals.Length, Is.EqualTo(7));
            var roster = _station.GetComponentsInChildren<ScrollRect>(true)
                .Single(scroll => scroll.content != null &&
                    scroll.content.GetComponentInChildren<StationAnimalView>(true) != null);
            Assert.That(roster.content.childCount, Is.EqualTo(7));
            Assert.That(roster.content.GetComponentsInChildren<StationAnimalView>(true),
                Is.EquivalentTo(_station.Animals));
            Assert.That(roster.gameObject.activeInHierarchy, Is.False);
        }

        private IEnumerator Load()
        {
            AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode(RescueLoopAssets.ScenePath,
                new LoadSceneParameters());
            while (!loading.isDone) yield return null;
            yield return null;
            _loop = RescueLoopHost.Current;
            Assert.That(_loop, Is.Not.Null);
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            _station = roots.Select(root => root.GetComponent<StationPanelView>()).Single(item => item != null);
            _map = roots.Select(root => root.GetComponent<CityMapView>()).Single(item => item != null);
            _rescue = roots.Select(root => root.GetComponent<RescueCaseView>()).Single(item => item != null);
        }

        private void CompleteRescue()
        {
            for (int step = 0; step < 100 && _loop.Trial.Stage != CaseStage.Summary; step++)
            {
                RescueCaseTrial trial = _loop.Trial;
                if (trial.Stage != CaseStage.Choosing)
                {
                    Click(_rescue, "Advance");
                    continue;
                }
                string desired = !trial.Animal.HasSpace ? "give-space" : trial.Animal.IsHungry ?
                    trial.Animal.HasFoodOnGround ? "wait" : "food" : "wait";
                var option = trial.Options.FirstOrDefault(item => item.Definition.Id == desired) ??
                    trial.Options.First(item => trial.GetOptionFailure(item.InstanceId) == CaseFailure.None &&
                        item.Definition.Id != "approach");
                int index = trial.Options.ToList().IndexOf(option);
                OptionCardView card = _rescue.Options.GetComponentsInChildren<OptionCardView>()[index];
                card.GetComponentInChildren<HoldActionButton>().OnSubmit(new BaseEventData(EventSystem.current));
            }
            Assert.That(_loop.Trial.Stage, Is.EqualTo(CaseStage.Summary));
        }

        private void Stroke(Vector2 normalized)
        {
            RectTransform rect = (RectTransform)_station.Surface.transform;
            Vector3 local = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, normalized.x),
                Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, normalized.y));
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left, pointerId = 1,
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local))
            };
            _station.Surface.OnPointerDown(pointer);
            _station.Surface.OnPointerUp(pointer);
        }

        private static Button Button(Component view, string name)
        {
            if (view is StationPanelView station && station.Scene != null && name.StartsWith("Animal",
                StringComparison.Ordinal)) return station.Scene.Cells[int.Parse(name.Substring(6))].SelectButton;
            return view.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        }
        private static void Click(Component view, string name)
        {
            if (view is StationPanelView station && station.IsSceneLayout && name == "History" &&
                !Button(view, name).isActiveAndEnabled) Button(view, "StationMenu").onClick.Invoke();
            Button button = Button(view, name);
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        private static IEnumerator CaptureSixRatios(Component view, string name)
        {
            Canvas canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("LoopCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            string folder = Path.GetFullPath("../../rescue-loop-screenshots");
            Directory.CreateDirectory(folder);
            foreach (Vector2Int size in new[] { new Vector2Int(540, 960), new Vector2Int(390, 844),
                new Vector2Int(768, 1024), new Vector2Int(540, 1080), new Vector2Int(540, 1170),
                new Vector2Int(540, 1200) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                for (int frame = 0; frame < 5; frame++)
                {
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                }
                camera.Render();
                AssertLayout(view, canvas);
                RenderTexture previous = RenderTexture.active;
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
        }

        private static void AssertLayout(Component view, Canvas canvas)
        {
            Rect bounds = ((RectTransform)canvas.transform).rect;
            var corners = new Vector3[4];
            foreach (Selectable control in view.GetComponentsInChildren<Selectable>())
            {
                foreach (var scroll in control.GetComponentsInParent<ScrollRect>())
                {
                    ((RectTransform)control.transform).GetWorldCorners(corners);
                    float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
                    float top = scroll.viewport.InverseTransformPoint(corners[1]).y;
                    float delta = top > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - top :
                        bottom < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bottom : 0;
                    scroll.content.anchoredPosition += new Vector2(0, delta);
                    Canvas.ForceUpdateCanvases();
                }
                ((RectTransform)control.transform).GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = canvas.transform.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.xMin - 1, bounds.xMax + 1), control.name);
                    Assert.That(local.y, Is.InRange(bounds.yMin - 1, bounds.yMax + 1), control.name);
                }
            }
            foreach (Text label in view.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(label.text)) continue;
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                if (label.resizeTextForBestFit)
                {
                    settings.fontSize = label.cachedTextGenerator.fontSizeUsedForBestFit;
                    settings.resizeTextForBestFit = false;
                }
                float height = new TextGenerator().GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Assert.That(height, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 2), label.name);
            }
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
