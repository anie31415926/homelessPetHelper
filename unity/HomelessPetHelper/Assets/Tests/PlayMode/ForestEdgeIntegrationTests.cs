#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HomelessPetHelper.Game;
using HomelessPetHelper.Game.Approach;
using HomelessPetHelper.Game.Field;
using HomelessPetHelper.Modules.Approach;
using HomelessPetHelper.Game.Text;
using HomelessPetHelper.Modules.Outing;
using HomelessPetHelper.Modules.Outing.UI;
using HomelessPetHelper.Modules.Rescue;
using HomelessPetHelper.Modules.Siege.UI;
using HomelessPetHelper.Modules.Station.UI;
using HomelessPetHelper.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HomelessPetHelper.Tests.PlayMode
{
    public sealed class ForestEdgeIntegrationTests
    {
        private MainSceneStorageScope _storage;
        [SetUp] public void IsolateStorage()
        {
            var storage = new MemorySaveStorage();
            _storage = new MainSceneStorageScope(() => storage);
        }
        [TearDown] public void ReleaseStorage() => _storage.Dispose();

        [UnityTest]
        public IEnumerator SitesWithoutRequestsAndConsecutiveOutingsKeepTheirLocationArtwork()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var root = Object.FindObjectOfType<MainGameRoot>();
            var session = root.Presenter.Session;
            var station = Object.FindObjectOfType<StationPanelView>(true);
            var map = Object.FindObjectOfType<CityMapView>(true);
            var approach = Object.FindObjectOfType<ApproachPanelView>(true);
            var siege = Object.FindObjectOfType<SiegeTrialView>(true);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var copy = new OptionPreviewText(content.MapText);
            MainApproachDriver.Click(station, "Restart");
            Assert.That(session.Requests.Requests.Count, Is.EqualTo(5));
            foreach (string site in new[] { "Market", "Riverside" })
            {
                MainApproachDriver.Click(map, site);
                Assert.That(session.Requests.Requests.Any(request => request.PointId == site.ToLowerInvariant()),
                    Is.False, site);
                Assert.That(session.Clock.Current.ElapsedMinutes, Is.Zero);
                Assert.That(session.Requests.Requests.All(request => request.State == RequestState.Available), Is.True);
                Assert.That(map.GetComponentsInChildren<Text>().Single(label => label.name == "Heading").text,
                    Is.EqualTo(copy.Get(site.ToLowerInvariant())));
            }
            yield return Capture(map, "main-map");
            foreach (string site in new[] { "Parking", "Yard" })
            {
                MainApproachDriver.Click(map, site);
                MainApproachDriver.Click(map, "Primary");
                var picture = content.GetOutingBackground(site.ToLowerInvariant());
                AssertBackground(approach, picture);
                approach.SendMessage("OnApplicationFocus", false);
                approach.SendMessage("OnApplicationFocus", true);
                Assert.That(approach.Session.IsSuspended, Is.True);
                MainApproachDriver.Click(approach, "Pause");
                AssertBackground(approach, picture);
                yield return Capture(approach, site + "-approach");
                MainApproachDriver.Complete();
                AssertBackground(siege, picture);
                yield return Capture(siege, site + "-siege");
                MainApproachDriver.Click(siege, "Retry");
                yield return ReturnFromMap(map);
                Assert.That(station.gameObject.activeInHierarchy, Is.True);
                Assert.That(session.Station.Animals, Is.Empty);
                MainApproachDriver.Click(station, "Restart");
            }
            MainApproachDriver.Click(map, "Cancel");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TreehouseKeepsKittenAndRoomWhenEnteringBattleAndRestoring()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var station = Object.FindObjectOfType<StationPanelView>(true);
            var map = Object.FindObjectOfType<CityMapView>(true);
            var approach = Object.FindObjectOfType<ApproachPanelView>(true);
            var siege = Object.FindObjectOfType<SiegeTrialView>(true);
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Settings/Game/GameContent.asset");
            var artwork = content.GetOutingArtwork("treehouse");
            var sprite = artwork.Background;
            MainApproachDriver.Click(station, "Restart");
            MainApproachDriver.Click(map, "Treehouse");
            MainApproachDriver.Click(map, "Primary");
            AssertBackground(approach, sprite);
            AssertForeground(approach.FieldScene, artwork.Foreground);
            Assert.That(approach.Session.Template.Mode, Is.EqualTo(ApproachMode.Cleanup));
            Assert.That(approach.FieldScene.Snapshot.AnimalRevealed, Is.False);
            var cleanupProgress = approach.GetComponentsInChildren<Slider>().Single(bar => bar.name == "CleanupWorkProgress");
            Assert.That(cleanupProgress.value, Is.EqualTo(0).Within(.001f));
            Assert.That(approach.FieldScene.Animal.GetComponent<FieldPetMotion>().Surface.gameObject.activeInHierarchy,
                Is.False);
            Canvas.ForceUpdateCanvases();
            foreach (int cell in new[] { 65, 57, 50, 3, 5 }) MainApproachDriver.Clean(approach, cell);
            Assert.That(approach.gameObject.activeInHierarchy, Is.True, "Opening waits for the player.");
            Assert.That(approach.FieldScene.Snapshot.AnimalRevealed, Is.True);
            Assert.That(cleanupProgress.value, Is.GreaterThan(0));
            yield return Capture(approach, "Treehouse-approach");
            MainApproachDriver.Clean(approach, 51);
            MainApproachDriver.Click(approach, "Continue");
            AssertBackground(siege, sprite);
            var field = siege.GetComponent<OutingFieldView>();
            AssertForeground(field, artwork.Foreground);
            Assert.That(field.Snapshot.AnimalRevealed, Is.True);
            Assert.That(field.Animal.GetComponent<FieldPetMotion>().Surface.gameObject.activeInHierarchy, Is.True);
            Assert.That(field.Snapshot.Remaining.Count(value => value > 0), Is.EqualTo(1));
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(siege.GetComponentsInChildren<Text>().Any(item => item.name == "DockResourceBalance"),
                Is.True, "Active energy indicator is beside the hand.");
            AssertCompactDock(siege, field);
            yield return Capture(siege, "Treehouse-siege");
            var card = siege.Scene.GetComponentsInChildren<SiegeChoiceCell>().First();
            var contact = new PointerEventData(EventSystem.current)
                { pointerId = 7, button = PointerEventData.InputButton.Left, eligibleForClick = true };
            card.OnPointerDown(contact);
            yield return new WaitForSecondsRealtime(.55f);
            card.OnPointerUp(contact);
            Assert.That(contact.eligibleForClick, Is.False, "Inspecting a card must not select or deploy it.");
            Assert.That(siege.Scene.Plan.SelectedCard, Is.Null);
            Assert.That(siege.Scene.GetComponentsInChildren<Text>().Single(text => text.name == "FieldDetail")
                .text, Is.Not.Empty);
            yield return Capture(siege, "Treehouse-card-detail");
            Object.FindObjectOfType<MainGameRoot>().Presenter.FlushFieldProgress();
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            siege = Object.FindObjectOfType<SiegeTrialView>(true);
            AssertBackground(siege, sprite);
            field = siege.GetComponent<OutingFieldView>();
            AssertForeground(field, artwork.Foreground);
            Assert.That(field.Snapshot.Remaining.Count(value => value > 0), Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TreehouseRefillsOneLoosePropOnAVisitButDoesNotRerollOnReload()
        {
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            void Depart()
            {
                MainApproachDriver.Click(Object.FindObjectOfType<StationPanelView>(), "Restart");
                var map = Object.FindObjectOfType<CityMapView>();
                MainApproachDriver.Click(map, "Treehouse");
                MainApproachDriver.Click(map, "Primary");
                Canvas.ForceUpdateCanvases();
            }
            Depart();
            var approach = Object.FindObjectOfType<ApproachPanelView>();
            var root = Object.FindObjectOfType<MainGameRoot>();
            var id = root.Presenter.OutingAnimal.AnimalId;
            foreach (int cell in new[] { 65, 57, 50, 3, 5, 51, 41 }) MainApproachDriver.Clean(approach, cell);
            Assert.That(approach.FieldScene.Snapshot.Remaining.All(value => value == 0), Is.True);
            MainApproachDriver.Click(approach, "Pause");
            MainApproachDriver.Click(approach, "Return");
            yield return ReturnFromMap(Object.FindObjectOfType<CityMapView>(true));
            var clock = root.Presenter.Session.Clock;
            Assert.That(clock.TryAdvance(clock.Current.Version, 120, out _, out _), Is.True);
            Depart();
            Assert.That(root.Presenter.OutingAnimal.AnimalId, Is.EqualTo(id));
            Assert.That(approach.Session.IsComplete && approach.Session.IsAnimalRevealed, Is.True);
            Assert.That(approach.FieldScene.Snapshot.Remaining.Count(value => value > 0), Is.EqualTo(1));
            var remaining = approach.FieldScene.Snapshot.Remaining.ToArray();
            Assert.That(clock.TryAdvance(clock.Current.Version, 120, out _, out _), Is.True);
            Assert.That(root.Presenter.Progress.TryCheckpoint(), Is.True);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            approach = Object.FindObjectOfType<ApproachPanelView>();
            Assert.That(approach, Is.Not.Null);
            Assert.That(approach.FieldScene.Snapshot.Remaining, Is.EqualTo(remaining));
            Assert.That(Object.FindObjectOfType<MainGameRoot>().Presenter.OutingAnimal.AnimalId, Is.EqualTo(id));
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator ReturnFromMap(CityMapView map)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!map.isActiveAndEnabled && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(map.isActiveAndEnabled, Is.True, "The field should return to the map.");
            MainApproachDriver.Click(map, "Cancel");
        }

        private static void AssertCompactDock(SiegeTrialView siege, OutingFieldView field)
        {
            var scene = siege.Scene;
            var sliders = siege.GetComponentsInChildren<Slider>();
            var energy = sliders.Single(bar => bar.name == "DockResourceProgress");
            var rescue = sliders.Single(bar => bar.name == "AnimalRescueProgress");
            Assert.That(energy.interactable || rescue.interactable, Is.False);
            Assert.That(sliders.Any(bar => bar.name == "FieldPlayerCharge" || bar.name == "FieldAnimalCharge"),
                Is.False, "Realtime mode replaces the old header gauges.");
            var battle = ((SiegeRescueActivity)Object.FindObjectOfType<MainGameRoot>().Presenter.Session.Activity).Battle;
            float expectedEnergy = Mathf.Clamp01((battle.Snapshot.Energy + (float)battle.Snapshot.ResourceChargeMs /
                battle.Catalog.Rules.ResourcePeriodMs) / battle.Catalog.Rules.EnergyCap);
            Assert.That(energy.value, Is.EqualTo(expectedEnergy).Within(.06f));
            var head = field.Animal.rectTransform.TransformPoint(new Vector3(field.Animal.rectTransform.rect.center.x,
                field.Animal.rectTransform.rect.yMax, 0));
            var bar = (RectTransform)rescue.transform;
            Assert.That(field.Frame.InverseTransformPoint(bar.position).y,
                Is.GreaterThan(field.Frame.InverseTransformPoint(head).y));
            Assert.That(bar.GetSiblingIndex(), Is.GreaterThan(field.Frame.GetComponentsInChildren<Image>()
                .Single(image => image.name == "RoomForeground").transform.GetSiblingIndex()));
            foreach (var card in scene.GetComponentsInChildren<SiegeChoiceCell>())
            {
                var rect = (RectTransform)card.transform;
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(143));
                Assert.That(rect.rect.height, Is.InRange(143f, 180f));
            }
        }

        private static void AssertForeground(OutingFieldView field, Sprite expected)
        {
            Assert.That(expected, Is.Not.Null);
            var railing = field.Frame.GetComponentsInChildren<Image>().Single(image => image.name == "RoomForeground");
            Assert.That(railing.isActiveAndEnabled, Is.True);
            Assert.That(railing.sprite, Is.SameAs(expected));
            Assert.That(railing.raycastTarget, Is.False, "Foreground art must not intercept cleanup or deployment.");
            Assert.That(railing.transform.GetSiblingIndex(), Is.GreaterThan(field.Animal.transform.GetSiblingIndex()),
                "The railing must occlude the animal and its animated child surface.");
        }

        private static void AssertBackground(Component view, Sprite expected)
        {
            Assert.That(view.gameObject.activeInHierarchy, Is.True);
            var image = view.GetComponentsInChildren<Image>().Single(item => item.name == "LocationBackground");
            Assert.That(image.sprite, Is.SameAs(expected));
            Assert.That(image.raycastTarget, Is.False, "Artwork must not intercept puzzle or battlefield input.");
        }

        private static IEnumerator Capture(Component view, string name)
        {
            var canvas = view.GetComponent<Canvas>();
            var camera = new GameObject("ForestEdgeCapture", typeof(Camera)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.cullingMask = 1 << canvas.gameObject.layer;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var target = new RenderTexture(540, 960, 24);
            camera.targetTexture = target;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string folder = Path.GetFullPath("../../forest-edge-screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            canvas.worldCamera = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            Object.Destroy(image);
            Object.Destroy(camera.gameObject);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
