using System;
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Animals.UI;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Siege;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class MainContinuityPlayTests
    {
        private MemorySaveStorage _storage;
        private MainSceneStorageScope _scope;
        private MainGameRoot _root;
        private StationPanelView _station;
        private CityMapView _map;
        private SiegeTrialView _siege;

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

        private static void Configure(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "Main") scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainGameRoot>(true))
                .Single().ConfigureOutingSeedSource(() => 20261122);
        }

        private IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            _root = UnityEngine.Object.FindObjectOfType<MainGameRoot>();
            _station = UnityEngine.Object.FindObjectOfType<StationPanelView>(true);
            _map = UnityEngine.Object.FindObjectOfType<CityMapView>(true);
            _siege = UnityEngine.Object.FindObjectOfType<SiegeTrialView>(true);
        }

        [UnityTest]
        public IEnumerator NewGameOpeningPointsAtExistingCatRequestAndDoesNotRepeatAfterReload()
        {
            UseOpening();
            yield return Load();
            PresentOpeningOrNotice();
            Assert.That(FeedbackContains("救助站开门啦"), Is.True);
            Assert.That(FeedbackContains("水晶矿洞"), Is.True);
            var request = _root.Presenter.Session.Requests.Requests.Single(item => item.PointId == "parking");
            Click(_station, "OverlayConfirm");
            Assert.That(_map.gameObject.activeInHierarchy, Is.True);
            Assert.That(request.State, Is.EqualTo(RequestState.Available));
            Click(_map, "Primary");
            yield return WaitForPanel<Game.Approach.ApproachPanelView>();
            Assert.That(_root.Presenter.Session.Outing.Session.AcceptedRequestId, Is.EqualTo(request.RequestId));

            yield return Load();
            Assert.That(_root.Presenter.Progress.IsNewGame, Is.False);
            Assert.That(_root.Presenter.Session.Outing.Session.AcceptedRequestId, Is.EqualTo(request.RequestId));
        }

        [UnityTest]
        public IEnumerator OpeningCanBeDismissedAndLeavesTheRequestAvailable()
        {
            UseOpening();
            yield return Load();
            PresentOpeningOrNotice();
            Assert.That(FeedbackContains("救助站开门啦"), Is.True);
            yield return new UiLayoutCompliancePlayTests().CaptureAndAssert(_station, "station-opening");
            _root.Presenter.Overlays.CancelConfirmation();

            yield return Load();
            PresentOpeningOrNotice();
            Assert.That(_root.Presenter.Progress.IsNewGame, Is.False);
            Assert.That(FeedbackContains("救助站开门啦"), Is.False);
            Assert.That(_root.Presenter.Session.Requests.Requests.Single(item => item.PointId == "parking").State,
                Is.EqualTo(RequestState.Available));
        }

        private void UseOpening()
        {
            _scope.Dispose();
            _scope = new MainSceneStorageScope(() => _storage, showOpening: true);
        }

        private void PresentOpeningOrNotice()
        {
            var present = typeof(MainLoopPresenter).GetMethod("PresentPendingRescueNotice",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(present, Is.Not.Null);
            present.Invoke(_root.Presenter, null);
        }

        [UnityTest]
        public IEnumerator RescueNoticeOpensTheSameMapRequestAndAcceptedOutingSurvivesReload()
        {
            yield return Load();
            var presenter = _root.Presenter;
            var session = presenter.Session;
            var old = session.Requests.Requests.Single(request => request.PointId == "yard");
            Assert.That(session.Requests.TryTransition(old.RequestId, RequestSignal.HelpedElsewhere, out _), Is.True);
            Assert.That(session.Clock.TryAdvance(session.Clock.Current.Version, 180, out _, out _), Is.True);
            Assert.That(presenter.Progress.TryCheckpoint(), Is.True, presenter.Progress.PersistenceError?.ToString());
            var notice = session.Requests.Requests.Last();
            PresentOpeningOrNotice();
            Assert.That(FeedbackContains("救助消息"), Is.True);
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            Assert.That(FeedbackContains(new OptionPreviewText(content.MapText).Get("yard")), Is.True);
            yield return new UiLayoutCompliancePlayTests().CaptureAndAssert(_station, "rescue-notice");
            Click(_station, "OverlayConfirm");
            Assert.That(_map.gameObject.activeInHierarchy, Is.True);
            Assert.That(session.Outing.Session.HasDeparted, Is.False);
            Assert.That(notice.State, Is.EqualTo(RequestState.Available));
            Assert.That(session.Station.AvailableTemporaryPlaces, Is.EqualTo(2));
            Click(_map, "Primary");
            yield return WaitForPanel<Game.Approach.ApproachPanelView>();
            Guid animalId = presenter.OutingAnimal.AnimalId;
            Assert.That(session.Outing.Session.AcceptedRequestId, Is.EqualTo(notice.RequestId));
            Assert.That(notice.State, Is.EqualTo(RequestState.Accepted));

            yield return Load();
            Assert.That(_root.Presenter.Progress.CanContinue, Is.True);
            Assert.That(_root.Presenter.Session.Outing.Session.AcceptedRequestId, Is.EqualTo(notice.RequestId));
            Assert.That(_root.Presenter.OutingAnimal.AnimalId, Is.EqualTo(animalId));
        }

        [UnityTest]
        public IEnumerator RealControlsRescueCatThenDogAndReloadTheirIndividualCareAndTime()
        {
            yield return Load();
            Assert.That(_storage.Writes, Is.Zero);
            Click(_station, "Restart");
            Click(_map, "Primary");
            var encountered = _root.Presenter.OutingAnimal;
            MainApproachDriver.Complete();
            var animated = _siege.Scene != null
                ? _siege.GetComponent<Game.Field.OutingFieldView>().Animal.GetComponent<AnimalCoatView>()
                : _siege.GetComponentsInChildren<RawImage>().Single(image => image.name == "AnimatedAnimal")
                    .GetComponent<AnimalCoatView>();
            Assert.That(animated.Snapshot, Is.SameAs(encountered));
            var catActivity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            Assert.That(catActivity.Battle.Snapshot.EncounterId, Is.EqualTo("cat"));
            yield return Win();
            Click(_siege, "Retry");
            Assert.That(FeedbackContains("先让它歇一会儿"), Is.True);
            var layout = new UiLayoutCompliancePlayTests();
            yield return layout.CaptureAndAssert(_station, "arrival-summary");
            var cat = _root.Presenter.Session.Station.Animals.Single().AnimalId;
            Click(_station, "Enter");
            Click(_station, "Animal0");
            yield return layout.CaptureAndAssert(_station, "animal-detail");
            Click(_station, "Check");
            yield return layout.CaptureAndAssert(_station, "animal-assessed");
            Click(_station, "Clean");
            yield return layout.CaptureAndAssert(_station, "animal-care");
            Canvas.ForceUpdateCanvases();
            Stroke();
            Click(_station, "Finish");
            Click(_station, "Back");
            var catCoat = _root.Presenter.Progress.Care.Get(cat).Capture();
            Assert.That(catCoat.CopyDirt().RemovedFraction, Is.GreaterThan(0));
            Click(_station, "Restart");
            var content = Resources.FindObjectsOfTypeAll<GameContent>().Single(item => item.name == "GameContent");
            var mapCopy = new OptionPreviewText(content.MapText);
            Assert.That(_map.GetComponentsInChildren<Text>(true).Single(text => text.name == "Heading").text,
                Is.EqualTo(mapCopy.Get("yard")), "Reopening after cat rescue recommends the remaining dog request.");
            Assert.That(Button(_map, "Primary").IsInteractable(), Is.True);
            Click(_map, "Parking");
            string detail = _map.GetComponentsInChildren<Text>(true).Single(text => text.name == "Details").text;
            Assert.That(detail, Is.EqualTo(mapCopy.Get("detail.noRequest", mapCopy.Get("parking.detail"))));
            Assert.That(detail, Does.Not.Contain(mapCopy.Get("parking.rescue")));
            Assert.That(Button(_map, "Parking").GetComponentsInChildren<Text>(true)
                .Single(text => text.name == "State").text, Is.EqualTo(mapCopy.Get("point.NoRequest")));
            Assert.That(Button(_map, "Primary").IsInteractable(), Is.False);
            Assert.That(_map.GetComponentsInChildren<Text>(true).Single(text => text.name == "Feedback").text,
                Is.EqualTo(mapCopy.Get("unavailable.exploration")));
            Button(_map, "Primary").onClick.Invoke();
            Assert.That(_root.Presenter.Session.Outing.Session.HasDeparted, Is.False);
            Click(_map, "Yard");
            Click(_map, "Primary"); MainApproachDriver.Complete();
            var dogActivity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            Assert.That(dogActivity.Battle.Snapshot.EncounterId, Is.EqualTo("dog"));
            Assert.That(dogActivity.Individual.Current.DefinitionId, Is.EqualTo("dog"));
            yield return Win();
            Click(_siege, "Retry");
            var dog = _root.Presenter.Session.Station.Animals[1].AnimalId;
            Click(_station, "Enter");
            Click(_station, "Animal1");
            Click(_station, "Check");
            Click(_station, "Back");
            Assert.That(cat, Is.Not.EqualTo(dog));
            Assert.That(_station.Scene.Count, Is.EqualTo(2));
            long minutes = _root.Presenter.Session.Clock.Current.ElapsedMinutes;
            Guid oldStation = _root.Presenter.Session.StationSessionId;
            yield return Load();
            var session = _root.Presenter.Session;
            Assert.That(_root.Presenter.Progress.CanContinue, Is.True);
            Assert.That(session.StationSessionId, Is.Not.EqualTo(oldStation));
            Assert.That(session.Station.Animals.Select(item => item.AnimalId), Is.EqualTo(new[] { cat, dog }));
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            Assert.That(_root.Presenter.Progress.Care.Get(cat).Capture().CopyDirt().CopySamples(),
                Is.EqualTo(catCoat.CopyDirt().CopySamples()));
            Assert.That(session.Station.Animals[0].AnimalAtArrival.Coat.Dirtiness, Is.EqualTo(65));
            Assert.That(session.Requests.Requests.Count(item => item.State == RequestState.Resolved), Is.EqualTo(2));
            Click(_station, "Animal0");
            Click(_station, "Clean");
            Click(_station, "Finish");
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            Click(_station, "Back");
            yield return CaptureStation();
            Click(_station, "Animal1");
            Assert.That(_station.GetComponentsInChildren<Text>(true).Any(text => text.text == "小麦"), Is.True);
            Click(_station, "Back");
            Click(_station, "Restart");
            Assert.That(_map.GetComponentsInChildren<Text>(true).Single(text => text.name == "Heading").text,
                Is.EqualTo(mapCopy.Get("parking")), "With no supported request, keep an open place for inspection.");
            Assert.That(Button(_map, "Primary").IsInteractable(), Is.False);
            Assert.That(session.Outing.Session.HasDeparted, Is.False);
            Assert.That(session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            Assert.That(session.Station.Animals.Count, Is.EqualTo(2));
            Click(_map, "Cancel");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AdoptionPublishesChoosesAndArchivesWithoutReleasingOnCancelOrSaveFailure()
        {
            yield return Load();
            Click(_station, "StationMenu"); Click(_station, "StationMemorial");
            Assert.That(FeedbackContains("送养完成后"), Is.True);
            Click(_station, "OverlayCancel");
            Click(_station, "Restart"); Click(_map, "Parking"); Click(_map, "Primary");
            yield return WaitForPanel<Game.Approach.ApproachPanelView>();
            MainApproachDriver.Complete();
            yield return Win();
            Click(_siege, "Retry");
            yield return WaitForPanel<StationPanelView>();
            var animal = _root.Presenter.Session.Station.Animals.Single();
            var layout = new UiLayoutCompliancePlayTests();
            Assert.That(FeedbackContains("栗子"), Is.True);
            Click(_station, "SummaryRename");
            var nameField = _station.GetComponentInChildren<InputField>();
            yield return null;
            Assert.That(nameField.isFocused, Is.True, "Rename should focus its visible input.");
            Assert.That(nameField.text, Is.EqualTo("栗子"));
            nameField.text = "<abc>";
            Click(_station, "OverlayConfirm");
            Assert.That(FeedbackContains("请输入"), Is.True);
            Assert.That(_root.Presenter.Progress.GetCustomName(animal.AnimalId), Is.Null);
            nameField.text = "雪团芝麻麦芽芋圆";
            yield return layout.CaptureAndAssert(_station, "name-summary-dialog");
            Click(_station, "OverlayConfirm");
            Assert.That(FeedbackContains("雪团芝麻麦芽芋圆"), Is.True);
            Click(_station, "Enter"); Click(_station, "Animal0");
            var portrait = _station.Surface.GetComponentsInChildren<Image>().Single(image =>
                image.sprite != null && image.sprite.name == "T_Pet_original");
            Assert.That(portrait.isActiveAndEnabled, Is.True, "The selected rescue belongs inside its detail popup.");
            Assert.That(portrait.raycastTarget, Is.False, "Viewing the animal is not a care stroke.");
            yield return layout.CaptureAndAssert(_station, "name-detail-long");
            Click(_station, "DetailRename");
            _station.GetComponentInChildren<InputField>().text = "柚子";
            Click(_station, "OverlayCancel");
            Assert.That(_root.Presenter.Progress.GetCustomName(animal.AnimalId), Is.EqualTo("雪团芝麻麦芽芋圆"));
            Click(_station, "DetailRename");
            _station.GetComponentInChildren<InputField>().text = "柚子";
            Click(_station, "OverlayConfirm");
            Assert.That(FeedbackContains("柚子"), Is.True);
            Click(_station, "QuietSpace");
            Assert.That(FeedbackContains("基础检查"), Is.True);
            Click(_station, "OverlayCancel"); Click(_station, "Check");
            yield return layout.CaptureAndAssert(_station, "adoption-detail");
            Click(_station, "QuietSpace"); Click(_station, "OverlayConfirm");
            Assert.That(FeedbackContains("1/2"), Is.True);
            Click(_station, "OverlayBrowse");
            Assert.That(FeedbackContains("2/2"), Is.True);
            yield return layout.CaptureAndAssert(_station, "adoption-applicants");
            Click(_station, "OverlayConfirm");
            Assert.That(Button(_station, "OverlayConfirm").IsInteractable(), Is.False);
            Button(_station, "OverlayConfirm").onClick.Invoke(); // Simulate a queued duplicate.
            Assert.That(_root.Presenter.Session.Station.Animals.Single().AnimalId, Is.EqualTo(animal.AnimalId));
            yield return new WaitForSecondsRealtime(0.4f);
            yield return layout.CaptureAndAssert(_station, "adoption-farewell");
            Click(_station, "OverlayCancel");
            Assert.That(_root.Presenter.Session.Station.AvailableTemporaryPlaces, Is.EqualTo(1));
            yield return Load();
            Click(_station, "Animal0"); Click(_station, "QuietSpace");
            Assert.That(_root.Presenter.Progress.GetCustomName(animal.AnimalId), Is.EqualTo("柚子"));
            Assert.That(FeedbackContains("1/2"), Is.True, "Published state is durable.");
            Click(_station, "OverlayConfirm");
            yield return new WaitForSecondsRealtime(0.4f);
            _storage.FailWrites = true;
            Click(_station, "OverlayConfirm");
            Assert.That(FeedbackContains("保存失败"), Is.True);
            Assert.That(_root.Presenter.Session.Station.Animals.Count, Is.EqualTo(1));
            _storage.FailWrites = false;
            yield return Load();
            Click(_station, "Animal0"); Click(_station, "QuietSpace"); Click(_station, "OverlayConfirm");
            yield return new WaitForSecondsRealtime(0.4f);
            Click(_station, "OverlayConfirm");
            Assert.That(FeedbackContains("去新家"), Is.True);
            Assert.That(_root.Presenter.Session.Station.AvailableTemporaryPlaces, Is.EqualTo(2));
            Assert.That(_root.Presenter.Session.LastArrival, Is.Null);
            Assert.That(_root.Presenter.Session.Requests.Requests.Count(r => r.State == RequestState.Resolved),
                Is.EqualTo(1));
            Assert.That(_storage.Slots.Any(slot => slot != null && !slot.Contains(animal.AnimalId.ToString("N")) &&
                slot.Contains("Memorials")), Is.True, "The committed memorial keeps no detailed animal data.");
            yield return Load();
            Assert.That(_root.Presenter.Progress.CanContinue, Is.True);
            Assert.That(_root.Presenter.Session.Station.Animals, Is.Empty);
            Click(_station, "StationMenu"); Click(_station, "StationMemorial");
            Assert.That(FeedbackContains("柚子"), Is.True);
            Assert.That(_station.GetComponentsInChildren<Image>().Single(i => i.name == "DialogPortrait").sprite,
                Is.Not.Null);
            yield return layout.CaptureAndAssert(_station, "adoption-memorial");
            Click(_station, "OverlayCancel");
            Click(_station, "Restart"); Click(_map, "Yard"); Click(_map, "Primary");
            yield return WaitForPanel<Game.Approach.ApproachPanelView>();
        }

        private static IEnumerator WaitForPanel<T>() where T : MonoBehaviour
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (UnityEngine.Object.FindObjectOfType<T>() == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(UnityEngine.Object.FindObjectOfType<T>(), Is.Not.Null, typeof(T).Name);
        }

        [UnityTest]
        public IEnumerator BackgroundAndSceneReloadDoNotSettleUnendedCare()
        {
            yield return Load();
            Click(_station, "Restart"); Click(_map, "Primary"); MainApproachDriver.Complete();
            yield return Win();
            Click(_siege, "Retry"); Click(_station, "Enter"); Click(_station, "Animal0"); Click(_station, "Check");
            var arrival = _root.Presenter.Session.Station.Animals.Single();
            var original = _root.Presenter.Progress.Care.Get(arrival.AnimalId).Capture();
            long minutes = _root.Presenter.Session.Clock.Current.ElapsedMinutes;
            int writes = _storage.Writes;
            Click(_station, "Clean"); Canvas.ForceUpdateCanvases(); Stroke();
            _station.SendMessage("OnApplicationPause", true);
            Assert.That(_storage.Writes, Is.EqualTo(writes));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
            yield return Load();
            Assert.That(_root.Presenter.Progress.Care.Get(arrival.AnimalId).Capture().CopyDirt().CopySamples(),
                Is.EqualTo(original.CopyDirt().CopySamples()));
            Assert.That(_root.Presenter.Session.Clock.Current.ElapsedMinutes, Is.EqualTo(minutes));
        }

        [UnityTest]
        public IEnumerator ReadAndWriteFailuresShowFeedbackAndPreventProgressMutations()
        {
            _storage.Slots[0] = "preserved broken progress";
            yield return Load();
            Assert.That(_root.Presenter.Session, Is.Null);
            Assert.That(Button(_station, "Restart").IsInteractable(), Is.False);
            Assert.That(FeedbackContains("读取失败"), Is.True);
            Button(_station, "Restart").onClick.Invoke();
            Assert.That(_map.gameObject.activeInHierarchy, Is.False);
            Assert.That(_storage.Writes, Is.Zero);
            Assert.That(_storage.Slots[0], Is.EqualTo("preserved broken progress"));
            _storage.Slots[0] = null;
            yield return Load();
            _storage.FailWrites = true;
            Click(_station, "Restart");
            Assert.That(_root.Presenter.Progress.CanContinue, Is.False);
            Assert.That(_map.gameObject.activeInHierarchy, Is.False);
            Assert.That(FeedbackContains("保存失败"), Is.True);
            _storage.FailWrites = false;
            Button(_station, "Restart").onClick.Invoke();
            Assert.That(_root.Presenter.Session.Outing.Session, Is.Null);
            Assert.That(_storage.Writes, Is.Zero);
        }

        private bool FeedbackContains(string text) => _station.GetComponentsInChildren<Text>(true)
            .Any(label => label.isActiveAndEnabled && label.text.Contains(text));

        private IEnumerator Win()
        {
            var activity = (SiegeRescueActivity)_root.Presenter.Session.Activity;
            for (int i = 0; i < 700 && activity.Battle.Phase != SiegeState.BattlePhase.Won &&
                activity.Battle.Phase != SiegeState.BattlePhase.Timeout; i++)
            {
                if (activity.Battle.Realtime)
                {
                    var state = activity.Battle.Snapshot;
                    var card = state.Hand.FirstOrDefault(item =>
                        activity.Battle.Catalog.Unit(item.DefinitionId).Cost <= state.Energy);
                    if (card != null)
                    {
                        var lane = activity.Battle.Catalog.Lanes[state.Stats.Deployed % 3];
                        var point = lane.PointAt(lane.Length * .2);
                        if (!SiegeDeployment.CanDeploy(activity.Battle.Catalog, point)) point = lane.PointAt(0);
                        activity.Battle.TryOrder(new SiegeOrder
                        {
                            SessionId = state.SessionId, Turn = state.Turn, DeployCardId = card.Id,
                            DeployPoint = point
                        }, true, out _, out _);
                    }
                }
                else if (activity.Battle.Phase == SiegeState.BattlePhase.Decision)
                {
                    var cell = _siege.GetComponentsInChildren<SiegeChoiceCell>().FirstOrDefault();
                    if (cell != null) cell.GetComponent<Button>().onClick.Invoke();
                    // Exercise the physical routes instead of feeding every card into one defense.
                    string lane = new[] { "Top", "Mid", "Bottom" }[activity.Battle.Snapshot.Turn % 3];
                    Click(_siege, lane); Click(_siege, "Submit");
                }
                activity.Battle.Advance(1000);
                yield return null;
            }
            yield return null;
            Assert.That(activity.Battle.Phase, Is.EqualTo(SiegeState.BattlePhase.Won),
                activity.Battle.Snapshot.EncounterId + ": " +
                activity.Battle.Phase + ", progress=" + activity.Battle.Snapshot.Progress);
        }

        private void Stroke()
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)_station.Surface.transform;
            Assert.That(rect.rect.height, Is.GreaterThan(0), "Care must retain usable space at the current viewport.");
            for (int y = 2; y < 8; y++)
            for (int x = 2; x < 7; x++)
            {
                var local = new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x / 10f),
                    Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y / 10f));
                var pointer = new PointerEventData(EventSystem.current) { pointerId = 1,
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local)) };
                _station.Surface.OnPointerDown(pointer); _station.Surface.OnPointerUp(pointer);
            }
        }

        private static Button Button(Component view, string name)
        {
            if (view is StationPanelView station && name.StartsWith("Animal", StringComparison.Ordinal))
                return station.Scene.Cells[int.Parse(name.Substring(6))].SelectButton;
            return MainApproachDriver.Button(view, name);
        }
        private static void Click(Component view, string name)
        {
            if (view is CityMapView) { MainApproachDriver.Click(view, name); return; }
            if (view is SiegeTrialView sceneView && sceneView.Scene != null &&
                (name == "Retry" || name == "Pause"))
            { MainApproachDriver.Click(view, name); return; }
            if (name == "Retry" && view is SiegeTrialView siege && siege.Scene != null &&
                !Button(view, name).isActiveAndEnabled) MainApproachDriver.Click(view, "Pause");
            var button = Button(view, name);
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        private IEnumerator CaptureStation()
        {
            var canvas = _station.GetComponent<Canvas>();
            var camera = new GameObject("ContinuityCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            string folder = Path.GetFullPath("MainContinuityEvidence"); Directory.CreateDirectory(folder);
            foreach (var size in new[] { new Vector2Int(390, 844), new Vector2Int(540, 960),
                new Vector2Int(768, 1024) })
            {
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                yield return null; yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
                var previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); pixels.Apply();
                    RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(folder,
                    "two-residents-restored-" + size.x + "x" + size.y + ".png"), pixels.EncodeToPNG());
                UnityEngine.Object.Destroy(pixels); camera.targetTexture = null; target.Release();
                    UnityEngine.Object.Destroy(target);
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            UnityEngine.Object.Destroy(camera.gameObject);
        }
    }
}
